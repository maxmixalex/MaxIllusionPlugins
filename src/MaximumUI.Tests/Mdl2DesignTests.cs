using System;
using System.Reflection;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class Mdl2DesignTests {
        internal const int TestCount = 22;
        private static readonly MuiRect ControlRect = new MuiRect(12f, 16f, 260f, 32f);

        public static void RunAll() {
            IconsHaveBoundedFontIndependentGeometry();
            NativeIconGeometryDoesNotAllocateAfterWarmup();
            CheckboxAndRadioHaveDistinctSelectionMarks();
            SwitchRetainsAVisibleStateWithoutDependingOnColor();
            DisabledSelectionDoesNotLookActive();
            DisabledContainersPropagateVisualState();
            TextInputBordersDistinguishHoverFocusAndDisabled();
            KeyboardFocusIsNotConfusedWithHover();
            SearchAndArrowButtonsUseGeometry();
            DisclosureLabelsContainOnlyTheirActualText();
            TypographyTravelsWithTheDrawCommand();
            TranslatedDrawListsPreserveTypography();
            ToggleSwitchSupportsTheWholeHitTarget();
            DisabledFocusedControlsReleaseKeyboardInput();
            ScaledTypographyKeepsControlsTallEnough();
            SmallControlGeometryStaysInsideItsBounds();
            DisabledButtonsDoNotRetainActionColors();
            ExpandedContainersFocusOnlyTheirHeader();
            TreeFrameAndConnectionFlagsRemainFunctional();
            PlotLinesConnectFiniteSamplesAndRespectBounds();
            ThemeEditorUsesAlignedColorColumns();
            NumericLabelsAndValuesUseTheirMeasuredWidth();
        }

        private static void IconsHaveBoundedFontIndependentGeometry() {
            MuiRect rect = new MuiRect(37f, 51f, 32f, 32f);
            foreach (Mdl2Icon icon in Enum.GetValues(typeof(Mdl2Icon))) {
                DrawList drawing = new DrawList();
                Mdl2IconGeometry.Draw(drawing, rect, icon, new MuiColor(0f, 0f, 0f, 1f), 1f);
                Assert(drawing.Commands.Count > 0, "Every supported icon must produce geometry.");
                foreach (DrawCommand command in drawing.Commands) {
                    Assert(command.Type == DrawCommandType.Line, "Icons must not depend on a text font or Unicode fallback.");
                    Assert(Contains(rect, command.P1) && Contains(rect, command.P2), "An icon must remain inside its assigned bounds.");
                    Assert(command.P1.X != command.P2.X || command.P1.Y != command.P2.Y, "An icon segment must be visible.");
                }
            }
        }

        private static void NativeIconGeometryDoesNotAllocateAfterWarmup() {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return;
            Func<long> bytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            Mdl2Icon[] icons = (Mdl2Icon[])Enum.GetValues(typeof(Mdl2Icon));
            float sum = 0f;
            for (int warm = 0; warm < 10; warm++) sum += ReadIconSegments(icons);
            long before = bytes();
            for (int iteration = 0; iteration < 10000; iteration++) sum += ReadIconSegments(icons);
            long allocated = bytes() - before;
            Assert(sum > 0f && allocated == 0, "Native icon sampling must not allocate per repaint; measured " + allocated + " bytes.");
        }

        private static float ReadIconSegments(Mdl2Icon[] icons) {
            float total = 0f;
            foreach (Mdl2Icon icon in icons) {
                for (int i = 0; i < Mdl2IconGeometry.SegmentCount(icon); i++) {
                    MuiVec2 a, b;
                    Mdl2IconGeometry.Segment(icon, i, ControlRect, out a, out b);
                    total += a.X + b.Y;
                }
            }
            return total;
        }

        private static void CheckboxAndRadioHaveDistinctSelectionMarks() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            DrawList checkbox = Paint(new Checkbox("Selected", true), theme, null);
            DrawList radio = Paint(new RadioButton("Selected", true), theme, null);
            Assert(Count(checkbox, DrawCommandType.Line) >= 2, "A selected checkbox must draw a check mark instead of an ambiguous inner square.");
            Assert(Count(radio, DrawCommandType.FillCircle) >= 2 || Count(radio, DrawCommandType.StrokeCircle) > 0,
                "A radio button must use a circular outline and a circular selected state.");
            Assert(TextCount(checkbox, "Selected") == 1 && TextCount(radio, "Selected") == 1, "Selection labels appear exactly once.");
        }

        private static void SwitchRetainsAVisibleStateWithoutDependingOnColor() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            MuiRect track = Mdl2ControlChrome.SwitchBounds(ControlRect);
            MuiRect off = Mdl2ControlChrome.SwitchThumb(track, false);
            MuiRect on = Mdl2ControlChrome.SwitchThumb(track, true);
            Assert(on.X > off.X && on.Y == off.Y && on.Width == off.Width, "Switch state must also be conveyed by thumb position.");
            Assert(Contains(track, off) && Contains(track, on), "Both switch states must remain inside the track.");
            DrawList drawing = Paint(new ToggleSwitch("Enabled", true), theme, null);
            Assert(Count(drawing, DrawCommandType.FillCircle) >= 3, "The switch needs a track and a separate visible thumb.");
            Assert(TextCount(drawing, "Enabled") == 1, "The switch retains its actual label.");
        }

        private static void DisabledSelectionDoesNotLookActive() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Widget[] controls = { new Toggle("Locked", true), new Checkbox("Selected", true),
                new RadioButton("Selected", true), new ToggleSwitch("Enabled", true), new Slider(0.5f, 0f, 1f) };
            foreach (Widget widget in controls) {
                widget.Enabled = false;
                DrawList drawing = Paint(widget, theme, new WidgetPaintState { HoveredPath = "test", ActivePath = "test", FocusedPath = "test" });
                foreach (DrawCommand command in drawing.Commands) {
                    Assert(!Same(command.Color, theme.Accent) && !Same(command.Color, theme.AccentHover) && !Same(command.Color, theme.AccentPressed),
                        "A disabled " + widget.GetType().Name + " must not retain enabled accent feedback.");
                }
            }
        }

        private static void DisabledContainersPropagateVisualState() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Panel panel = new Panel { Enabled = false };
            panel.Add(new Button("Apply") { Variant = ButtonVariant.Primary });
            panel.Add(new Checkbox("Selected", true));
            panel.Add(new RadioButton("Selected", true));
            DrawList drawing = Paint(panel, theme, null);
            foreach (DrawCommand command in drawing.Commands) {
                Assert(!Same(command.Color, theme.Accent), "Disabling a panel must disable all nested control visuals without mutating child properties.");
            }
            Assert(panel.Children[0].Enabled, "Inherited disabled state must not overwrite a retained child's own Enabled value.");
        }

        private static void TextInputBordersDistinguishHoverFocusAndDisabled() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Widget[] fields = { new TextField("Value"), new SearchBox("Value"), new PasswordField("Value"), new TextArea("Value") };
            foreach (Widget field in fields) {
                Assert(HasBorder(Paint(field, theme, null), ControlRect, theme.ControlBorder, 2f), "Resting input needs a complete neutral 2px border.");
                Assert(HasBorder(Paint(field, theme, new WidgetPaintState { HoveredPath = "test" }), ControlRect, theme.ControlHoverBorder, 2f), "Hovered input needs a complete neutral hover border.");
                Assert(HasBorder(Paint(field, theme, new WidgetPaintState { FocusedPath = "test" }), ControlRect, theme.Accent, 2f), "Keyboard focus must outline all four input edges.");
                field.Enabled = false;
                DrawList disabled = Paint(field, theme, new WidgetPaintState { FocusedPath = "test" });
                Assert(!HasColor(disabled, theme.Accent), "Disabled input cannot continue to appear focused.");
            }
        }

        private static void KeyboardFocusIsNotConfusedWithHover() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Button button = new Button("Apply") { Variant = ButtonVariant.Primary };
            DrawList focused = Paint(button, theme, new WidgetPaintState { FocusedPath = "test" });
            DrawList hovered = Paint(button, theme, new WidgetPaintState { HoveredPath = "test" });
            Assert(HasStroke(focused, theme.FocusRing), "Keyboard-focused primary buttons require a separate focus outline.");
            Assert(!HasStroke(hovered, theme.FocusRing), "Pointer hover must not masquerade as keyboard focus.");
        }

        private static void SearchAndArrowButtonsUseGeometry() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            DrawList search = Paint(new SearchBox(string.Empty), theme, null);
            Assert(Count(search, DrawCommandType.Line) > 2 && TextCount(search, "S") == 0, "Search must draw a magnifier rather than a letter S or empty reserved space.");
            DrawList clear = Paint(new SearchBox("Find"), theme, null);
            Assert(Count(clear, DrawCommandType.Line) == 2 && TextCount(clear, "Find") == 1, "A populated search field must show the clear icon without changing its text.");
            DrawList readOnly = Paint(new SearchBox("Find") { Flags = TextInputFlags.ReadOnly }, theme, null);
            Assert(Count(readOnly, DrawCommandType.Line) == Mdl2IconGeometry.SegmentCount(Mdl2Icon.Search) && TextCount(readOnly, "Find") == 1,
                "Read-only search retains its value and magnifier instead of offering an inoperable clear action.");
            foreach (ArrowDirection direction in Enum.GetValues(typeof(ArrowDirection))) {
                DrawList arrow = Paint(new ArrowButton(direction), theme, null);
                Assert(Count(arrow, DrawCommandType.Line) >= 2, "Arrow buttons must use shared chevron geometry.");
                foreach (DrawCommand command in arrow.Commands)
                    Assert(command.Type != DrawCommandType.Text || string.IsNullOrEmpty(command.Text), "Arrow buttons cannot depend on text-glyph substitutions.");
            }
        }

        private static void DisclosureLabelsContainOnlyTheirActualText() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Widget[] controls = { new Foldout("Advanced", false), new TreeNode("Advanced", false),
                new ComboBox<string>("Mode", "Normal", new[] { "Normal", "Compact" }, delegate(string value) { return value; }, null) };
            foreach (Widget control in controls) {
                DrawList drawing = Paint(control, theme, null);
                Assert(Count(drawing, DrawCommandType.Line) >= 2, "Disclosure controls need visible shared chevron geometry.");
                foreach (DrawCommand command in drawing.Commands) {
                    if (command.Type != DrawCommandType.Text) continue;
                    Assert(command.Text.IndexOf("[+]") < 0 && command.Text.IndexOf("[-]") < 0 && command.Text.IndexOf("▾") < 0 && command.Text != "v" && command.Text != ">",
                        "Control icons must not be concatenated into their accessible text labels.");
                }
            }
        }

        private static void TypographyTravelsWithTheDrawCommand() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            LabelRole[] roles = { LabelRole.Body, LabelRole.Heading, LabelRole.Subheading, LabelRole.Caption };
            foreach (LabelRole role in roles) {
                DrawList drawing = Paint(new Label("Typography") { Role = role }, theme, null);
                DrawCommand text = FindText(drawing, "Typography");
                Assert(text != null && text.TextFormat.FontSize == ThemeTypography.FontSize(theme, role),
                    "The production draw list must carry typography; exporters must not guess a label's role.");
            }
            DrawCommand action = FindText(Paint(new Button("Apply"), theme, null), "Apply");
            Assert(action != null && action.TextFormat.Alignment == TextAlignment.Center, "Command labels retain centered alignment across backends.");
        }

        private static void TranslatedDrawListsPreserveTypography() {
            DrawList source = new DrawList();
            TextFormat format = new TextFormat(24f, TextAlignment.End, TextWeight.Semilight, true, TextVerticalAlignment.Top);
            source.Text(new MuiRect(2f, 3f, 200f, 60f), "A heading", null, new MuiColor(0f, 0f, 0f, 1f), format);
            DrawList translated = new DrawList();
            translated.Append(source, new MuiVec2(12f, 16f));
            DrawCommand command = translated.Commands[0];
            Assert(command.Rect.X == 14f && command.Rect.Y == 19f, "Translated text follows the drawing origin.");
            Assert(command.TextFormat.FontSize == 24f && command.TextFormat.Alignment == TextAlignment.End &&
                command.TextFormat.Weight == TextWeight.Semilight && command.TextFormat.WordWrap && command.TextFormat.VerticalAlignment == TextVerticalAlignment.Top,
                "Translation must preserve font size, weight, wrapping, and both alignments.");
            Assert(source.Commands[0].Rect.X == 2f, "Appending cannot mutate source geometry.");
        }

        private static void ToggleSwitchSupportsTheWholeHitTarget() {
            ToggleSwitch control = new ToggleSwitch("Enabled", true);
            LayoutNode root = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(ThemePalettes.MetroLight())).Layout(control, ControlRect, "switch");
            InputDispatcher input = new InputDispatcher();
            MuiVec2 labelPosition = new MuiVec2(ControlRect.Right - 10f, ControlRect.Y + 16f);
            InputDispatchResult down = input.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseDown, MouseButton = 0, MousePosition = labelPosition }, root);
            InputDispatchResult up = input.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseUp, MouseButton = 0, MousePosition = labelPosition }, root);
            Assert(down.Consumed && ReferenceEquals(up.ClickedWidget, control), "The switch label participates in the hit target.");
            input.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseDown, MouseButton = 0, MousePosition = labelPosition }, root);
            up = input.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseUp, MouseButton = 0, MousePosition = new MuiVec2(-10f, -10f) }, root);
            Assert(up.ClickedWidget == null, "Dragging a switch press outside cancels activation.");
        }

        private static void DisabledFocusedControlsReleaseKeyboardInput() {
            TextField field = new TextField("Value");
            Panel container = new Panel { field };
            LayoutNode root = new LayoutEngine().Layout(container, new MuiRect(0f, 0f, 300f, 100f), "root");
            MuiRect rect = root.Children[0].Rect;
            MuiInputEvent down = new MuiInputEvent { Type = MuiEventType.MouseDown, MouseButton = 0, MousePosition = new MuiVec2(rect.X + 4f, rect.Y + 4f) };
            InputDispatcher input = new InputDispatcher();
            Assert(input.Dispatch(down, root).Consumed, "The text field first receives focus.");
            field.Enabled = false;
            InputDispatchResult key = input.Dispatch(new MuiInputEvent { Type = MuiEventType.KeyDown, KeyCode = 65 }, root);
            Assert(!key.Consumed && key.FocusedWidget == null && key.ActivePath == null, "Disabling a focused field immediately relinquishes keyboard input and capture.");
            field.Enabled = true;
            input.Dispatch(down, root);
            container.Enabled = false;
            key = input.Dispatch(new MuiInputEvent { Type = MuiEventType.TextInput, Character = 'a' }, root);
            Assert(!key.Consumed && key.FocusedWidget == null, "Disabling an ancestor also relinquishes its descendant's keyboard input.");
        }

        private static void ScaledTypographyKeepsControlsTallEnough() {
            foreach (float size in new[] { 14f, 18f, 24f }) {
                ThemeTokens theme = ThemePalettes.MetroLight(); theme.FontSize = size;
                LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(theme);
                VStack controls = new VStack(8f) { new Button("Apply"), new TextField("Input"), new ToggleSwitch("Enabled", true) };
                LayoutNode root = new LayoutEngine(metrics).Layout(controls, new MuiRect(0f, 0f, 320f, 300f));
                foreach (LayoutNode node in root.Children)
                    Assert(node.Rect.Height >= ThemeTypography.LineHeight(theme, LabelRole.Body) + 8f, "Large text must retain vertical padding in all common controls.");
                Assert(root.Children[1].Rect.Y >= root.Children[0].Rect.Bottom + 8f, "Text scaling preserves row separation.");
                if (size == 14f)
                    Assert(ThemeTypography.FontSize(theme, LabelRole.Heading) == 24f && ThemeTypography.LineHeight(theme, LabelRole.Heading) == 28f,
                        "Default MDL2 headings retain the original 24px font and 28px line height.");
                if (size == 18f) {
                    Assert(ThemeTypography.FontSize(theme, LabelRole.Heading) == 31f && ThemeTypography.LineHeight(theme, LabelRole.Heading) == 40f,
                        "A scaled 31px heading needs a 40px grid-aligned line to contain its measured descenders.");
                    MuiRect title = ThemeTypography.DialogTitleBounds(new MuiRect(10f, 20f, 400f, 260f), theme);
                    Assert(title.Height == 40f, "Native and managed dialogs must share the same large-text title height.");
                }
            }
        }

        private static void SmallControlGeometryStaysInsideItsBounds() {
            foreach (float size in new[] { 0f, 1f, 4f, 8f, 16f, 32f }) {
                MuiRect rect = new MuiRect(10f, 20f, size, size);
                Assert(Contains(rect, Mdl2ControlChrome.SelectionBounds(rect, 20f)), "Clipped checkbox geometry must not escape a small viewport.");
                Assert(Contains(rect, Mdl2ControlChrome.FocusBounds(rect)), "Focus outlines must remain inside their control.");
                MuiRect track = Mdl2ControlChrome.SwitchBounds(rect);
                Assert(Contains(rect, track) && Contains(track, Mdl2ControlChrome.SwitchThumb(track, false)) && Contains(track, Mdl2ControlChrome.SwitchThumb(track, true)),
                    "A small switch must not draw its thumb outside its visible track.");
            }
        }

        private static void DisabledButtonsDoNotRetainActionColors() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            foreach (ButtonVariant variant in Enum.GetValues(typeof(ButtonVariant))) {
                Button button = new Button("Action") { Variant = variant, Enabled = false };
                DrawList drawing = Paint(button, theme, new WidgetPaintState { HoveredPath = "test", ActivePath = "test", FocusedPath = "test" });
                Assert(!HasColor(drawing, theme.Accent) && !HasColor(drawing, theme.AccentHover) && !HasColor(drawing, theme.AccentPressed),
                    "A disabled primary action must not retain the active accent in any pointer/focus state.");
                Assert(Same(FindText(drawing, "Action").Color, theme.TextDisabled), "All disabled button variants must share the disabled text color.");
                Assert(variant == ButtonVariant.Quiet ? Count(drawing, DrawCommandType.FillRect) == 0 : HasColor(drawing, theme.ButtonDisabledBg),
                    "Disabled quiet commands remain transparent; filled commands use the disabled surface.");
            }
        }

        private static void ExpandedContainersFocusOnlyTheirHeader() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(theme);
            Widget[] containers = { new Foldout("Group", true), new CollapsingHeader("Group", true),
                new ClosableCollapsingHeader("Group", true), new TreeNode("Group", true) };
            foreach (Widget container in containers) {
                container.Add(new Button("Child"));
                LayoutNode node = new LayoutEngine(metrics).Layout(container, new MuiRect(10f, 20f, 260f, 160f), "focus");
                DrawList drawing = new WidgetPainter(theme).Paint(node, new WidgetPaintState { FocusedPath = "focus" });
                MuiRect header = new MuiRect(node.Rect.X, node.Rect.Y, node.Rect.Width, metrics.RowHeight);
                Assert(HasBorder(drawing, Mdl2ControlChrome.FocusBounds(header), theme.FocusRing, 1f),
                    "Focus belongs to the interactive disclosure header, not to the entire expanded subtree.");
            }
        }

        private static void TreeFrameAndConnectionFlagsRemainFunctional() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(theme);
            TreeNode tree = new TreeNode("Group", true) { new TreeLeaf("First"), new TreeLeaf("Last") };
            LayoutNode node = new LayoutEngine(metrics).Layout(tree, new MuiRect(10f, 20f, 260f, 160f), "tree");
            DrawList plain = new WidgetPainter(theme).Paint(node, new WidgetPaintState());
            tree.Framed = true;
            tree.DrawLines = true;
            DrawList framed = new WidgetPainter(theme).Paint(node, new WidgetPaintState());
            Assert(!HasStroke(plain, theme.Border) && HasStroke(framed, theme.Border), "Framed must add the optional tree header border without framing ordinary rows.");
            bool connector = false;
            foreach (DrawCommand command in framed.Commands)
                if (command.Type == DrawCommandType.Line && Same(command.Color, theme.Separator) && command.P1.X == command.P2.X && command.P2.Y > command.P1.Y) connector = true;
            Assert(connector && Count(framed, DrawCommandType.Line) > Count(plain, DrawCommandType.Line), "DrawLines must keep a visible connector to the expanded children.");
        }

        private static void PlotLinesConnectFiniteSamplesAndRespectBounds() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            PlotLines plot = new PlotLines(new[] { -100f, 0.5f, 100f }) { HasRange = true, Min = 0f, Max = 1f, OverlayText = "Rate" };
            DrawList drawing = Paint(plot, theme, null);
            Assert(Count(drawing, DrawCommandType.Line) == 2, "Three finite samples require two visible connecting segments, not a scatter of pixels.");
            foreach (DrawCommand command in drawing.Commands)
                if (command.Type == DrawCommandType.Line) Assert(Contains(ControlRect, command.P1) && Contains(ControlRect, command.P2), "Explicit plot ranges must clamp points within the plot surface.");
            Assert(FindText(drawing, "Rate").TextFormat.Alignment == TextAlignment.Center, "Plot overlay text must remain centered consistently with native painting.");
            plot.Values = new[] { 0f, 0.25f, float.NaN, 0.75f, 1f, float.PositiveInfinity };
            plot.HasRange = false;
            drawing = Paint(plot, theme, null);
            Assert(Count(drawing, DrawCommandType.Line) == 2, "Missing/nonfinite plot samples must break lines, without crossing gaps or creating NaN geometry.");
            foreach (DrawCommand command in drawing.Commands)
                if (command.Type == DrawCommandType.Line) Assert(Contains(ControlRect, command.P1) && Contains(ControlRect, command.P2), "Automatic ranges must ignore nonfinite samples.");
            drawing = Paint(new PlotLines(new[] { 0.5f }), theme, null);
            Assert(Count(drawing, DrawCommandType.FillCircle) == 1, "A single sample remains visible as one marker.");
            foreach (ThemeTokens palette in new[] { ThemePalettes.MetroLight(), ThemePalettes.MetroDark() }) {
                Widget[] plots = { new PlotLines(new[] { 0f, 1f }) { OverlayText = "Rate" },
                    new PlotHistogram(new[] { 1f, 0.5f, 1f }) { OverlayText = "Rate" } };
                foreach (Widget control in plots) {
                    drawing = Paint(control, palette, null);
                    int textIndex = -1;
                    for (int i = 0; i < drawing.Commands.Count; i++)
                        if (drawing.Commands[i].Type == DrawCommandType.Text && drawing.Commands[i].Text == "Rate") textIndex = i;
                    Assert(textIndex > 0, "Every plot retains its overlay label.");
                    DrawCommand backing = drawing.Commands[textIndex - 1];
                    Assert(backing.Type == DrawCommandType.FillRect && Same(backing.Color, palette.FieldBg) && Contains(ControlRect, backing.Rect),
                        "Both themes need a field-colored backing immediately before overlay text so bars and lines cannot reduce text contrast.");
                    Assert(backing.Rect.Width < ControlRect.Width && backing.Rect.Height < ControlRect.Height,
                        "The contrast backing covers the label instead of obscuring the complete plot.");
                }
            }
        }

        private static void ThemeEditorUsesAlignedColorColumns() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            ThemeEditor editor = new ThemeEditor(theme) {
                ShowTitle = false, ShowScalars = false, MaxRows = 3,
                ColorTokens = new[] { ThemeColorToken.Text, ThemeColorToken.WindowBg, ThemeColorToken.ControlHoverBorder }
            };
            LayoutNode node = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(theme)).Layout(editor, new MuiRect(10f, 20f, 800f, 100f), "editor");
            DrawList drawing = new WidgetPainter(theme).Paint(node, new WidgetPaintState());
            float x = -1f;
            int redComponents = 0;
            foreach (DrawCommand command in drawing.Commands) {
                if (command.Type != DrawCommandType.Text || !command.Text.StartsWith("R ", StringComparison.Ordinal)) continue;
                if (x < 0f) x = command.Rect.X;
                Assert(command.Rect.X == x, "All color rows must share the same component column start, regardless of token-label length.");
                redComponents++;
            }
            Assert(redComponents == 3, "Every requested color token must retain its editable components.");
            Assert(FindText(drawing, "Text").Rect.Width == FindText(drawing, "ControlHoverBorder").Rect.Width, "Theme token labels use one measured grid column.");
        }

        private static void NumericLabelsAndValuesUseTheirMeasuredWidth() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(theme);
            const string label = "数值范围上限";
            const string number = "2147483647";
            SliderInt slider = new SliderInt(label, int.MaxValue, 0, int.MaxValue);
            LayoutNode node = new LayoutEngine(metrics).Layout(slider, new MuiRect(10f, 20f, 500f, metrics.RowHeight), "slider");
            DrawList drawing = new WidgetPainter(theme).Paint(node, new WidgetPaintState());
            DrawCommand labelText = FindText(drawing, label), valueText = FindText(drawing, number);
            Assert(labelText != null && labelText.Rect.Width >= metrics.MeasureText(label), "Numeric labels must reserve measured CJK width rather than a fixed 82px cap.");
            Assert(valueText != null && valueText.Rect.Width >= metrics.MeasureText(number), "Long numeric values must retain enough width without reducing the slider to a negative rectangle.");
            Assert(labelText.Rect.Right < valueText.Rect.X, "Adaptive label/value widths must leave a distinct central track region.");
        }

        private static DrawList Paint(Widget widget, ThemeTokens theme, WidgetPaintState state) {
            LayoutNode node = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(theme)).Layout(widget, ControlRect, "test");
            return new WidgetPainter(theme).Paint(node, state ?? new WidgetPaintState());
        }

        private static int Count(DrawList drawing, DrawCommandType type) {
            int count = 0; foreach (DrawCommand command in drawing.Commands) if (command.Type == type) count++; return count;
        }

        private static int TextCount(DrawList drawing, string value) {
            int count = 0; foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Text && command.Text == value) count++; return count;
        }

        private static DrawCommand FindText(DrawList drawing, string value) {
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Text && command.Text == value) return command; return null;
        }

        private static bool HasColor(DrawList drawing, MuiColor color) {
            foreach (DrawCommand command in drawing.Commands) if (Same(command.Color, color)) return true; return false;
        }

        private static bool HasStroke(DrawList drawing, MuiColor color) {
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.StrokeRect && Same(command.Color, color)) return true; return false;
        }

        private static bool HasBorder(DrawList drawing, MuiRect rect, MuiColor color, float thickness) {
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.StrokeRect && Same(command.Color, color) && command.Thickness >= thickness &&
                command.Rect.X == rect.X && command.Rect.Y == rect.Y && command.Rect.Width == rect.Width && command.Rect.Height == rect.Height) return true;
            return false;
        }

        private static bool Contains(MuiRect rect, MuiVec2 point) {
            return point.X >= rect.X - 0.001f && point.Y >= rect.Y - 0.001f && point.X <= rect.Right + 0.001f && point.Y <= rect.Bottom + 0.001f;
        }

        private static bool Contains(MuiRect outer, MuiRect inner) {
            return Contains(outer, new MuiVec2(inner.X, inner.Y)) && Contains(outer, new MuiVec2(inner.Right, inner.Bottom));
        }

        private static bool Same(MuiColor a, MuiColor b) {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new InvalidOperationException("MDL2 regression: " + message);
        }
    }
}
