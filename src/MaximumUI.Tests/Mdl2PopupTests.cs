using System;
using System.Reflection;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class Mdl2PopupTests {
        internal const int TestCount = 18;

        public static void RunAll() {
            DropdownFitsOffsetAndSmallViewports();
            DropdownKeepsFilterSeparateAndLastRowReachable();
            DropdownVirtualizesLongLists();
            EmptyDropdownHasAnOperationalStatus();
            MenuMeasuresTextAndSeparatesShortcutColumns();
            DisabledMenuItemsIgnoreHoverAndKeepDisabledText();
            LongMenuScrollMakesItsLastActionReachable();
            ScrollbarThumbsRemainBoundedAtBothEnds();
            NativePopupGeometryDoesNotAllocateAfterWarmup();
            TooltipReservesMeasuredTextHeightAndPadding();
            TooltipWrapsAndFlipsWithinTheViewport();
            OpenPopupHostsRemainVisiblyAssociatedWithTheirOverlay();
            PopupInputOwnershipDoesNotDisableBackgroundAppearance();
            DisabledPopupOwnersReleaseInputBeforeRepaint();
            ClosingAfterSelectionKeepsItsQueuedActionValid();
            ReopeningOrCancellingRejectsOldQueuedActions();
            ReplacingOwnersAndDisablingMenuAncestorsRejectsQueuedActions();
            StableOwnerCaptureDoesNotAllocatePerGuiEvent();
        }

        private static void DisabledPopupOwnersReleaseInputBeforeRepaint() {
            Panel root = new Panel();
            VStack parent = new VStack();
            Button owner = new Button("Options");
            root.Add(parent); parent.Add(owner);
            Widget[] chain = { root, parent, owner };
            PopupOwnerState state = new PopupOwnerState();
            Assert(state.Capture(chain, true) && state.BeginOpen(), "An enabled owner can open its popup.");
            int invoked = 0;
            Action pending = state.Guard(delegate { invoked++; });
            parent.Enabled = false;
            Assert(!state.IsEnabled, "Disabling an ancestor must invalidate popup input immediately, without waiting for Repaint or another Capture.");
            pending();
            Assert(invoked == 0 && !state.Capture(chain, true) && !state.Open, "A disabled ancestor rejects queued selection and closes the retained popup.");
            parent.Enabled = true;
            state.Capture(chain, true); state.BeginOpen();
            pending = state.Guard(delegate { invoked++; });
            owner.Enabled = false;
            pending();
            Assert(invoked == 0 && !state.IsEnabled, "The owner's own disabled state also prevents delivery.");
        }

        private static void ClosingAfterSelectionKeepsItsQueuedActionValid() {
            Button owner = new Button("Options");
            Widget[] chain = { owner };
            ComboBoxState combo = new ComboBoxState();
            RenderContext context = new RenderContext();
            combo.PopupOwner.Capture(chain, true); combo.PopupOwner.BeginOpen();
            context.ComboBoxStates.Add(combo);
            int invoked = 0;
            Action pending = combo.PopupOwner.Guard(delegate { invoked++; });
            combo.Open = false;
            combo.PopupOwner.Close();
            context.PruneClosedComboBoxes();
            int version = combo.PopupOwner.Version;
            Assert(!GuiEventPolicy.ControlEnabled(true, true, true, UnityEngine.EventType.MouseDown), "Popup ownership suppresses background IMGUI input.");
            Assert(combo.PopupOwner.Capture(chain, true) && combo.PopupOwner.Version == version,
                "Temporary GUI input suppression must not enter the model-enabled chain or replace its owner.");
            pending();
            Assert(invoked == 1 && context.ComboBoxStates.Count == 0,
                "Production close/prune after a valid selection must preserve its queued action through the following background pass.");
            MenuBar bar = new MenuBar(); Menu menu = new Menu("File"); bar.Add(menu);
            Widget[] menuChain = { bar };
            PopupOwnerState menuOwner = new PopupOwnerState();
            menuOwner.Capture(menuChain, true); menuOwner.CapturePopupPath(new Widget[] { menu }, 0, 1); menuOwner.BeginOpen();
            pending = menuOwner.Guard(delegate { invoked++; });
            menuOwner.Close();
            version = menuOwner.Version;
            menuOwner.Capture(menuChain, true);
            pending();
            Assert(invoked == 2 && menuOwner.Version == version,
                "A closed menu must preserve its selected submenu path during background Capture until the valid action is delivered.");
        }

        private static void ReopeningOrCancellingRejectsOldQueuedActions() {
            Widget[] chain = { new Button("Options") };
            MenuBarState menu = new MenuBarState();
            menu.PopupOwner.Capture(chain, true); menu.PopupOwner.BeginOpen();
            int invoked = 0;
            Action old = menu.PopupOwner.Guard(delegate { invoked++; });
            menu.PopupOwner.Close(); menu.PopupOwner.BeginOpen();
            old();
            Assert(invoked == 0, "A reopened popup must not deliver a selection queued by its previous open session.");
            Action current = menu.PopupOwner.Guard(delegate { invoked++; });
            menu.PopupOwner.Close(); current();
            Assert(invoked == 1, "Closing the current selection does not invalidate its own delivery.");
            menu.PopupOwner.BeginOpen();
            current = menu.PopupOwner.Guard(delegate { invoked++; });
            RenderContext context = new RenderContext { ActiveMenuBar = menu };
            context.CloseMenus(); current();
            Assert(invoked == 1, "Explicit menu cancellation invalidates queued actions even while the owner remains enabled.");
        }

        private static void ReplacingOwnersAndDisablingMenuAncestorsRejectsQueuedActions() {
            Button first = new Button("First") { Key = "retained-owner" };
            Button replacement = new Button("Replacement") { Key = "retained-owner" };
            PopupOwnerState state = new PopupOwnerState();
            state.Capture(new Widget[] { first }, true); state.BeginOpen();
            int invoked = 0;
            Action old = state.Guard(delegate { invoked++; });
            state.Capture(new Widget[] { replacement }, true);
            old();
            Assert(invoked == 0, "Reusing a key for a replacement widget invalidates the old owner's queued callback.");
            MenuBar bar = new MenuBar();
            Menu file = new Menu("File"), nested = new Menu("Advanced");
            MenuItem item = new MenuItem("Apply", delegate { invoked++; });
            bar.Add(file); file.Add(nested); nested.Add(item);
            state.Capture(new Widget[] { bar }, true);
            state.CapturePopupPath(new Widget[] { file, nested }, 0, 2); state.BeginOpen();
            Action selected = state.Guard(item.OnClick, item);
            file.Enabled = false; selected();
            Assert(invoked == 0 && !state.IsEnabled, "A parent menu disabled after selection must also reject delivery.");
            file.Enabled = true;
            selected = state.Guard(item.OnClick, item);
            item.Enabled = false; selected();
            Assert(invoked == 0, "A selected menu item disabled before queue delivery must not execute.");
            item.Enabled = true;
            file.Children.Clear(); selected();
            Assert(invoked == 0 && !state.IsEnabled, "Replacing a submenu invalidates the retained popup path even before another render.");
        }

        private static void StableOwnerCaptureDoesNotAllocatePerGuiEvent() {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return;
            Func<long> bytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            MenuBar bar = new MenuBar(); Menu menu = new Menu("File"); bar.Add(menu);
            // Match ImguiRenderer's reused ownerAncestors list, including its menu path.
            System.Collections.Generic.List<Widget> chain = new System.Collections.Generic.List<Widget> { new Panel(), new VStack(), bar };
            System.Collections.Generic.List<Widget> menuPath = new System.Collections.Generic.List<Widget> { menu };
            PopupOwnerState state = new PopupOwnerState();
            System.Collections.Generic.List<Widget> restored = new System.Collections.Generic.List<Widget>(chain.Count);
            const int iterations = 10000;
            long[] allocated = new long[3];
            int[] valid = new int[3], versions = new int[3], restoredCounts = new int[3];
            state.Capture(chain, true);
            state.CapturePopupPath(menuPath, 0, 1);
            state.BeginOpen();
            int version = state.Version;
            // Warm the allocation counter and the complete open-popup workload before
            // measuring. Every subsequent batch must pass; no minimum or retry is used.
            bytes();
            int warmValid = CaptureStableOwnerBatch(state, chain, menuPath, restored, iterations);
            bytes();
            for (int batch = 0; batch < allocated.Length; batch++) {
                long before = bytes();
                valid[batch] = CaptureStableOwnerBatch(state, chain, menuPath, restored, iterations);
                allocated[batch] = bytes() - before;
                versions[batch] = state.Version;
                restoredCounts[batch] = restored.Count;
            }
            Assert(warmValid == iterations && state.Open, "The complete warmup must preserve the open popup and its valid owner.");
            for (int batch = 0; batch < allocated.Length; batch++)
                Assert(valid[batch] == iterations && versions[batch] == version && restoredCounts[batch] == chain.Count && allocated[batch] == 0,
                    "Stable popup-owner capture must preserve its generation and reuse bounded ancestor storage in every batch; measured " +
                    allocated[0] + ", " + allocated[1] + ", " + allocated[2] + " bytes; batch " + batch + ": valid=" + valid[batch] +
                    ", version=" + versions[batch] + "/" + version + ", restored=" + restoredCounts[batch] + "/" + chain.Count + ".");
        }

        private static int CaptureStableOwnerBatch(PopupOwnerState state, System.Collections.Generic.List<Widget> chain,
            System.Collections.Generic.List<Widget> menuPath, System.Collections.Generic.List<Widget> restored, int iterations) {
            int valid = 0;
            for (int i = 0; i < iterations; i++) {
                if (state.Capture(chain, true) && state.CapturePopupPath(menuPath, 0, 1) && state.IsEnabled) valid++;
                restored.Clear(); state.AppendAncestorsTo(restored);
            }
            return valid;
        }

        private static void PopupInputOwnershipDoesNotDisableBackgroundAppearance() {
            foreach (UnityEngine.EventType type in Enum.GetValues(typeof(UnityEngine.EventType))) {
                Assert(GuiEventPolicy.ControlEnabled(true, true, true, type) == (type == UnityEngine.EventType.Repaint),
                    "An overlay preserves background enabled appearance only during Repaint and blocks every input event.");
                Assert(!GuiEventPolicy.ControlEnabled(false, true, true, type) && !GuiEventPolicy.ControlEnabled(true, false, true, type),
                    "Repaint must never re-enable a disabled widget or disabled ancestor.");
                Assert(GuiEventPolicy.ControlEnabled(true, true, false, type), "An unobstructed enabled widget keeps ordinary event processing.");
                Assert(!GuiEventPolicy.ControlEnabled(false, true, false, type) && !GuiEventPolicy.ControlEnabled(true, false, false, type),
                    "Ordinary event processing also respects both levels of enabled state.");
            }
        }

        private static void DropdownFitsOffsetAndSmallViewports() {
            MuiRect[] viewports = { new MuiRect(0f, 0f, 1280f, 720f), new MuiRect(100f, 60f, 360f, 240f), new MuiRect(0f, 0f, 24f, 28f) };
            foreach (MuiRect viewport in viewports) {
                MuiRect anchor = new MuiRect(viewport.Right - 80f, viewport.Bottom - 32f, 200f, 32f);
                PopupLayout layout = PopupGeometry.Dropdown(anchor, viewport, 50, 8, 32f, true);
                Assert(Contains(viewport, layout.Bounds), "Dropdown placement must stay inside its actual viewport, including nonzero origins.");
                Assert(Contains(layout.Bounds, layout.FilterRect), "A search filter must remain inside even a very short popup.");
                Assert(Contains(layout.Bounds, layout.ScrollViewport), "Clamping a popup must recompute its content viewport.");
                Assert(Contains(layout.ScrollViewport, layout.ContentViewport), "The reserved scroll gutter must never widen the content viewport.");
            }
        }

        private static void DropdownKeepsFilterSeparateAndLastRowReachable() {
            PopupLayout layout = PopupGeometry.Dropdown(new MuiRect(20f, 30f, 240f, 32f), new MuiRect(0f, 0f, 800f, 600f), 30, 6, 32f, true);
            Assert(layout.Scrollable && layout.FilterRect.Bottom <= layout.ContentViewport.Y, "The filter must not scroll or overlap option rows.");
            float end = layout.ClampScroll(float.MaxValue);
            MuiRect last = layout.Row(29, end);
            Assert(Near(last.Bottom, layout.ContentViewport.Bottom), "The final option must be fully reachable at maximum scroll.");
            Assert(Near(layout.ClampScroll(-500f), 0f), "A negative offset must clamp to the first row.");
            Assert(layout.ContentViewport.Width + ScrollbarGeometry.GutterSize == layout.ScrollViewport.Width, "Dropdown text must reserve the native scrollbar gutter.");
        }

        private static void DropdownVirtualizesLongLists() {
            string[] values = new string[10000];
            for (int i = 0; i < values.Length; i++) values[i] = "Option " + i;
            PopupLayout layout = PopupGeometry.Dropdown(new MuiRect(0f, 0f, 200f, 32f), new MuiRect(0f, 0f, 800f, 600f), values.Length, 6, 32f, false);
            DrawList drawing = new DrawList();
            PopupPresentation.DrawDropdown(drawing, ThemePalettes.MetroLight(), layout, values, 5000, -1, 5000f * 32f);
            int texts = 0, pushes = 0, pops = 0;
            foreach (DrawCommand command in drawing.Commands) {
                if (command.Type == DrawCommandType.Text) texts++;
                if (command.Type == DrawCommandType.PushClip) pushes++;
                if (command.Type == DrawCommandType.PopClip) pops++;
            }
            Assert(texts <= 10 && texts >= 6, "An expanded dropdown must paint only visible rows and a bounded overscan.");
            Assert(pushes == 1 && pops == 1, "Option rows need a balanced clip independent of filter and scroll chrome.");
            Assert(HasText(drawing, "Option 5000"), "Virtualization must preserve the active visible option.");
            Assert(!HasText(drawing, "Option 0"), "Rows outside the viewport must not produce draw commands.");
        }

        private static void EmptyDropdownHasAnOperationalStatus() {
            PopupLayout layout = PopupGeometry.Dropdown(new MuiRect(0f, 0f, 220f, 32f), new MuiRect(0f, 0f, 800f, 600f), 0, 8, 32f, true);
            DrawList drawing = new DrawList();
            PopupPresentation.DrawDropdown(drawing, ThemePalettes.MetroLight(), layout, new string[0], -1, -1, 0f, "unmatched", "Search");
            Assert(HasText(drawing, "unmatched") && HasText(drawing, "No results"), "An empty search must retain its filter and communicate the result state.");
            Assert(!layout.Scrollable && layout.ContentHeight == layout.RowHeight, "An empty result needs one status row rather than a spurious scrollbar.");
        }

        private static void MenuMeasuresTextAndSeparatesShortcutColumns() {
            Menu menu = new Menu("File");
            MenuItem item = new MenuItem("长文件名称") { Shortcut = "Ctrl+Shift+S", Checked = true };
            menu.Add(item);
            Menu submenu = new Menu("Export");
            menu.Add(submenu);
            Func<string, float> measure = delegate(string value) { return value.Length * 17f; };
            PopupLayout layout = PopupGeometry.Menu(new MuiRect(0f, 0f, 44f, 32f), new MuiRect(0f, 0f, 1000f, 800f), menu, false, 32f, measure);
            MuiRect row = PopupGeometry.MenuRow(layout, menu, 0, false);
            float shortcut = measure(item.Shortcut) + 16f;
            MuiRect label = PopupGeometry.MenuTextBounds(row, shortcut, false);
            MuiRect shortcutRect = PopupGeometry.MenuShortcutBounds(row, shortcut);
            Assert(label.Width >= measure(item.Text), "Menu width must use actual measured title and shortcut advances.");
            Assert(label.Right <= shortcutRect.X, "The action label must not overlap its shortcut column.");
            DrawList drawing = new DrawList();
            PopupPresentation.DrawMenu(drawing, ThemePalettes.MetroLight(), layout, menu, false, measureText: measure);
            Assert(HasText(drawing, item.Text) && HasText(drawing, item.Shortcut) && HasText(drawing, submenu.Title), "Menu chrome must preserve the actual accessible labels separately from icons.");
            int lineCount = 0;
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Line) lineCount++;
            Assert(lineCount >= 4, "Checked rows and submenu affordances must use real icon geometry.");
        }

        private static void DisabledMenuItemsIgnoreHoverAndKeepDisabledText() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Menu menu = new Menu("Edit");
            MenuItem disabled = new MenuItem("Paste") { Enabled = false, Shortcut = "Ctrl+V", Checked = true };
            menu.Add(disabled);
            PopupLayout layout = PopupGeometry.Menu(new MuiRect(0f, 0f, 44f, 32f), new MuiRect(0f, 0f, 800f, 600f), menu, false, 32f, null);
            DrawList drawing = new DrawList();
            PopupPresentation.DrawMenu(drawing, theme, layout, menu, false, 0);
            foreach (DrawCommand command in drawing.Commands) {
                if (command.Type == DrawCommandType.Text || command.Type == DrawCommandType.Line)
                    Assert(Equal(command.Color, theme.TextDisabled), "Disabled action labels, shortcuts and checks must remain disabled under the pointer.");
                Assert(!Equal(command.Color, theme.ButtonHoverBg), "Disabled rows must not paint pointer hover feedback.");
            }
            menu.Enabled = false;
            disabled.Enabled = true;
            drawing.Clear();
            PopupPresentation.DrawMenu(drawing, theme, layout, menu, false, 0);
            Assert(TextHasColor(drawing, "Paste", theme.TextDisabled), "A disabled menu must propagate its state to enabled child actions.");
        }

        private static void LongMenuScrollMakesItsLastActionReachable() {
            Menu menu = new Menu("Commands");
            for (int i = 0; i < 100; i++) menu.Add(new MenuItem("Action " + i));
            PopupLayout layout = PopupGeometry.Menu(new MuiRect(250f, 220f, 70f, 32f), new MuiRect(20f, 10f, 400f, 300f), menu, true, 32f, null);
            Assert(layout.Scrollable && layout.Bounds.Bottom <= 310f && layout.Bounds.Right <= 420f, "Oversized menus need bounded placement and a scrollable viewport.");
            float bottom = layout.ClampScroll(float.MaxValue);
            MuiRect last = PopupGeometry.MenuRow(layout, menu, 99, true, bottom);
            Assert(Near(last.Bottom, layout.ContentViewport.Bottom), "The last menu action must fit in view after scrolling, including the Back row.");
            DrawList drawing = new DrawList();
            PopupPresentation.DrawMenu(drawing, ThemePalettes.MetroLight(), layout, menu, true, 99, false, bottom);
            Assert(HasText(drawing, "Action 99") && !HasText(drawing, "Action 0"), "A menu at maximum scroll must paint its last command, not its hidden first command.");
        }

        private static void ScrollbarThumbsRemainBoundedAtBothEnds() {
            MuiRect vertical = new MuiRect(100f, 40f, 16f, 200f);
            MuiRect horizontal = new MuiRect(40f, 100f, 200f, 16f);
            float[] offsets = { -100f, 0f, 350f, 800f, float.MaxValue };
            foreach (float offset in offsets) {
                ScrollbarLayout v = ScrollbarGeometry.Vertical(vertical, 1000f, 200f, offset);
                ScrollbarLayout h = ScrollbarGeometry.Horizontal(horizontal, 1000f, 200f, offset);
                Assert(v.Visible && h.Visible && Contains(v.Track, v.Thumb) && Contains(h.Track, h.Thumb), "Scroll thumb geometry must remain within its track under extreme offsets.");
                Assert(Near(v.Thumb.Height, h.Thumb.Width), "Horizontal and vertical scrollbars must use the same range proportion.");
            }
            ScrollbarLayout start = ScrollbarGeometry.Vertical(vertical, 1000f, 200f, 0f);
            ScrollbarLayout end = ScrollbarGeometry.Vertical(vertical, 1000f, 200f, float.MaxValue);
            Assert(start.Thumb.Y == start.Track.Y + 2f && end.Thumb.Bottom == end.Track.Bottom - 2f, "Scroll endpoints must align with the actual padded native track.");
            Assert(!ScrollbarGeometry.Vertical(vertical, 100f, 200f, 0f).Visible, "Content that fits must not show a scrollbar.");
            Assert(!ScrollbarGeometry.Vertical(new MuiRect(0f, 0f, 2f, 2f), 100f, 20f, 1f).Visible, "A collapsed scrollbar must not create negative thumb extents.");
        }

        private static void NativePopupGeometryDoesNotAllocateAfterWarmup() {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return;
            Func<long> bytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            Menu menu = new Menu("File") { new MenuItem("Save") { Shortcut = "Ctrl+S" }, new MenuItem("Close") };
            MuiRect anchor = new MuiRect(10f, 20f, 200f, 32f), viewport = new MuiRect(0f, 0f, 800f, 600f);
            for (int i = 0; i < 10; i++) PopupGeometry.Menu(anchor, viewport, menu, false, 32f, null);
            long before = bytes();
            float total = 0f;
            for (int i = 0; i < 10000; i++) {
                PopupLayout dropdown = PopupGeometry.Dropdown(anchor, viewport, 100, 8, 32f, true);
                PopupLayout popup = PopupGeometry.Menu(anchor, viewport, menu, false, 32f, null);
                total += dropdown.ClampScroll(i) + popup.Bounds.Width;
                total += ScrollbarGeometry.Vertical(new MuiRect(0f, 0f, 16f, 200f), 1000f, 200f, i).Thumb.Y;
            }
            Assert(bytes() - before == 0, "Native popup placement and scroll geometry must not allocate on every GUI event.");
            Assert(total > 0f, "Geometry results must be evaluated during allocation verification.");
        }

        private static void TooltipReservesMeasuredTextHeightAndPadding() {
            float measuredWidth = 0f;
            MuiRect bounds = PopupGeometry.Tooltip("Right-click for actions", new MuiVec2(100f, 100f), new MuiRect(0f, 0f, 800f, 600f),
                delegate(string text) { return 160f; }, delegate(string text, float width) { measuredWidth = width; return 26f; });
            MuiRect content = PopupGeometry.TooltipTextBounds(bounds);
            Assert(content.Height >= 26f && Near(content.Width, measuredWidth), "A tooltip must reserve its measured text height in addition to both vertical padding edges.");
            Assert(bounds.Height >= 44f, "A 26px text line plus 18px padding cannot fit in the legacy 24px tooltip.");
            DrawList drawing = new DrawList();
            PopupPresentation.DrawTooltip(drawing, ThemePalettes.MetroLight(), bounds, "Right-click for actions");
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Text)
                Assert(command.TextFormat.WordWrap && command.TextFormat.VerticalAlignment == TextVerticalAlignment.Top && Contains(bounds, command.Rect),
                    "Tooltip presentation must use the measured padded text region with top-aligned wrapping.");
        }

        private static void TooltipWrapsAndFlipsWithinTheViewport() {
            MuiRect viewport = new MuiRect(50f, 40f, 400f, 300f);
            MuiVec2 pointer = new MuiVec2(viewport.Right - 2f, viewport.Bottom - 2f);
            MuiRect bounds = PopupGeometry.Tooltip("A long tooltip", pointer, viewport,
                delegate(string text) { return 600f; }, delegate(string text, float width) { return (float)Math.Ceiling(600f / width) * 20f; });
            Assert(Contains(viewport, bounds) && bounds.Bottom <= pointer.Y, "A tooltip near the lower-right edge must move above its pointer and stay within the viewport.");
            Assert(bounds.Width <= 320f && PopupGeometry.TooltipTextBounds(bounds).Height >= 60f, "A width-limited tooltip must grow to fit its wrapped lines.");
            MuiRect tiny = new MuiRect(10f, 20f, 1f, 1f);
            Assert(Contains(tiny, PopupGeometry.Tooltip("Text", new MuiVec2(10f, 20f), tiny, null, null)), "A collapsed viewport must not create an off-screen tooltip rectangle.");
        }

        private static void OpenPopupHostsRemainVisiblyAssociatedWithTheirOverlay() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(theme);
            ComboBox<string> combo = new ComboBox<string>(string.Empty, "Normal", new[] { "Normal", "Compact" }, delegate(string value) { return value; }, null);
            LayoutNode comboNode = new LayoutEngine(metrics).Layout(combo, new MuiRect(0f, 0f, 240f, 32f), "combo");
            DrawList drawing = new WidgetPainter(theme).Paint(comboNode, new WidgetPaintState { OpenPopupPath = "combo" });
            bool activeBorder = false;
            foreach (DrawCommand command in drawing.Commands)
                if (command.Type == DrawCommandType.StrokeRect && command.Thickness == 2f && Equal(command.Color, theme.Accent)) activeBorder = true;
            Assert(activeBorder, "An expanded combo must keep its open field border visible behind the dropdown.");
            MenuBar bar = new MenuBar();
            bar.Add(new Menu("File"));
            Menu edit = new Menu("Edit");
            bar.Add(edit);
            LayoutNode menuNode = new LayoutEngine(metrics).Layout(bar, new MuiRect(0f, 0f, 300f, 32f), "menu");
            WidgetPaintState state = new WidgetPaintState { OpenPopupPath = "menu", OpenMenuIndex = 1 };
            drawing = new WidgetPainter(theme).Paint(menuNode, state);
            int indicators = 0;
            foreach (DrawCommand command in drawing.Commands)
                if (command.Type == DrawCommandType.FillRect && Equal(command.Color, theme.Accent)) indicators++;
            Assert(indicators == 1, "Exactly the open menu header must receive the selected indicator.");
            edit.Enabled = false;
            drawing = new WidgetPainter(theme).Paint(menuNode, state);
            Assert(TextHasColor(drawing, "Edit", theme.TextDisabled), "A disabled menu header must retain disabled text even if it was previously open.");
            foreach (DrawCommand command in drawing.Commands)
                Assert(!Equal(command.Color, theme.Accent), "A disabled popup host cannot retain an active open indicator.");
        }

        private static bool HasText(DrawList drawing, string text) {
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Text && command.Text == text) return true;
            return false;
        }

        private static bool TextHasColor(DrawList drawing, string text, MuiColor color) {
            foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Text && command.Text == text && Equal(command.Color, color)) return true;
            return false;
        }

        private static bool Equal(MuiColor a, MuiColor b) { return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A; }
        private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.001f; }
        private static bool Contains(MuiRect outer, MuiRect inner) {
            return inner.Width >= 0f && inner.Height >= 0f && inner.X >= outer.X - 0.001f && inner.Y >= outer.Y - 0.001f && inner.Right <= outer.Right + 0.001f && inner.Bottom <= outer.Bottom + 0.001f;
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
