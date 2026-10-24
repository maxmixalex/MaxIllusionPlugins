using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class ImguiRenderer : IDisposable {
        private static readonly GUIContent TempContent = new GUIContent();
        private readonly DragController dragController = new DragController();
        private LayoutEngine layoutEngine = new LayoutEngine();
        private float controlRowHeight = 32f;
        private LayoutCache layoutCache;
        private UnityPainter unityPainter;
        private MaximumTheme layoutTheme;
        private int contentRevision;
        private GUIStyle linkLabel;
        private GUIStyle hoveredLinkLabel;
        private GUIStyle disabledLabel;
        private GUIStyle disabledCenterLabel;
        private GUIStyle disabledRightLabel;
        private GUIStyle disabledButtonText;
        private GUIStyle searchFieldStyle;
        private GUIStyle helpTextStyle;
        private GUIStyle disabledHelpTextStyle;
        private GUIStyle popupShortcutStyle;
        private readonly Func<string, float> popupTextMeasure;
        private readonly Func<string, float, float> tooltipTextHeightMeasure;
        private Texture2D selectionDisc;
        private readonly Dictionary<GUIStyle, GUIStyle> wrappedLabels = new Dictionary<GUIStyle, GUIStyle>();

        private RenderContext currentContext;
        private Window currentWindow;
        private string currentWindowStateId;
        private Vector2 currentWindowContentOffset;
        private bool mouseDownOnMaximumUi;
        private int dialogDepth;
        private Dialog currentDialog;
        private Widget currentWidget;
        private bool popupBlocksInput;
        private int ownedHotControl;
        private DragState activeDrag;
        private WindowState resizingWindow;
        private readonly GuiCoordinates coordinates = new GuiCoordinates();
        private struct ScrollInputFrame {
            public MuiRect VisibleBounds;
            public MuiVec2 Position;
            public MuiVec2 Maximum;
            public bool Enabled;
        }
        private ScrollInputFrame[] scrollInputs = new ScrollInputFrame[4];
        private int scrollDepth;
        private int popupDepth;
        private TabsState activeTabs;
        private readonly List<Widget> ownerAncestors = new List<Widget>(8);
        private bool modelEnabled = true;
        private bool modelRootEnabled = true;
        private PopupOwnerState currentPopupOwner;
        private readonly TextFocusSession textFocus = new TextFocusSession();

        internal bool HasPointerCapture {
            get { return ownedHotControl != 0 && GUIUtility.hotControl == ownedHotControl; }
        }

        public ImguiRenderer() {
            layoutCache = new LayoutCache(layoutEngine);
            popupTextMeasure = MeasurePopupText;
            tooltipTextHeightMeasure = MeasureTooltipTextHeight;
        }

        public void InvalidateLayout() {
            layoutCache.Invalidate();
            unchecked { contentRevision++; }
        }

        public void Dispose() {
            ResetPendingTextFocus();
            CancelInput();
            layoutCache.Invalidate();
            wrappedLabels.Clear();
            if (unityPainter != null) unityPainter.Dispose();
            unityPainter = null;
            // An unpainted renderer owns no Unity objects; disposing a retained
            // mount must not call Unity's native equality operator in that case.
            if (!ReferenceEquals(selectionDisc, null)) UnityEngine.Object.Destroy(selectionDisc);
            selectionDisc = null;
        }

        internal void CancelInput() {
            if (ownedHotControl != 0 && GUIUtility.hotControl == ownedHotControl) GUIUtility.hotControl = 0;
            ownedHotControl = 0;
            if (activeDrag != null) activeDrag.Active = false;
            if (resizingWindow != null) resizingWindow.Resizing = false;
            if (activeTabs != null) activeTabs.ClearDrag();
            activeDrag = null;
            resizingWindow = null;
            activeTabs = null;
            mouseDownOnMaximumUi = false;
        }

        internal void ResetPendingTextFocus() {
            textFocus.Reset();
        }

        private void CommitFocusedText() {
            // The session already guards its original popup/source. Using the
            // command currently being pressed as its owner would be incorrect.
            MaximumUIRuntime.EnqueueCallback(textFocus.TakeBlur());
        }

        private void CapturePointer(int control) {
            GUIUtility.hotControl = control;
            ownedHotControl = control;
        }

        private void ReleasePointer(int control) {
            if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            if (ownedHotControl == control) ownedHotControl = 0;
        }

        private LayoutNode Layout(Widget widget, MuiRect viewport, string path, RenderContext context) {
            return context.CacheLayout ? layoutCache.Layout(widget, viewport, path) : layoutEngine.Layout(widget, viewport, path);
        }

        private void Paint(DrawList drawList) {
            if (unityPainter == null) unityPainter = new UnityPainter();
            unityPainter.Theme = layoutTheme;
            unityPainter.Paint(drawList);
        }

        public void Render(Widget widget, RenderContext context) {
            if (widget == null || context == null) { CancelInput(); return; }

            RenderContext oldContext = currentContext;
            currentContext = context;
            Color oldGuiColor = GUI.color;
            Color oldContentColor = GUI.contentColor;
            Color oldBackgroundColor = GUI.backgroundColor;
            bool oldGuiEnabled = GUI.enabled;
            bool oldModelEnabled = modelEnabled;
            bool oldModelRootEnabled = modelRootEnabled;
            PopupOwnerState oldPopupOwner = currentPopupOwner;
            int oldOwnerCount = ownerAncestors.Count;
            modelEnabled = modelRootEnabled = oldGuiEnabled;
            currentPopupOwner = null;
            Matrix4x4 oldMatrix = GUI.matrix;
            int previousHotControl = GUIUtility.hotControl;
            Event guiEvent = Event.current;
            try {
                coordinates.Reset(Screen.width, Screen.height);
                scrollDepth = 0;
                if (!context.OwnsKeyboardInput) CommitFocusedText();
                popupBlocksInput = PreparePopupInput(context);
                if (context.ActiveContextMenu != null) context.ActiveContextMenu.Seen = false;
                if (context.ActiveMenuBar != null) context.ActiveMenuBar.Seen = false;
                if (layoutTheme != context.Theme) {
                    layoutTheme = context.Theme;
                    LayoutMetrics metrics = layoutTheme == null ? new LayoutMetrics() : layoutTheme.CreateLayoutMetrics();
                    controlRowHeight = metrics.RowHeight;
                    layoutEngine = new LayoutEngine(metrics);
                    layoutCache = new LayoutCache(layoutEngine);
                    wrappedLabels.Clear();
                    linkLabel = new GUIStyle(context.Theme.Label);
                    hoveredLinkLabel = new GUIStyle(context.Theme.Label);
                    MaximumTheme.SetTextColor(linkLabel, ToUnityColor(context.Theme.Tokens.Accent));
                    MaximumTheme.SetTextColor(hoveredLinkLabel, ToUnityColor(context.Theme.Tokens.AccentHover));
                    disabledLabel = new GUIStyle(context.Theme.Label);
                    MaximumTheme.SetTextColor(disabledLabel, ToUnityColor(context.Theme.Tokens.TextDisabled));
                    helpTextStyle = new GUIStyle(context.Theme.Label) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset() };
                    disabledHelpTextStyle = new GUIStyle(helpTextStyle);
                    MaximumTheme.SetTextColor(disabledHelpTextStyle, ToUnityColor(context.Theme.Tokens.TextDisabled));
                    disabledCenterLabel = new GUIStyle(disabledLabel) { alignment = TextAnchor.MiddleCenter };
                    disabledRightLabel = new GUIStyle(disabledLabel) { alignment = TextAnchor.MiddleRight };
                    disabledButtonText = new GUIStyle(context.Theme.Button);
                    MaximumTheme.SetBackground(disabledButtonText, null);
                    MaximumTheme.SetOnBackground(disabledButtonText, null);
                    MaximumTheme.SetTextColor(disabledButtonText, ToUnityColor(context.Theme.Tokens.TextDisabled));
                    searchFieldStyle = new GUIStyle(context.Theme.TextField);
                    searchFieldStyle.padding = new RectOffset(context.Theme.TextField.padding.left, 32,
                        context.Theme.TextField.padding.top, context.Theme.TextField.padding.bottom);
                    popupShortcutStyle = new GUIStyle(context.Theme.MutedLabel) { alignment = TextAnchor.MiddleRight };
                }
                for (int i = 0; i < context.ComboBoxStates.Count; i++) context.ComboBoxStates[i].HasButtonRect = false;
                if (context.Query != null) {
                    context.Query.BeginFrame();
                    context.Query.RecordInput(ToMuiInputEvent(Event.current));
                }
                if (context.Navigation != null && !context.SuppressInput) context.Navigation.BeginFrame();
                context.Overlay.Clear();
                context.InputDialog = null;
                bool externalModalOpen = context.IsModalOpen;
                string rootPath = string.IsNullOrEmpty(context.Path) ? "0" : context.Path + ".0";
                context.IsModalOpen = externalModalOpen || WidgetVisibility.ContainsRootDialog(widget, context, rootPath) || (context.Dialogs != null && context.Dialogs.HasOpenDialogs);
                MaximumUIRuntime.IsModalOpen |= context.IsModalOpen;
                RenderWidget(widget, context, "0");

                if (context.ActiveContextMenu != null && !context.ActiveContextMenu.Seen) context.CloseMenus();
                if (context.ActiveMenuBar != null && !context.ActiveMenuBar.Seen) context.CloseMenus();

                if (!context.SuppressDialogs) RegisterServiceDialogs(context);
                RegisterDragDropPreview(context);
                // Register and paint dialogs first so their combo boxes have current
                // coordinates and only the top dialog can own popup input.
                context.Overlay.Render(context);
                popupBlocksInput = false;

                for (int i = 0; i < context.ComboBoxStates.Count; i++) {
                    ComboBoxState state = context.ComboBoxStates[i];
                    if (state != null && (!state.HasButtonRect || !state.PopupOwner.IsEnabled || (context.IsModalOpen &&
                        (context.InputDialog == null || !ReferenceEquals(state.ModalDialog, context.InputDialog))))) {
                        if (state.Dropdown != null) state.Dropdown.Hide();
                        state.Open = false;
                        state.PopupOwner.Invalidate();
                    }
                    if (state == null || state.Dropdown == null || !state.Dropdown.Show || !state.HasButtonRect || string.IsNullOrEmpty(state.WindowStateId)) continue;

                    WindowState windowState = context.StateStore.GetOrCreate<WindowState>(state.WindowStateId);
                    if (state.Searchable) {
                        state.Dropdown.Set(windowState.Rect, state.ButtonRect, state.Items, state.Callback, state.MaxVisibleItems, state.FilterText,
                            state.FilterChanged, state.FilterPlaceholder);
                    } else {
                        state.Dropdown.Set(windowState.Rect, state.ButtonRect, state.Items, state.Callback, state.MaxVisibleItems);
                    }
                }

                for (int i = 0; i < context.Dropdowns.Count; i++) {
                    DropdownOverlay dropdown = context.Dropdowns[i];
                    bool enabled = GUI.enabled;
                    GUI.enabled = GuiEventPolicy.ControlEnabled(enabled, true, context.SuppressInput, Event.current == null ? EventType.Layout : Event.current.type);
                    try { dropdown.Draw(); }
                    finally { GUI.enabled = enabled; }
                    RegisterInputRegion(dropdown.Rect, dropdown.Show, true);
                }

                for (int i = 0; i < context.ComboBoxStates.Count; i++) {
                    ComboBoxState state = context.ComboBoxStates[i];
                    state.Open = state.Dropdown != null && state.Dropdown.Show;
                }
                context.PruneClosedComboBoxes();

                CompleteDragDropFrame(context);
                if (context.Navigation != null && !context.SuppressInput) context.Navigation.EndFrame();
                CaptureInputIfNeeded();
                UpdateQueryCapture(context);
            } finally {
                if (!context.SuppressInput && previousHotControl == 0 && GUIUtility.hotControl != 0) ownedHotControl = GUIUtility.hotControl;
                if (guiEvent != null && GuiEventPolicy.ShouldReleasePointer(guiEvent.rawType, guiEvent.button)) {
                    CancelInput();
                    context.DragDrop.Clear();
                }
                popupBlocksInput = false;
                GUI.color = oldGuiColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
                GUI.enabled = oldGuiEnabled;
                modelEnabled = oldModelEnabled;
                modelRootEnabled = oldModelRootEnabled;
                currentPopupOwner = oldPopupOwner;
                ownerAncestors.RemoveRange(oldOwnerCount, ownerAncestors.Count - oldOwnerCount);
                GUI.matrix = oldMatrix;
                currentContext = oldContext;
            }
        }

        private void RenderWidget(Widget widget, RenderContext context, string path) {
            if (widget == null || widget is EmptyWidget || (widget is Dialog && context.SuppressDialogs)) return;

            string oldPath = context.Path;
            context.Path = oldPath == null || oldPath.Length == 0 ? path : oldPath + "." + path;

            bool oldEnabled = GUI.enabled;
            bool oldModelEnabled = modelEnabled;
            modelEnabled = modelEnabled && widget.Enabled;
            ownerAncestors.Add(widget);
            bool blockInput = context.SuppressInput || popupBlocksInput || (IsInputBlockedByDialog() && !(widget is Dialog) && !HasDialogWidget(widget));
            GUI.enabled = GuiEventPolicy.ControlEnabled(oldEnabled, widget.Enabled, blockInput, Event.current == null ? EventType.Layout : Event.current.type);
            try {
                if (widget is Window) {
                    RenderWindow((Window)widget, context);
                } else if (widget is Dialog) {
                    RenderDialog((Dialog)widget, context);
                } else {
                    LayoutNode root = Layout(widget, new MuiRect(0f, 0f, Screen.width, Screen.height), context.Path, context);
                    RenderLayoutNode(root, context, Vector2.zero);
                }
            } finally {
                GUI.enabled = oldEnabled;
                modelEnabled = oldModelEnabled;
                ownerAncestors.RemoveAt(ownerAncestors.Count - 1);
                context.Path = oldPath;
            }
        }

        private void RenderWindow(Window widget, RenderContext context) {
            if (!widget.Visible) return;
            float titleHeight = WindowTitleHeight(widget, context);

            string id = context.StateId(widget, "window");
            WindowState state = context.StateStore.GetOrCreate<WindowState>(id);
            if (!state.Initialized) {
                state.Rect = new Rect(80f, 60f, widget.Width, widget.Height);
                state.ExpandedHeight = widget.Height;
                state.Initialized = true;
            }

            state.Rect.width = Mathf.Max(state.Rect.width, widget.MinWidth);
            state.Rect.height = state.Folded ? titleHeight : Mathf.Max(state.Rect.height, widget.MinHeight);
            state.Rect = ClipWindowPosition(state.Rect, !state.Folded, widget.MinWidth, widget.MinHeight, titleHeight);

            RenderContext oldContext = currentContext;
            Window oldWindow = currentWindow;
            string oldWindowStateId = currentWindowStateId;
            Vector2 oldWindowContentOffset = currentWindowContentOffset;
            string windowPath = context.Path;
            currentContext = context;
            currentWindow = widget;
            currentWindowStateId = id;
            currentWindowContentOffset = Vector2.zero;
            try {
                try {
                    DrawWindowFrame(context, widget, state);
                    string oldPath = context.Path;
                    context.Path = windowPath;
                    try {
                        DrawWindowContents(context, widget);
                    } catch (Exception ex) {
                        Debug.LogException(ex);
                        DrawWindowError(context, state, ex);
                    } finally {
                        context.Path = oldPath;
                    }
                } catch (Exception ex) {
                    Debug.LogException(ex);
                    DrawWindowError(context, state, ex);
                }
                if (widget.Resizable && !state.Folded) DoWindowResize(context, widget, state);
                state.Rect = ClipWindowPosition(state.Rect, !state.Folded, widget.MinWidth, widget.MinHeight, titleHeight);
                RegisterInputRegion(state.Rect, true);
                RecordWindowQuery(context, widget, state);
            } finally {
                currentContext = oldContext;
                currentWindow = oldWindow;
                currentWindowStateId = oldWindowStateId;
                currentWindowContentOffset = oldWindowContentOffset;
            }
        }

        private void DrawWindowFrame(RenderContext context, Window widget, WindowState state) {
            GUIStyle style = state.Folded ? context.Theme.FoldedWindow : context.Theme.Window;
            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    DrawStyle(style, state.Rect, GetContent(widget.Title), false, false, false, false);
                }
            } finally {
                GUI.color = oldColor;
            }

            float buttonSize = WindowTitleHeight(widget, context);
            float closeX = state.Rect.xMax - buttonSize;
            float foldX = widget.Closable ? closeX - buttonSize : closeX;
            bool inputBlocked = IsInputBlockedByDialog();

            if (widget.Foldable && DoIconButton(new Rect(foldX, state.Rect.y, buttonSize, buttonSize), state.Folded ? Mdl2Icon.ChevronDown : Mdl2Icon.Remove, context.Theme.QuietButton, false) && !inputBlocked) {
                if (!state.Folded) state.ExpandedHeight = state.Rect.height;
                state.Folded = !state.Folded;
                if (!state.Folded) state.Rect.height = state.ExpandedHeight > 0f ? state.ExpandedHeight : widget.Height;
            }

            if (widget.Closable && DoIconButton(new Rect(closeX, state.Rect.y, buttonSize, buttonSize), Mdl2Icon.Close, context.Theme.QuietButton, false) && !inputBlocked) {
                EnqueueCallback(delegate {
                    widget.Visible = false;
                    if (widget.OnVisibleChange != null) widget.OnVisibleChange(false);
                });
            }

            if (!inputBlocked) {
                Rect dragRect = new Rect(state.Rect.x, state.Rect.y, Mathf.Max(0f, foldX - state.Rect.x), buttonSize);
                DoDrag(state, StableHash(context.StateId(widget, "window-drag")), dragRect);
            }
        }

        private void DrawWindowContents(RenderContext context, Window widget) {
            string id = context.StateId(widget, "window");
            WindowState state = context.StateStore.GetOrCreate<WindowState>(id);
            if (state.Folded) return;

            RectOffset padding = context.Theme.WindowContentPadding;
            Vector2 oldContentOffset = currentWindowContentOffset;
            currentWindowContentOffset = new Vector2(padding.left, padding.top);
            try {
                LayoutNode root = Layout(widget, ToMuiRect(state.Rect), context.Path, context);
                RenderLayoutChildren(root, context, Vector2.zero);
            } finally {
                currentWindowContentOffset = oldContentOffset;
            }
        }

        private static void DrawWindowError(RenderContext context, WindowState state, Exception ex) {
            RectOffset padding = context.Theme.WindowContentPadding;
            Rect rect = new Rect(
                state.Rect.x + padding.left,
                state.Rect.y + padding.top,
                Mathf.Max(1f, state.Rect.width - padding.left - padding.right),
                Mathf.Max(1f, state.Rect.height - padding.top - padding.bottom));
            DrawStyle(context.Theme.Label, rect, GetContent("MaximumUI render error: " + ex.GetType().Name), false, false, false, false);
        }

        private void RenderLayoutChildren(LayoutNode node, RenderContext context, Vector2 offset) {
            if (node == null) return;

            for (int i = 0; i < node.Children.Count; i++) {
                RenderLayoutNode(node.Children[i], context, offset);
            }
        }

        private void RenderLayoutNode(LayoutNode node, RenderContext context, Vector2 offset) {
            if (node == null || node.Widget == null || node.Widget is EmptyWidget) return;

            Widget widget = node.Widget;
            if (widget is Dialog && context.SuppressDialogs) return;
            string oldPath = context.Path;
            context.Path = node.Path;
            Widget oldWidget = currentWidget;
            currentWidget = widget;

            bool oldEnabled = GUI.enabled;
            bool oldModelEnabled = modelEnabled;
            modelEnabled = modelEnabled && widget.Enabled;
            ownerAncestors.Add(widget);
            bool blockInput = context.SuppressInput || popupBlocksInput || (IsInputBlockedByDialog() && !(widget is Dialog) && !HasDialogWidget(widget));
            GUI.enabled = GuiEventPolicy.ControlEnabled(oldEnabled, widget.Enabled, blockInput, Event.current == null ? EventType.Layout : Event.current.type);
            try {
                RecordItemQuery(context, node, widget, offset);
                if (widget is Window) {
                    RenderWindow((Window)widget, context);
                } else if (widget is Panel) {
                    DrawStyle(context.Theme.Box, OffsetRect(node.Rect, offset), GUIContent.none, false, false, false, false);
                    RenderLayoutChildren(node, context, offset);
                    if (((Panel)widget).Selected)
                        DrawStrokeRect(OffsetRect(node.Rect, offset), ToUnityColor(GUI.enabled ? context.Theme.Tokens.Accent : context.Theme.Tokens.Border), 1f);
                } else if (widget is TableHeaderRow) {
                    DrawFilledRect(OffsetRect(node.Rect, offset), ToUnityColor(context.Theme.Tokens.SelectedBg));
                    RenderLayoutChildren(node, context, offset);
                } else if (widget is TableCell) {
                    TableCell cell = (TableCell)widget;
                    Rect cellRect = OffsetRect(node.Rect, offset);
                    if (cell.HasBackground) DrawFilledRect(cellRect, ToUnityColor(cell.Background));
                    if (cell.ShowBorder) DrawStrokeRect(cellRect, ToUnityColor(context.Theme.Tokens.Border), 1f);
                    RenderLayoutChildren(node, context, offset);
                } else if (widget is VStack || widget is HStack || widget is Grid || widget is TableRow || widget is Tab) {
                    RenderLayoutChildren(node, context, offset);
                } else if (widget is Table) {
                    RenderTableAt(node, (Table)widget, context, offset);
                } else if (widget is TwoPaneLayout) {
                    RenderTwoPaneLayoutAt(node, (TwoPaneLayout)widget, context, offset);
                } else if (widget is DragSource) {
                    RenderDragSourceAt(node, (DragSource)widget, context, offset);
                } else if (widget is DropTarget) {
                    RenderDropTargetAt(node, (DropTarget)widget, context, offset);
                } else if (widget is Label) {
                    RenderLabelAt(node, (Label)widget, context, offset);
                } else if (widget is BulletText) {
                    RenderBulletTextAt(node, (BulletText)widget, context, offset);
                } else if (widget is ValueLabel) {
                    RenderValueLabelAt(node, (ValueLabel)widget, context, offset);
                } else if (widget is TextLink) {
                    RenderTextLinkAt(node, (TextLink)widget, context, offset);
                } else if (widget is Button) {
                    RenderButtonAt(node, (Button)widget, context, offset);
                } else if (widget is SmallButton) {
                    RenderSmallButtonAt(node, (SmallButton)widget, context, offset);
                } else if (widget is ArrowButton) {
                    RenderArrowButtonAt(node, (ArrowButton)widget, context, offset);
                } else if (widget is Toggle) {
                    RenderToggleAt(node, (Toggle)widget, context, offset);
                } else if (widget is ToggleSwitch) {
                    RenderToggleSwitchAt(node, (ToggleSwitch)widget, context, offset);
                } else if (widget is Checkbox) {
                    RenderCheckboxAt(node, (Checkbox)widget, context, offset);
                } else if (widget is RadioButton) {
                    RenderRadioButtonAt(node, (RadioButton)widget, context, offset);
                } else if (widget is ISegmentedControlWidget) {
                    RenderSegmentedControlAt(node, (ISegmentedControlWidget)widget, context, offset);
                } else if (widget is Slider) {
                    RenderSliderAt(node, (Slider)widget, context, offset);
                } else if (widget is InputInt) {
                    RenderInputIntAt(node, (InputInt)widget, context, offset);
                } else if (widget is InputFloat) {
                    RenderInputFloatAt(node, (InputFloat)widget, context, offset);
                } else if (widget is InputDouble) {
                    RenderInputDoubleAt(node, (InputDouble)widget, context, offset);
                } else if (widget is DragInt) {
                    RenderDragIntAt(node, (DragInt)widget, context, offset);
                } else if (widget is DragFloat) {
                    RenderDragFloatAt(node, (DragFloat)widget, context, offset);
                } else if (widget is SliderInt) {
                    RenderSliderIntAt(node, (SliderInt)widget, context, offset);
                } else if (widget is SliderFloat) {
                    RenderSliderFloatAt(node, (SliderFloat)widget, context, offset);
                } else if (widget is SliderAngle) {
                    RenderSliderAngleAt(node, (SliderAngle)widget, context, offset);
                } else if (widget is ProgressBar) {
                    RenderProgressBarAt(node, (ProgressBar)widget, context, offset);
                } else if (widget is StatusBadge) {
                    RenderStatusBadgeAt(node, (StatusBadge)widget, context, offset);
                } else if (widget is LoadingIndicator) {
                    RenderLoadingIndicatorAt(node, (LoadingIndicator)widget, context, offset);
                } else if (widget is Image) {
                    RenderImageAt(node, (Image)widget, context, offset);
                } else if (widget is ImageButton) {
                    RenderImageButtonAt(node, (ImageButton)widget, context, offset);
                } else if (widget is TextField) {
                    RenderTextFieldAt(node, (TextField)widget, context, offset);
                } else if (widget is TextArea) {
                    RenderTextAreaAt(node, (TextArea)widget, context, offset);
                } else if (widget is PasswordField) {
                    RenderPasswordFieldAt(node, (PasswordField)widget, context, offset);
                } else if (widget is SearchBox) {
                    RenderSearchBoxAt(node, (SearchBox)widget, context, offset);
                } else if (widget is IListBoxWidget) {
                    RenderListBoxAt(node, (IListBoxWidget)widget, context, offset);
                } else if (widget is IDebugLinesWidget) {
                    RenderDebugLinesAt(node, (IDebugLinesWidget)widget, context, offset);
                } else if (widget is ThemeEditor) {
                    RenderThemeEditorAt(node, (ThemeEditor)widget, context, offset);
                } else if (widget is HelpBox) {
                    RenderHelpBoxAt(node, (HelpBox)widget, context, offset);
                } else if (widget is Separator) {
                    RenderSeparatorAt(node, context, offset);
                } else if (widget is SeparatorText) {
                    RenderSeparatorTextAt(node, (SeparatorText)widget, context, offset);
                } else if (widget is Space || widget is Spacer) {
                } else if (widget is ScrollView) {
                    RenderScrollViewAt(node, (ScrollView)widget, context, offset);
                } else if (widget is Foldout) {
                    RenderFoldoutAt(node, (Foldout)widget, context, offset);
                } else if (widget is CollapsingHeader) {
                    RenderCollapsingHeaderAt(node, (CollapsingHeader)widget, context, offset);
                } else if (widget is ClosableCollapsingHeader) {
                    RenderClosableCollapsingHeaderAt(node, (ClosableCollapsingHeader)widget, context, offset);
                } else if (widget is Tabs) {
                    RenderTabsAt(node, (Tabs)widget, context, offset);
                } else if (widget is MenuBar) {
                    RenderMenuBarAt(node, (MenuBar)widget, context, offset);
                } else if (widget is Selectable) {
                    RenderSelectableAt(node, (Selectable)widget, context, offset);
                } else if (widget is TreeNode) {
                    RenderTreeNodeAt(node, (TreeNode)widget, context, offset);
                } else if (widget is TreeLeaf) {
                    RenderTreeLeafAt(node, (TreeLeaf)widget, context, offset);
                } else if (widget is ColorButton) {
                    RenderColorButtonAt(node, (ColorButton)widget, context, offset);
                } else if (widget is ColorEdit3) {
                    RenderColorEdit3At(node, (ColorEdit3)widget, context, offset);
                } else if (widget is ColorEdit4) {
                    RenderColorEdit4At(node, (ColorEdit4)widget, context, offset);
                } else if (widget is ColorPicker3) {
                    RenderColorPicker3At(node, (ColorPicker3)widget, context, offset);
                } else if (widget is ColorPicker4) {
                    RenderColorPicker4At(node, (ColorPicker4)widget, context, offset);
                } else if (widget is ColorEdit) {
                    RenderColorEditAt(node, (ColorEdit)widget, context, offset);
                } else if (widget is VectorField) {
                    RenderVectorFieldAt(node, (VectorField)widget, context, offset);
                } else if (widget is PlotLines) {
                    RenderPlotLinesAt(node, (PlotLines)widget, context, offset);
                } else if (widget is PlotHistogram) {
                    RenderPlotHistogramAt(node, (PlotHistogram)widget, context, offset);
                } else if (widget is DrawCanvas) {
                    RenderDrawCanvasAt(node, (DrawCanvas)widget, context, offset);
                } else if (widget is DrawListWidget) {
                    RenderDrawListWidgetAt(node, (DrawListWidget)widget, context, offset);
                } else if (widget is VirtualList) {
                    RenderVirtualListAt(node, (VirtualList)widget, context, offset);
                } else if (widget is Dialog) {
                    RenderDialog((Dialog)widget, context);
                } else if (widget is IComboBoxWidget) {
                    RenderComboBoxAt(node, widget, (IComboBoxWidget)widget, context, offset);
                } else {
                    RenderLayoutChildren(node, context, offset);
                }

                RegisterWidgetOverlays(node, widget, context, offset);
                if (GUI.enabled && !IsInputBlockedByDialog() && context.OwnsKeyboardInput && context.Navigation != null && context.Navigation.IsKeyboardFocused(widget, node.Path) && IsNavigationFocusable(widget)) {
                    if (!(widget is TextField) && !(widget is TextArea) && !(widget is PasswordField) && !(widget is SearchBox)) {
                        Rect focusRect = OffsetRect(node.Rect, offset);
                        if (widget is Foldout || widget is CollapsingHeader || widget is ClosableCollapsingHeader || widget is TreeNode)
                            focusRect.height = Mathf.Min(focusRect.height, controlRowHeight);
                        DrawKeyboardFocus(focusRect, context.Theme.Tokens);
                    }
                }
            } finally {
                currentWidget = oldWidget;
                GUI.enabled = oldEnabled;
                modelEnabled = oldModelEnabled;
                ownerAncestors.RemoveAt(ownerAncestors.Count - 1);
                context.Path = oldPath;
            }
        }

        private void RenderLabelAt(LayoutNode node, Label widget, RenderContext context, Vector2 offset) {
            GUIStyle style = widget.Style as GUIStyle ?? (widget.Role == LabelRole.Heading ? context.Theme.Heading :
                widget.Role == LabelRole.Subheading ? context.Theme.Subheading :
                widget.Role == LabelRole.Caption ? context.Theme.Caption :
                widget.Role == LabelRole.Muted ? context.Theme.MutedLabel : context.Theme.Label);
            if (widget.WordWrap && !style.wordWrap) {
                GUIStyle wrapped;
                if (!wrappedLabels.TryGetValue(style, out wrapped)) {
                    wrapped = new GUIStyle(style) { wordWrap = true };
                    if (wrappedLabels.Count >= 64) wrappedLabels.Clear();
                    wrappedLabels[style] = wrapped;
                }
                style = wrapped;
            }

            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawStyle(style, rect, GetContent(widget.Text), false, false, false, false);
            }
        }

        private void RenderTableAt(LayoutNode node, Table table, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.PanelBg));
                if (table.ShowOuterBorder) DrawStrokeRect(rect, ToUnityColor(context.Theme.Tokens.Border), 1f);
                DrawTableRowsBackground(node, table, offset, context);
            }

            if (table.ShowHeader && HasVisibleTableColumns(table) && !HasExplicitHeaderRow(node)) {
                RenderTableHeader(context, node, table, rect);
            }

            RenderLayoutChildren(node, context, offset);
        }

        private void DrawTableRowsBackground(LayoutNode node, Table table, Vector2 offset, RenderContext context) {
            int dataRow = 0;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode rowNode = node.Children[i];
                TableRow row = rowNode.Widget as TableRow;
                if (row == null) continue;

                Rect rowRect = OffsetRect(rowNode.Rect, offset);
                if (row.HasBackground) {
                    DrawFilledRect(rowRect, ToUnityColor(row.Background));
                } else if (table.AlternatingRows && dataRow % 2 == 1) {
                    DrawFilledRect(rowRect, ToUnityColor(context.Theme.Tokens.ButtonBg));
                }

                if (table.ShowInnerBorders) {
                    DrawFilledRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), ToUnityColor(context.Theme.Tokens.Separator));
                }

                dataRow++;
            }
        }

        private void RenderTableHeader(RenderContext context, LayoutNode node, Table table, Rect tableRect) {
            float headerHeight = Mathf.Min(GetTableHeaderHeight(table, context), tableRect.height);
            float[] widths = GetTableColumnWidths(table, GetTableColumnCount(table, node), tableRect.width);
            float x = tableRect.x;
            int columnIndex = 0;
            for (int i = 0; i < table.Children.Count && columnIndex < widths.Length; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null || !column.Visible) continue;

                Rect rect = new Rect(x, tableRect.y, widths[columnIndex], headerHeight);
                bool clicked = DoButton(rect, string.Empty, context.Theme.TabButton, column.SortDirection != TableSortDirection.None);
                DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                    new Rect(rect.x + 8f, rect.y, Mathf.Max(0f, rect.width - (column.SortDirection == TableSortDirection.None ? 16f : 36f)), rect.height),
                    GetContent(column.Title), false, false, false, false);
                if (column.SortDirection != TableSortDirection.None)
                    DrawIcon(new Rect(rect.xMax - 24f, rect.y, 20f, rect.height),
                        column.SortDirection == TableSortDirection.Ascending ? Mdl2Icon.ChevronUp : Mdl2Icon.ChevronDown, NativeTextColor(context), 1f);
                if (clicked && column.Sortable && column.OnSort != null && !IsInputBlockedByDialog()) {
                    TableSortDirection next = NextSortDirection(column.SortDirection);
                    EnqueueCallback(delegate { column.OnSort(next); });
                }

                x += widths[columnIndex] + table.ColumnGap;
                columnIndex++;
            }
        }

        private void RenderTwoPaneLayoutAt(LayoutNode node, TwoPaneLayout widget, RenderContext context, Vector2 offset) {
            RenderLayoutChildren(node, context, offset);
            if (LayoutEngine.FlowChildCount(node) < 2) return;

            Rect first = OffsetRect(node.Children[0].Rect, offset);
            Rect second = OffsetRect(node.Children[1].Rect, offset);
            Rect whole = OffsetRect(node.Rect, offset);
            Rect splitter = widget.Orientation == TwoPaneOrientation.Horizontal
                ? new Rect(first.xMax, whole.y, Mathf.Max(1f, second.x - first.xMax), whole.height)
                : new Rect(whole.x, first.yMax, whole.width, Mathf.Max(1f, second.y - first.yMax));

            DrawFilledRect(splitter, ToUnityColor(context.Theme.Tokens.Separator));
            RegisterInputRegion(splitter, true);

            if (widget.OnChange == null || IsInputBlockedByDialog()) return;

            int controlId = GUIUtility.GetControlID(StableHash(context.StateId(widget, "splitter")), FocusType.Passive, splitter);
            Event current = Event.current;
            if (current == null) return;

            bool active = GUIUtility.hotControl == controlId;
            if (current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(splitter)) {
                CapturePointer(controlId);
                active = true;
                current.Use();
                Input.ResetInputAxes();
            }

            if (active && (current.type == EventType.MouseDrag || current.type == EventType.MouseDown)) {
                float next;
                if (widget.Orientation == TwoPaneOrientation.Horizontal) {
                    float available = Mathf.Max(1f, whole.width - widget.SplitterSize);
                    next = (current.mousePosition.x - whole.x) / available;
                } else {
                    float available = Mathf.Max(1f, whole.height - widget.SplitterSize);
                    next = (current.mousePosition.y - whole.y) / available;
                }
                next = Mathf.Clamp01(next);
                EnqueueCallback(delegate { widget.OnChange(next); });
                current.Use();
                Input.ResetInputAxes();
            }

            if (active && current.type == EventType.MouseUp && current.button == 0) {
                ReleasePointer(controlId);
                current.Use();
                Input.ResetInputAxes();
            }
        }

        private void RenderDragSourceAt(LayoutNode node, DragSource widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            string id = context.StateId(widget, "drag-source");

            if (GUI.enabled && widget.Payload != null && current != null && !IsInputBlockedByDialog()) {
                if (current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(rect)) {
                    context.DragDrop.Active = true;
                    context.DragDrop.SourceId = id;
                    context.DragDrop.Payload = widget.Payload;
                    MuiVec2 point = coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y));
                    context.DragDrop.StartPosition = new Vector2(point.X, point.Y);
                    context.DragDrop.CurrentPosition = context.DragDrop.StartPosition;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (context.DragDrop.Active && string.Equals(context.DragDrop.SourceId, id, StringComparison.Ordinal) &&
                    (current.type == EventType.MouseDrag || current.type == EventType.MouseMove || current.type == EventType.MouseDown)) {
                    MuiVec2 point = coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y));
                    context.DragDrop.CurrentPosition = new Vector2(point.X, point.Y);
                    if (current.type == EventType.MouseDrag) {
                        current.Use();
                        Input.ResetInputAxes();
                    }
                }
            }

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawStrokeRect(rect, ToUnityColor(context.DragDrop.Active && string.Equals(context.DragDrop.SourceId, id, StringComparison.Ordinal) ? context.Theme.Tokens.Accent : context.Theme.Tokens.Border), 1f);
            }

            RenderLayoutChildren(node, context, offset);
            RegisterInputRegion(rect, true);
        }

        private void RenderDropTargetAt(LayoutNode node, DropTarget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool accepts = DragPayloadMatches(context.DragDrop.Payload, widget.AcceptedType);
            bool hovered = context.DragDrop.Active && accepts && Event.current != null && MouseOverVisibleRect(rect);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                if (hovered) DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.SelectedBg));
                DrawStrokeRect(rect, ToUnityColor(hovered ? context.Theme.Tokens.Accent : context.Theme.Tokens.Border), 1f);
                if (node.Children.Count == 0 && !string.IsNullOrEmpty(widget.EmptyText)) {
                    DrawStyle(context.Theme.CenterLabel, rect, GetContent(widget.EmptyText), false, false, false, false);
                }
            }

            RenderLayoutChildren(node, context, offset);

            Event current = Event.current;
            if (GUI.enabled && current != null && current.type == EventType.MouseUp && current.button == 0 &&
                context.DragDrop.Active && accepts && MouseOverVisibleRect(rect)) {
                DragPayload payload = context.DragDrop.Payload;
                context.DragDrop.Clear();
                if (widget.OnDrop != null) EnqueueCallback(delegate { widget.OnDrop(payload); });
                current.Use();
                Input.ResetInputAxes();
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderBulletTextAt(LayoutNode node, BulletText widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float size = Mathf.Min(6f, Mathf.Max(3f, rect.height * 0.35f));
            Rect bullet = new Rect(rect.x + 4f, rect.y + Mathf.Max(0f, (rect.height - size) * 0.5f), size, size);
            DrawFilledRect(bullet, ToUnityColor(context.Theme.Tokens.TextMuted));
            DrawStyle(context.Theme.Label, new Rect(rect.x + 16f, rect.y, Mathf.Max(1f, rect.width - 16f), rect.height), GetContent(widget.Text), false, false, false, false);
        }

        private void RenderValueLabelAt(LayoutNode node, ValueLabel widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float labelWidth = widget.LabelWidth > 0f ? widget.LabelWidth : Mathf.Min(96f, rect.width * 0.45f);
            DrawStyle(context.Theme.MutedLabel, new Rect(rect.x, rect.y, labelWidth, rect.height), GetContent(widget.Label), false, false, false, false);
            DrawStyle(context.Theme.Label, new Rect(rect.x + labelWidth + 6f, rect.y, Mathf.Max(1f, rect.width - labelWidth - 6f), rect.height), GetContent(widget.Value), false, false, false, false);
        }

        private void RenderTextLinkAt(LayoutNode node, TextLink widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool hover;
            bool clicked = DoTextClick(rect, out hover) || ConsumeActivation(widget, context);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(widget.OnClick);
            }

            Color color = ToUnityColor(!GUI.enabled ? context.Theme.Tokens.TextDisabled : hover ? context.Theme.Tokens.AccentHover : context.Theme.Tokens.Accent);
            DrawStyle(!GUI.enabled ? disabledLabel : hover ? hoveredLinkLabel : linkLabel, rect, GetContent(widget.Text), false, false, false, false);
            DrawFilledRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 1f), color);
        }

        private void RegisterWidgetOverlays(LayoutNode node, Widget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            bool hovered = current != null && MouseOverVisibleRect(rect) &&
                coordinates.ContainsScreenPoint(coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y)));

            if (hovered && GUI.enabled && !IsInputBlockedByDialog() && !string.IsNullOrEmpty(widget.TooltipText)) {
                string tooltip = widget.TooltipText;
                MuiVec2 point = coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y));
                Vector2 mouse = new Vector2(point.X, point.Y);
                context.Overlay.Register(new OverlayRequest {
                    SortOrder = 1300,
                    Render = delegate(RenderContext renderContext) {
                        DrawTooltip(renderContext, tooltip, mouse);
                    }
                });
            }

            if (widget.ContextMenuContent == null) return;

            string id = context.StateId(widget, "context-menu");
            ContextMenuState state = context.StateStore.GetOrCreate<ContextMenuState>(id);
            state.Seen = true;
            if (!state.PopupOwner.Capture(ownerAncestors, modelRootEnabled) || !modelEnabled) {
                state.Open = false;
                state.PopupOwner.Invalidate();
                return;
            }
            if (GUI.enabled && !IsInputBlockedByDialog() && current != null && current.type == EventType.MouseDown && current.button == 1 && hovered) {
                context.CloseMenus();
                context.HideOtherComboBoxes(null);
                state.Open = true;
                state.PopupOwner.BeginOpen();
                MuiVec2 point = coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y));
                state.Position = new Vector2(point.X, point.Y);
                state.ModalDialog = currentDialog;
                context.ActiveContextMenu = state;
                current.Use();
                Input.ResetInputAxes();
            }

            if (!state.Open) return;

            Widget content = widget.ContextMenuContent;
            context.Overlay.Register(new OverlayRequest {
                SortOrder = 1200,
                Render = delegate(RenderContext renderContext) {
                    DrawPopup(renderContext, state.ModalDialog, delegate { DrawContextMenu(renderContext, id, state, content); }, state.PopupOwner);
                }
            });
        }

        private void DrawTooltip(RenderContext context, string text, Vector2 mousePosition) {
            if (string.IsNullOrEmpty(text)) return;

            Rect rect = ToUnityRect(PopupGeometry.Tooltip(text, new MuiVec2(mousePosition.x, mousePosition.y),
                new MuiRect(0f, 0f, Screen.width, Screen.height), popupTextMeasure, tooltipTextHeightMeasure, context.Theme.RowHeight));
            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                DrawStyle(context.Theme.HelpBox, rect, GetContent(text), false, false, false, false);
            } finally {
                GUI.color = oldColor;
            }
            RegisterInputRegion(rect, true, true);
        }

        private void DrawContextMenu(RenderContext context, string id, ContextMenuState state, Widget content) {
            Event current = Event.current;
            if (!state.Open) return;
            if (!state.PopupOwner.IsEnabled) { state.Open = false; state.PopupOwner.Invalidate(); return; }
            if (GUI.enabled && current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) {
                state.Open = false;
                current.Use();
                return;
            }

            LayoutNode measured = layoutEngine.Measure(content, new LayoutConstraints(0f, 260f, 0f, 420f), id + ":content");
            float width = Mathf.Max(120f, measured.DesiredSize.X + 8f);
            float height = Mathf.Max(20f, measured.DesiredSize.Y + 8f);
            state.Rect = ClampOverlayRect(new Rect(state.Position.x, state.Position.y, width, height));

            if (GUI.enabled && current != null && current.type == EventType.MouseDown && !MouseOverVisibleRect(state.Rect)) {
                state.Open = false;
                current.Use();
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                DrawStyle(context.Theme.Dropdown, state.Rect, GUIContent.none, false, false, false, false);
            } finally {
                GUI.color = oldColor;
            }

            LayoutNode root = layoutEngine.Layout(content, ToMuiRect(new Rect(state.Rect.x + 4f, state.Rect.y + 4f, Mathf.Max(1f, state.Rect.width - 8f), Mathf.Max(1f, state.Rect.height - 8f))), id + ":content");
            EventType before = current == null ? EventType.Ignore : current.type;
            RenderLayoutNode(root, context, Vector2.zero);
            if (before == EventType.MouseUp && current.type == EventType.Used) { state.Open = false; state.PopupOwner.Close(); }
            RegisterInputRegion(state.Rect, state.Open, true);
        }

        private void RenderButtonAt(LayoutNode node, Button widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            GUIStyle style = widget.Variant == ButtonVariant.Primary ? context.Theme.PrimaryButton :
                widget.Variant == ButtonVariant.Quiet ? context.Theme.QuietButton :
                widget.Variant == ButtonVariant.Danger ? context.Theme.DangerButton : context.Theme.Button;
            bool clicked = DoButton(rect, widget.Text, style, false) || ConsumeActivation(widget, context);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderSmallButtonAt(LayoutNode node, SmallButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, widget.Text, context.Theme.Button, false) || ConsumeActivation(widget, context);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderArrowButtonAt(LayoutNode node, ArrowButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoIconButton(rect, Mdl2IconGeometry.Chevron(widget.Direction), context.Theme.Button, false) || ConsumeActivation(widget, context);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderToggleAt(LayoutNode node, Toggle widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool inputEnabled = GUI.enabled && !IsInputBlockedByDialog();
            Event current = Event.current;
            if ((inputEnabled && current != null && current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(rect)) || ConsumeActivation(widget, context)) {
                bool captured = !widget.Value;
                if (widget.OnChange != null) EnqueueCallback(delegate { widget.OnChange(captured); });
                current.Use();
                Input.ResetInputAxes();
            }

            DrawSelectionControl(rect, widget.Text, widget.Value, false, context);
        }

        private void RenderToggleSwitchAt(LayoutNode node, ToggleSwitch widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool hover, pressed;
            bool clicked = DoButton(rect, string.Empty, null, false, out hover, out pressed) || ConsumeActivation(widget, context);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool value = !widget.Value;
                EnqueueCallback(delegate { widget.OnChange(value); });
            }
            ThemeTokens theme = context.Theme.Tokens;
            MuiRect track = Mdl2ControlChrome.SwitchBounds(ToMuiRect(rect));
            MuiColor border = widget.Value ? Mdl2ControlChrome.SelectionColor(theme, GUI.enabled, hover, pressed) :
                Mdl2ControlChrome.ControlBorderColor(theme, GUI.enabled, hover);
            DrawCapsule(ToUnityRect(track), border);
            if (!widget.Value) DrawCapsule(new Rect(track.X + 2f, track.Y + 2f, Mathf.Max(0f, track.Width - 4f), Mathf.Max(0f, track.Height - 4f)),
                GUI.enabled ? theme.FieldBg : theme.ButtonDisabledBg);
            MuiRect thumb = Mdl2ControlChrome.SwitchThumb(track, widget.Value);
            DrawDisc(ToUnityRect(thumb), widget.Value ? theme.OnAccent : border);
            Rect label = new Rect(track.Right + 12f, rect.y, Mathf.Max(0f, rect.xMax - track.Right - 12f), rect.height);
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel, label, GetContent(widget.Text), false, false, false, false);
        }

        private void RenderCheckboxAt(LayoutNode node, Checkbox widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool inputEnabled = GUI.enabled && !IsInputBlockedByDialog();
            Event current = Event.current;
            if ((inputEnabled && current != null && current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(rect)) || ConsumeActivation(widget, context)) {
                bool captured = !widget.Value;
                if (widget.OnChange != null) EnqueueCallback(delegate { widget.OnChange(captured); });
                current.Use();
                Input.ResetInputAxes();
            }

            DrawSelectionControl(rect, widget.Text, widget.Value, false, context);
        }

        private void RenderRadioButtonAt(LayoutNode node, RadioButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool inputEnabled = GUI.enabled && !IsInputBlockedByDialog();
            Event current = Event.current;
            if ((inputEnabled && current != null && current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(rect)) || ConsumeActivation(widget, context)) {
                if (!widget.Selected && widget.OnSelect != null) EnqueueCallback(widget.OnSelect);
                current.Use();
                Input.ResetInputAxes();
            }

            DrawSelectionControl(rect, widget.Text, widget.Selected, true, context);
        }

        private void RenderSegmentedControlAt(LayoutNode node, ISegmentedControlWidget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            List<object> values = new List<object>();
            System.Collections.IEnumerable enumerable = widget.SegmentItems;
            if (enumerable != null) {
                foreach (object item in enumerable) values.Add(item);
            }

            if (values.Count == 0) return;

            float segmentWidth = rect.width / values.Count;
            for (int i = 0; i < values.Count; i++) {
                object item = values[i];
                Rect segmentRect = new Rect(rect.x + segmentWidth * i, rect.y, segmentWidth, rect.height);
                bool selected = object.Equals(item, widget.SegmentValue);
                if (DoButton(segmentRect, widget.GetSegmentText(item), context.Theme.TabButton, selected) && widget.SegmentOnChange != null && !IsInputBlockedByDialog()) {
                    object captured = item;
                    Delegate callback = widget.SegmentOnChange;
                    EnqueueCallback(delegate { callback.DynamicInvoke(captured); });
                }
            }
        }

        private void RenderSliderAt(LayoutNode node, Slider widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float sliderHeight = Mathf.Min(rect.height, GetSliderHeight(context));
            Rect sliderRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - sliderHeight) * 0.5f), rect.width, sliderHeight);
            float value = DoSlider(rect, sliderRect, widget.Value, widget.Min, widget.Max, context);
            if (!Mathf.Approximately(value, widget.Value) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(delegate { widget.OnChange(value); });
            }
        }

        private void RenderInputIntAt(LayoutNode node, InputInt widget, RenderContext context, Vector2 offset) {
            Rect rect = DrawNumericLabel(OffsetRect(node.Rect, offset), widget.Label, context);
            double next;
            if (RenderNumericInput(rect, widget, context, NumericInputKind.Int, widget.Value,
                widget.HasMin, widget.Min, widget.HasMax, widget.Max,
                Event.current != null && Event.current.shift ? widget.StepFast : widget.Step,
                widget.Format, widget.OnChange != null, out next)) {
                int captured = (int)next;
                Action<int> callback = widget.OnChange;
                if (callback != null) EnqueueCallback(delegate { callback(captured); });
            }
        }

        private void RenderInputFloatAt(LayoutNode node, InputFloat widget, RenderContext context, Vector2 offset) {
            Rect rect = DrawNumericLabel(OffsetRect(node.Rect, offset), widget.Label, context);
            double next;
            if (RenderNumericInput(rect, widget, context, NumericInputKind.Float, widget.Value,
                widget.HasMin, widget.Min, widget.HasMax, widget.Max,
                Event.current != null && Event.current.shift ? widget.StepFast : widget.Step,
                widget.Format, widget.OnChange != null, out next)) {
                float captured = (float)next;
                Action<float> callback = widget.OnChange;
                if (callback != null) EnqueueCallback(delegate { callback(captured); });
            }
        }

        private void RenderInputDoubleAt(LayoutNode node, InputDouble widget, RenderContext context, Vector2 offset) {
            Rect rect = DrawNumericLabel(OffsetRect(node.Rect, offset), widget.Label, context);
            double next;
            if (RenderNumericInput(rect, widget, context, NumericInputKind.Double, widget.Value,
                widget.HasMin, widget.Min, widget.HasMax, widget.Max,
                Event.current != null && Event.current.shift ? widget.StepFast : widget.Step,
                widget.Format, widget.OnChange != null, out next)) {
                double captured = next;
                Action<double> callback = widget.OnChange;
                if (callback != null) EnqueueCallback(delegate { callback(captured); });
            }
        }

        private bool RenderNumericInput(Rect rect, Widget widget, RenderContext context, NumericInputKind kind,
            double value, bool hasMin, double min, bool hasMax, double max, double step, string format,
            bool editable, out double next) {
            string id = context.StateId(widget, "numeric");
            NumericInputState state = context.StateStore.GetOrCreate<NumericInputState>(id);
            state.Synchronize(value, kind, hasMin, min, hasMax, max);
            if (!editable && state.IsEditing) state.Abandon(value);
            next = state.Value;
            bool changed = false;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && editable;
            try {
                float buttonWidth = Mathf.Min(24f, Mathf.Max(18f, rect.height));
                Rect fieldRect = new Rect(rect.x, rect.y, Mathf.Max(1f, rect.width - buttonWidth * 2f), rect.height);
                Rect minusRect = new Rect(fieldRect.xMax, rect.y, buttonWidth, rect.height);
                Rect plusRect = new Rect(minusRect.xMax, rect.y, buttonWidth, rect.height);
                bool focused = context.OwnsKeyboardInput && string.Equals(GUI.GetNameOfFocusedControl(), id, StringComparison.Ordinal);
                if (focused && GUI.enabled && !IsInputBlockedByDialog()) state.BeginEdit();
                Event current = Event.current;
                bool completed = false;
                if (focused && GUI.enabled && !IsInputBlockedByDialog() && current != null && current.type == EventType.KeyDown &&
                    (current.keyCode == KeyCode.Escape || current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)) {
                    changed = current.keyCode == KeyCode.Escape ? state.Cancel(out next) : state.Commit(out next);
                    completed = true;
                    GUI.FocusControl(null);
                    if (context.Navigation != null) context.Navigation.FocusPointer(widget, context.Path);
                    ConsumeKey();
                }

                // Only round-trip numeric text enters Unity's editor. Formatted
                // labels can contain units or hundreds of decimal digits; passing
                // those through maxLength would turn a first focus into a value edit.
                string displayed = state.Text;
                GUI.SetNextControlName(id);
                string edited = GUI.TextField(fieldRect, displayed, 128, context.Theme.TextField);
                focused = context.OwnsKeyboardInput && string.Equals(GUI.GetNameOfFocusedControl(), id, StringComparison.Ordinal);
                if (!completed && GUI.enabled && context.OwnsKeyboardInput && !IsInputBlockedByDialog() &&
                    context.Navigation != null && context.Navigation.IsKeyboardFocused(widget, context.Path) && !focused) {
                    GUI.FocusControl(id);
                    focused = true;
                }
                if (focused) {
                    if (GUI.enabled && !IsInputBlockedByDialog()) {
                        state.BeginEdit();
                        MaximumUIRuntime.IsAnyTextFieldFocused = true;
                    }
                    if (!completed && GUI.enabled && !IsInputBlockedByDialog() && !string.Equals(edited, displayed, StringComparison.Ordinal))
                        changed = state.Edit(edited, out next) || changed;
                } else if (state.IsEditing) {
                    changed = state.Commit(out next) || changed;
                }
                if (!focused && !state.IsEditing) {
                    DrawStyle(context.Theme.TextField, fieldRect, GetContent(FormatNumericValue(state.Value, kind, format)),
                        !IsInputBlockedByDialog() && MouseOverVisibleRect(fieldRect), false, false, false);
                }

                int direction = 0;
                if (DoIconButton(minusRect, Mdl2Icon.Remove, context.Theme.Button, false)) direction = -1;
                if (DoIconButton(plusRect, Mdl2Icon.Add, context.Theme.Button, false)) direction = 1;
                if (direction != 0 && NumericInputParser.IsFinite(step) && !IsInputBlockedByDialog()) {
                    double candidate = state.Value + direction * step;
                    if (double.IsPositiveInfinity(candidate)) candidate = double.MaxValue;
                    if (double.IsNegativeInfinity(candidate)) candidate = -double.MaxValue;
                    candidate = NumericInputParser.Clamp(candidate, kind, hasMin, min, hasMax, max);
                    changed = state.Edit(NumericInputParser.Format(candidate, kind, null), out next) || changed;
                    changed = state.Commit(out next) || changed;
                    GUI.FocusControl(null);
                    if (context.Navigation != null) context.Navigation.FocusPointer(widget, context.Path);
                }
                return changed && editable;
            } finally {
                GUI.enabled = previousEnabled;
            }
        }

        private static string FormatNumericValue(double value, NumericInputKind kind, string format) {
            if (kind == NumericInputKind.Int) return FormatInt((int)value, format);
            if (kind == NumericInputKind.Float) return FormatFloat((float)value, format);
            return FormatDouble(value, format);
        }

        private void RenderDragIntAt(LayoutNode node, DragInt widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            int next = widget.Value;
            bool changed = DoNumericDrag(valueRect, FormatInt(widget.Value, widget.Format), context, delegate(float delta) {
                int amount = Mathf.RoundToInt(delta * Mathf.Max(0.001f, widget.Speed));
                if (amount != 0) next = ClampInt(widget.Value + amount, widget.HasMin, widget.Min, widget.HasMax, widget.Max);
            });
            if (changed && next != widget.Value && widget.OnChange != null && !IsInputBlockedByDialog()) {
                int captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderDragFloatAt(LayoutNode node, DragFloat widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            float next = widget.Value;
            bool changed = DoNumericDrag(valueRect, FormatFloat(widget.Value, widget.Format), context, delegate(float delta) {
                next = ClampFloat(widget.Value + delta * Mathf.Max(0.0001f, widget.Speed), widget.HasMin, widget.Min, widget.HasMax, widget.Max);
            });
            if (changed && !Mathf.Approximately(next, widget.Value) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderSliderIntAt(LayoutNode node, SliderInt widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            Rect sliderRect;
            Rect textRect;
            string valueText = FormatInt(widget.Value, widget.Format);
            SplitSliderValueRect(valueRect, valueText, context, out sliderRect, out textRect);
            int min = Math.Min(widget.Min, widget.Max);
            int max = Math.Max(widget.Min, widget.Max);
            float raw = DoSlider(valueRect, SliderTrackRect(sliderRect, context), widget.Value, min, max, context);
            int next = Mathf.Clamp(Mathf.RoundToInt(raw), min, max);
            DrawStyle(context.Theme.Label, textRect, GetContent(valueText), false, false, false, false);
            if (next != widget.Value && widget.OnChange != null && !IsInputBlockedByDialog()) {
                int captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderSliderFloatAt(LayoutNode node, SliderFloat widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            Rect sliderRect;
            Rect textRect;
            string valueText = FormatFloat(widget.Value, widget.Format);
            SplitSliderValueRect(valueRect, valueText, context, out sliderRect, out textRect);
            float value = DoSlider(valueRect, SliderTrackRect(sliderRect, context), widget.Value, widget.Min, widget.Max, context);
            DrawStyle(context.Theme.Label, textRect, GetContent(valueText), false, false, false, false);
            if (!Mathf.Approximately(value, widget.Value) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float captured = value;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderSliderAngleAt(LayoutNode node, SliderAngle widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            Rect sliderRect;
            Rect textRect;
            string valueText = widget.ValueDegrees.ToString("0") + " deg";
            SplitSliderValueRect(valueRect, valueText, context, out sliderRect, out textRect);
            float value = DoSlider(valueRect, SliderTrackRect(sliderRect, context), widget.ValueDegrees, widget.MinDegrees, widget.MaxDegrees, context);
            DrawStyle(context.Theme.Label, textRect, GetContent(valueText), false, false, false, false);
            if (!Mathf.Approximately(value, widget.ValueDegrees) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float captured = value;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderProgressBarAt(LayoutNode node, ProgressBar widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float trackHeight = Mathf.Min(4f, rect.height);
            Rect track = new Rect(rect.x, rect.yMax - trackHeight, rect.width, trackHeight);
            DrawFilledRect(track, ToUnityColor(context.Theme.Tokens.ButtonBg));
            float t = widget.Indeterminate ? 0.35f : Mathf.Clamp01(widget.Value);
            DrawFilledRect(new Rect(track.x, track.y, track.width * t, track.height),
                ToUnityColor(GUI.enabled ? context.Theme.Tokens.Accent : context.Theme.Tokens.TextDisabled));
            string text = widget.Text;
            if (string.IsNullOrEmpty(text)) text = ((int)(Mathf.Clamp01(widget.Value) * 100f)).ToString() + "%";
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, rect.height - 8f)), GetContent(text), false, false, false, false);
        }

        private void RenderStatusBadgeAt(LayoutNode node, StatusBadge widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            DrawFilledRect(rect, StatusColor(widget.Kind, context.Theme.Tokens));
            DrawStrokeRect(rect, ToUnityColor(context.Theme.Tokens.Border), 1f);
            DrawStyle(context.Theme.CenterLabel, rect, GetContent(widget.Text), false, false, false, false);
        }

        private void RenderLoadingIndicatorAt(LayoutNode node, LoadingIndicator widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float radius = Mathf.Min(2f, rect.height * 0.5f);
            for (int i = 0; i < 5; i++)
                DrawDisc(new Rect(rect.x + 2f + i * 7f - radius, rect.center.y - radius, radius * 2f, radius * 2f),
                    GUI.enabled ? context.Theme.Tokens.Accent : context.Theme.Tokens.TextDisabled);
            string text = string.IsNullOrEmpty(widget.Text) ? "Loading" : widget.Text;
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel, new Rect(rect.x + 44f, rect.y, Mathf.Max(0f, rect.width - 44f), rect.height), GetContent(text), false, false, false, false);
        }

        private void RenderImageAt(LayoutNode node, Image widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            DrawStyle(context.Theme.TextField, rect, GUIContent.none, false, false, false, false);
            Texture texture = widget.Texture as Texture;
            if (texture != null) {
                Color oldColor = GUI.color;
                GUI.color = ToUnityColor(widget.Tint);
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
                GUI.color = oldColor;
            }
        }

        private void RenderImageButtonAt(LayoutNode node, ImageButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, string.Empty, context.Theme.Button, false) || ConsumeActivation(widget, context);
            Texture texture = widget.Texture as Texture;
            float imageSize = Mathf.Min(rect.height - 4f, 20f);
            if (texture != null && imageSize > 0f && Event.current != null && Event.current.type == EventType.Repaint) {
                Color oldColor = GUI.color;
                GUI.color = ToUnityColor(widget.Tint);
                GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + Mathf.Max(0f, (rect.height - imageSize) * 0.5f), imageSize, imageSize), texture, ScaleMode.ScaleToFit, true);
                GUI.color = oldColor;
            }
            if (!string.IsNullOrEmpty(widget.Text)) {
                DrawStyle(context.Theme.Label, new Rect(rect.x + imageSize + 8f, rect.y, Mathf.Max(1f, rect.width - imageSize - 10f), rect.height), GetContent(widget.Text), false, false, false, false);
            }
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderTextFieldAt(LayoutNode node, TextField widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "text");
            TextFieldState state = context.StateStore.GetOrCreate<TextFieldState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            RenderTextInputCore(
                rect,
                state,
                id,
                widget.Value,
                widget.Placeholder,
                widget.MaxLength,
                widget.Style as GUIStyle ?? context.Theme.TextField,
                widget.Flags,
                widget.CharFilter,
                widget.AutoFocus,
                widget.OnChange,
                widget.OnSubmit,
                widget.OnBlur,
                widget.OnEdit,
                context,
                TextInputKind.TextField,
                '\0');
        }

        private void RenderSearchBoxAt(LayoutNode node, SearchBox widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "search");
            TextFieldState state = context.StateStore.GetOrCreate<TextFieldState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            bool hasText = !string.IsNullOrEmpty(state.Focused && state.HasEditingValue ? state.EditingValue : widget.Value);
            bool canClear = hasText && !HasTextInputFlag(widget.Flags, TextInputFlags.ReadOnly);
            bool clearHovered = false, clearPressed = false;
            Rect clearRect = new Rect(rect.xMax - 30f, rect.y + 2f, 28f, Mathf.Max(0f, rect.height - 4f));
            if (canClear) {
                bool clear = DoButton(clearRect, string.Empty, null, false, out clearHovered, out clearPressed, true);
                if (clear) {
                    if (string.IsNullOrEmpty(state.ControlName)) state.ControlName = id;
                    TextEditingLifecycle.Edit(state, string.Empty);
                    GUI.FocusControl(state.ControlName);
                    if (widget.OnEdit != null) EnqueueCallback(delegate { widget.OnEdit(string.Empty); });
                    if (widget.OnChange != null) EnqueueCallback(delegate { widget.OnChange(string.Empty); });
                }
            }
            RenderTextInputCore(
                rect,
                state,
                id,
                widget.Value,
                widget.Placeholder,
                widget.MaxLength,
                searchFieldStyle,
                widget.Flags,
                widget.CharFilter,
                false,
                widget.OnChange,
                widget.OnSubmit,
                widget.OnBlur,
                widget.OnEdit,
                context,
                TextInputKind.TextField,
                '\0');
            if (clearHovered || clearPressed) DrawFilledRect(clearRect,
                ToUnityColor(clearPressed ? context.Theme.Tokens.ButtonActiveBg : context.Theme.Tokens.ButtonHoverBg));
            DrawIcon(new Rect(rect.xMax - 28f, rect.y, 24f, rect.height),
                string.IsNullOrEmpty(state.EditingValue) || HasTextInputFlag(widget.Flags, TextInputFlags.ReadOnly) ? Mdl2Icon.Search : Mdl2Icon.Close, NativeTextColor(context), 1f);
        }

        private void RenderTextAreaAt(LayoutNode node, TextArea widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "textarea");
            TextFieldState state = context.StateStore.GetOrCreate<TextFieldState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            RenderTextInputCore(
                rect,
                state,
                id,
                widget.Value,
                widget.Placeholder,
                widget.MaxLength,
                widget.Style as GUIStyle ?? context.Theme.TextField,
                widget.Flags,
                widget.CharFilter,
                false,
                widget.OnChange,
                widget.OnSubmit,
                widget.OnBlur,
                widget.OnEdit,
                context,
                TextInputKind.TextArea,
                '\0');
        }

        private void RenderPasswordFieldAt(LayoutNode node, PasswordField widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "password");
            TextFieldState state = context.StateStore.GetOrCreate<TextFieldState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            RenderTextInputCore(
                rect,
                state,
                id,
                widget.Value,
                widget.Placeholder,
                widget.MaxLength,
                widget.Style as GUIStyle ?? context.Theme.TextField,
                widget.Flags,
                widget.CharFilter,
                false,
                widget.OnChange,
                widget.OnSubmit,
                widget.OnBlur,
                widget.OnEdit,
                context,
                TextInputKind.Password,
                widget.MaskChar);
        }

        private enum TextInputKind {
            TextField,
            TextArea,
            Password
        }

        private void RenderTextInputCore(
            Rect rect,
            TextFieldState state,
            string id,
            string value,
            string placeholder,
            int maxLength,
            GUIStyle style,
            TextInputFlags flags,
            Func<char, bool> charFilter,
            bool autoFocus,
            Action<string> onChange,
            Action<string> onSubmit,
            Action<string> onBlur,
            Action<string> onEdit,
            RenderContext context,
            TextInputKind kind,
            char maskChar) {
            if (state == null) return;
            if (string.IsNullOrEmpty(state.ControlName)) state.ControlName = id;

            string externalValue = value ?? string.Empty;
            bool focusedBefore = string.Equals(GUI.GetNameOfFocusedControl(), state.ControlName, StringComparison.Ordinal);
            bool wasFocused = TextEditingLifecycle.Synchronize(state, externalValue, focusedBefore, flags, charFilter, maxLength);

            Event keyEvent = Event.current;
            bool receivedKey = keyEvent != null && keyEvent.type == EventType.KeyDown;
            KeyCode receivedKeyCode = receivedKey ? keyEvent.keyCode : KeyCode.None;
            GUI.SetNextControlName(state.ControlName);
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            if (HasTextInputFlag(flags, TextInputFlags.ReadOnly)) GUI.enabled = false;
            string next;
            try {
                next = DrawUnityTextInput(rect, state.EditingValue, maxLength, style, kind, maskChar);
            } finally {
                GUI.enabled = oldEnabled;
                GUI.color = oldColor;
            }

            next = HasTextInputFlag(flags, TextInputFlags.ReadOnly)
                ? state.EditingValue
                : ApplyTextInputFilter(next, flags, charFilter, maxLength);

            state.Focused = string.Equals(GUI.GetNameOfFocusedControl(), state.ControlName, StringComparison.Ordinal);
            if (GUI.enabled && !IsInputBlockedByDialog() && context.OwnsKeyboardInput && ((autoFocus && !state.AutoFocusApplied) ||
                (currentWidget != null && context.Navigation != null && context.Navigation.IsKeyboardFocused(currentWidget, context.Path) && !state.Focused))) {
                GUI.FocusControl(state.ControlName);
                state.AutoFocusApplied = true;
                state.Focused = true;
            }
            bool acceptsEditing = GUI.enabled && !HasTextInputFlag(flags, TextInputFlags.ReadOnly);
            DrawStrokeRect(rect, ToUnityColor(Mdl2ControlChrome.FieldBorderColor(context.Theme.Tokens,
                acceptsEditing, acceptsEditing && !IsInputBlockedByDialog() && MouseOverVisibleRect(rect), state.Focused && context.OwnsKeyboardInput && !IsInputBlockedByDialog())), 2f);

            if (state.Focused && !state.HasFocusStartValue) {
                state.FocusStartValue = state.EditingValue ?? string.Empty;
                state.HasFocusStartValue = true;
            }

            if (state.Focused && GUI.enabled && context.OwnsKeyboardInput && !IsInputBlockedByDialog()) MaximumUIRuntime.IsAnyTextFieldFocused = true;
            if (state.Focused && GUI.enabled && context.OwnsKeyboardInput && !IsInputBlockedByDialog())
                MaximumUIRuntime.EnqueueCallback(textFocus.Track(state, onBlur, currentWidget, currentPopupOwner));

            Event current = Event.current;
            bool handledKey = false;
            bool submitRequested = false;
            if (state.Focused && GUI.enabled && context.OwnsKeyboardInput && !IsInputBlockedByDialog() && receivedKey) {
                if (receivedKeyCode == KeyCode.Escape) {
                    if (HasTextInputFlag(flags, TextInputFlags.EscapeClearsAll)) {
                        next = string.Empty;
                        handledKey = true;
                    } else if (HasTextInputFlag(flags, TextInputFlags.EscapeReverts)) {
                        next = state.HasFocusStartValue ? state.FocusStartValue : externalValue;
                        handledKey = true;
                    }
                } else if ((receivedKeyCode == KeyCode.Return || receivedKeyCode == KeyCode.KeypadEnter) &&
                    (kind != TextInputKind.TextArea || HasTextInputFlag(flags, TextInputFlags.EnterReturnsTrue))) {
                    submitRequested = onSubmit != null;
                    handledKey = true;
                }
            }

            if (!state.Focused && string.IsNullOrEmpty(next) && !string.IsNullOrEmpty(placeholder)) {
                Rect placeholderRect = new Rect(rect.x + style.padding.left, rect.y, Mathf.Max(0f, rect.width - style.padding.horizontal), rect.height);
                DrawStyle(context.Theme.MutedLabel, placeholderRect, GetContent(placeholder), false, false, false, false);
            }

            if (!HasTextInputFlag(flags, TextInputFlags.ReadOnly) && !string.Equals(next, state.EditingValue, StringComparison.Ordinal)) {
                TextEditingLifecycle.Edit(state, next);
                Input.ResetInputAxes();
                if (!IsInputBlockedByDialog()) {
                    if (onEdit != null) {
                        string capturedEdit = next;
                        EnqueueCallback(delegate { onEdit(capturedEdit); });
                    }
                    if (onChange != null) {
                        string captured = next;
                        EnqueueCallback(delegate { onChange(captured); });
                    }
                }
            }

            if (submitRequested) {
                // Changes from this event must run first, so a model's submit
                // normalization cannot be overwritten by its own OnChange.
                EnqueueCallback(TextEditingLifecycle.CommitCallback(state, onSubmit));
            }

            if (handledKey && current != null) {
                current.Use();
                Input.ResetInputAxes();
            }

            if (wasFocused && !state.Focused) {
                state.HasFocusStartValue = false;
                if (textFocus.Owns(state)) CommitFocusedText();
                else if (onBlur != null) EnqueueCallback(TextEditingLifecycle.CommitCallback(state, onBlur));
            }
            state.WasFocused = state.Focused;
        }

        private static string DrawUnityTextInput(Rect rect, string value, int maxLength, GUIStyle style, TextInputKind kind, char maskChar) {
            string text = value ?? string.Empty;
            if (kind == TextInputKind.TextArea) {
                return maxLength > 0 ? GUI.TextArea(rect, text, maxLength, style) : GUI.TextArea(rect, text, style);
            }

            if (kind == TextInputKind.Password) {
                char mask = maskChar == '\0' ? '*' : maskChar;
                return maxLength > 0 ? GUI.PasswordField(rect, text, mask, maxLength, style) : GUI.PasswordField(rect, text, mask, style);
            }

            return maxLength > 0 ? GUI.TextField(rect, text, maxLength, style) : GUI.TextField(rect, text, style);
        }

        private static string ApplyTextInputFilter(string value, TextInputFlags flags, Func<char, bool> charFilter, int maxLength) {
            return TextInputFilter.Apply(value, flags, charFilter, maxLength);
        }

        private static bool HasTextInputFlag(TextInputFlags flags, TextInputFlags flag) {
            return (flags & flag) == flag;
        }

        private void RenderHelpBoxAt(LayoutNode node, HelpBox widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                ThemeTokens tokens = context.Theme.Tokens;
                MuiColor background = widget.Type == HelpBoxType.Error ? tokens.ErrorBg : widget.Type == HelpBoxType.Warning ? tokens.WarningBg : tokens.PanelBg;
                DrawFilledRect(rect, ToUnityColor(background));
                DrawStrokeRect(rect, ToUnityColor(tokens.Border), tokens.BorderWidth);
                DrawStyle(GUI.enabled ? helpTextStyle : disabledHelpTextStyle, ToUnityRect(ThemeTypography.HelpTextBounds(ToMuiRect(rect))),
                    GetContent(widget.Message), false, false, false, false);
            }
        }

        private void RenderListBoxAt(LayoutNode node, IListBoxWidget widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(node.Widget, "listbox");
            ListBoxState state = context.StateStore.GetOrCreate<ListBoxState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            ISearchableListBoxWidget searchable = node.Widget as ISearchableListBoxWidget;
            string filter = searchable == null ? string.Empty : searchable.ListFilterText ?? string.Empty;
            if (!ReferenceEquals(state.Owner, widget)) {
                state.Owner = widget;
                state.Formatter = widget.GetListText;
            }
            if (!string.Equals(state.Filter, filter, StringComparison.Ordinal)) {
                state.Filter = filter;
                state.Position = Vector2.zero;
            }
            state.Choices.Refresh(widget, widget.ListItems, state.Formatter, filter, contentRevision, context.CacheLayout);
            IList<object> values = state.Choices.Values;
            float rowHeight = controlRowHeight;
            bool filterFocused = false;
            if (searchable != null) {
                Rect filterRect = ToUnityRect(ScrollGeometry.ListFilterBounds(ToMuiRect(rect), rowHeight));
                if (state.FilterWidget.Key == null) state.FilterWidget.Key = id + ":filter";
                string filterId = (string)state.FilterWidget.Key;
                TextFieldState filterState = context.StateStore.GetOrCreate<TextFieldState>(filterId);
                Widget previousWidget = currentWidget;
                string previousPath = context.Path;
                state.FilterWidget.Enabled = node.Widget.Enabled;
                state.FilterWidget.Focusable = node.Widget.Focusable;
                state.FilterNode.Rect = ToMuiRect(filterRect);
                state.FilterNode.Path = filterId;
                currentWidget = state.FilterWidget;
                context.Path = state.FilterNode.Path;
                try {
                    RecordItemQuery(context, state.FilterNode, state.FilterWidget, Vector2.zero);
                    RenderTextInputCore(filterRect, filterState, filterId, filter, searchable.ListFilterPlaceholder,
                        0, searchFieldStyle, TextInputFlags.None, null, false, searchable.ListOnFilterChange,
                        null, null, null, context, TextInputKind.TextField, '\0');
                } finally { currentWidget = previousWidget; context.Path = previousPath; }
                filterFocused = filterState.Focused;
                DrawIcon(new Rect(filterRect.xMax - 28f, filterRect.y, 24f, filterRect.height), Mdl2Icon.Search, NativeTextColor(context), 1f);
            }
            ScrollFrameLayout frame = ScrollGeometry.ListFrame(ToMuiRect(rect), rowHeight, values.Count, searchable != null);
            Rect listRect = ToUnityRect(frame.Viewport);
            Rect viewRect = ToUnityRect(frame.Content);
            state.Position = new Vector2(0f, frame.ClampY(state.Position.y));
            if (!filterFocused && HasNavigationKey(node.Widget, context) && widget.ListOnChange != null) {
                int selected = -1;
                for (int i = 0; i < values.Count; i++) {
                    if (object.Equals(values[i], widget.ListValue)) { selected = i; break; }
                }
                int next;
                if (KeyboardNavigation.MoveSelection(selected, values.Count, Event.current.keyCode,
                    Math.Max(1, (int)(frame.ContentViewport.Height / rowHeight)), out next)) {
                    state.Position.y = KeyboardNavigation.RevealRow(state.Position.y, next, rowHeight, frame.ContentViewport.Height, values.Count);
                    if (next != selected) {
                        object captured = values[next];
                        Delegate callback = widget.ListOnChange;
                        EnqueueCallback(delegate { callback.DynamicInvoke(captured); });
                    }
                    ConsumeKey();
                }
            }
            DrawFilledRect(ToUnityRect(frame.Bounds), ToUnityColor(context.Theme.Tokens.FieldBg));
            state.Position = BeginScrollView(context, listRect, state.Position, viewRect, false, frame.Vertical);
            try {
                int first, end;
                ScrollGeometry.VisibleRows(state.Position.y, frame.ContentViewport.Height, rowHeight, values.Count, out first, out end);
                for (int i = first; i < end; i++) {
                    object item = values[i];
                    Rect rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);
                    bool selected = object.Equals(item, widget.ListValue);
                    if (DoButton(rowRect, state.Choices.Labels[i], context.Theme.NavigationItem, selected) && widget.ListOnChange != null && !IsInputBlockedByDialog()) {
                        object captured = item;
                        Delegate callback = widget.ListOnChange;
                        EnqueueCallback(delegate { callback.DynamicInvoke(captured); });
                    }
                }
            } finally { EndScrollView(); }
            DrawScrollFrame(frame.Bounds, context);
        }

        private void RenderDebugLinesAt(LayoutNode node, IDebugLinesWidget widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(node.Widget, "debug-lines");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            float rowHeight = controlRowHeight;
            IList<string> lines = widget.GetDebugLines();
            ScrollFrameLayout frame = ScrollGeometry.DebugFrame(ToMuiRect(rect), rowHeight, lines.Count);
            Rect viewRect = ToUnityRect(frame.Content);
            state.Position = new Vector2(0f, frame.ClampY(state.Position.y));

            DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.FieldBg));
            state.Position = BeginScrollView(context, ToUnityRect(frame.Viewport), state.Position, viewRect, false, frame.Vertical);
            try {
                Rect title = new Rect(2f, 2f, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    DrawStyle(context.Theme.TabButton, title, GetContent(widget.DebugTitle), false, false, true, false);
                }

                for (int i = 0; i < lines.Count; i++) {
                    Rect line = new Rect(6f, title.yMax + i * rowHeight, Mathf.Max(1f, viewRect.width - 12f), rowHeight);
                    if (Event.current == null || Event.current.type == EventType.Repaint) {
                        DrawStyle(context.Theme.Label, line, GetContent(lines[i]), false, false, false, false);
                    }
                }
            } finally {
                EndScrollView();
            }

            DrawScrollFrame(frame.Bounds, context);
            RegisterInputRegion(rect, true);
        }

        private void RenderThemeEditorAt(LayoutNode node, ThemeEditor widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(node.Widget, "theme-editor");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            ThemeTokens tokens = widget.Tokens ?? new ThemeTokens();
            float rowHeight = controlRowHeight;
            ScrollFrameLayout frame = ScrollGeometry.ThemeEditorFrame(ToMuiRect(rect), rowHeight, widget);
            Rect viewRect = ToUnityRect(frame.Content);
            state.Position = new Vector2(0f, frame.ClampY(state.Position.y));
            float colorLabelWidth = ThemeEditorTokens.MeasureLabelWidth(widget, popupTextMeasure, Mathf.Max(0f, viewRect.width - 4f));

            DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.FieldBg));
            state.Position = BeginScrollView(context, ToUnityRect(frame.Viewport), state.Position, viewRect, false, frame.Vertical);
            try {
                Rect title = new Rect(2f, 2f, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                if (widget.ShowTitle && (Event.current == null || Event.current.type == EventType.Repaint)) {
                    DrawStyle(context.Theme.Subheading, title, GetContent(widget.Title), false, false, false, false);
                }
                float contentTop = widget.ShowTitle ? title.yMax : 2f;

                int row = 0;
                if (widget.ShowColors) {
                    for (int i = 0; i < ThemeEditorTokens.GetColors(widget).Length; i++) {
                        ThemeColorToken token = ThemeEditorTokens.GetColors(widget)[i];
                        Rect rowRect = new Rect(2f, contentTop + row * rowHeight, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                        RenderThemeColorRow(context, widget, tokens, token, rowRect, colorLabelWidth);
                        row++;
                    }
                }

                if (widget.ShowScalars) {
                    for (int i = 0; i < ThemeEditorTokens.GetScalars(widget).Length; i++) {
                        ThemeScalarToken token = ThemeEditorTokens.GetScalars(widget)[i];
                        Rect rowRect = new Rect(2f, contentTop + row * rowHeight, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                        RenderThemeScalarRow(context, widget, tokens, token, rowRect);
                        row++;
                    }
                }
            } finally {
                EndScrollView();
            }

            DrawScrollFrame(frame.Bounds, context);
            RegisterInputRegion(rect, true);
        }

        private void RenderThemeColorRow(RenderContext context, ThemeEditor editor, ThemeTokens tokens, ThemeColorToken token, Rect rowRect, float labelWidth) {
            MuiColor value;
            if (RenderColorEditCore(rowRect, ThemeEditorTokens.ColorName(token), tokens.GetColor(token),
                true, ColorDisplayMode.Byte, true, context, out value, labelWidth) && !IsInputBlockedByDialog()) {
                ThemeTokens next = tokens.Clone();
                next.SetColor(token, value);
                ApplyThemeEdit(editor, next);
            }
        }

        private void RenderThemeScalarRow(RenderContext context, ThemeEditor editor, ThemeTokens tokens, ThemeScalarToken token, Rect rowRect) {
            Rect labelRect = new Rect(rowRect.x + 6f, rowRect.y, Mathf.Max(1f, rowRect.width - 116f), rowRect.height);
            Rect valueRect = new Rect(rowRect.xMax - 104f, rowRect.y, 48f, rowRect.height);
            Rect minusRect = new Rect(rowRect.xMax - 50f, rowRect.y + 2f, 22f, Mathf.Max(1f, rowRect.height - 4f));
            Rect plusRect = new Rect(rowRect.xMax - 25f, rowRect.y + 2f, 22f, Mathf.Max(1f, rowRect.height - 4f));
            float value = tokens.GetScalar(token);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawStyle(context.Theme.Label, labelRect, GetContent(ThemeEditorTokens.ScalarName(token)), false, false, false, false);
                DrawStyle(context.Theme.Label, valueRect, GetContent(value.ToString("0.##")), false, false, false, false);
            }

            if (DoIconButton(minusRect, Mdl2Icon.Remove, context.Theme.Button, false) && !IsInputBlockedByDialog()) {
                ApplyThemeScalarEdit(editor, tokens, token, value - Mathf.Max(0.1f, editor.ScalarStep));
            }

            if (DoIconButton(plusRect, Mdl2Icon.Add, context.Theme.Button, false) && !IsInputBlockedByDialog()) {
                ApplyThemeScalarEdit(editor, tokens, token, value + Mathf.Max(0.1f, editor.ScalarStep));
            }
        }

        private void ApplyThemeScalarEdit(ThemeEditor editor, ThemeTokens tokens, ThemeScalarToken token, float value) {
            ThemeTokens next = tokens.Clone();
            next.SetScalar(token, ClampThemeScalar(token, value));
            ApplyThemeEdit(editor, next);
        }

        private void ApplyThemeEdit(ThemeEditor editor, ThemeTokens next) {
            if (editor == null || next == null) return;

            if (editor.OnChange != null) {
                ThemeTokens captured = next;
                EnqueueCallback(delegate { editor.OnChange(captured); });
            } else if (editor.Tokens != null) {
                editor.Tokens.CopyFrom(next);
            }
        }

        private void RenderSeparatorAt(LayoutNode node, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect lineRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - 1f) * 0.5f), rect.width, 1f);
            DrawStyle(context.Theme.Separator, lineRect, GUIContent.none, false, false, false, false);
        }

        private void RenderSeparatorTextAt(LayoutNode node, SeparatorText widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float y = rect.y + Mathf.Max(0f, (rect.height - 1f) * 0.5f);
            float textWidth = Mathf.Min(rect.width, Mathf.Max(20f, widget.Text.Length * context.Theme.FontSize * 0.55f + 12f));
            float lineWidth = Mathf.Max(0f, (rect.width - textWidth) * 0.5f);
            DrawStyle(context.Theme.Separator, new Rect(rect.x, y, lineWidth, 1f), GUIContent.none, false, false, false, false);
            DrawStyle(context.Theme.Separator, new Rect(rect.xMax - lineWidth, y, lineWidth, 1f), GUIContent.none, false, false, false, false);
            DrawStyle(context.Theme.CenterLabel, new Rect(rect.x + lineWidth, rect.y, textWidth, rect.height), GetContent(widget.Text), false, false, false, false);
        }

        private void RenderScrollViewAt(LayoutNode node, ScrollView widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "scroll");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            Rect viewRect = CalculateScrollViewRect(node, rect);
            Vector2 before = widget.ScrollPosition == null ? state.Position : widget.ScrollPosition.Resolve(
                ToMuiRect(rect), ToMuiRect(viewRect), widget.AlwaysShowHorizontal, widget.AlwaysShowVertical);
            state.Position = BeginScrollView(context, rect, before, viewRect, widget.AlwaysShowHorizontal, widget.AlwaysShowVertical);
            if (widget.ScrollPosition != null) widget.ScrollPosition.Publish(before, state.Position, GUI.enabled && !IsInputBlockedByDialog());
            try {
                RenderLayoutChildren(node, context, new Vector2(-node.Rect.X, -node.Rect.Y));
            } finally {
                EndScrollView();
            }
        }

        private void RenderFoldoutAt(LayoutNode node, Foldout widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, controlRowHeight));
            bool clicked = DoDisclosureButton(headerRect, widget.Label, widget.Open, context, widget.Framed) || ConsumeActivation(widget, context);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool open = !widget.Open;
                EnqueueCallback(delegate { widget.OnChange(open); });
            }

            if (widget.Open) RenderLayoutChildren(node, context, offset);
        }

        private void RenderCollapsingHeaderAt(LayoutNode node, CollapsingHeader widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, controlRowHeight));
            bool clicked = DoDisclosureButton(headerRect, widget.Label, widget.Open, context, widget.Framed) || ConsumeActivation(widget, context);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool open = !widget.Open;
                EnqueueCallback(delegate { widget.OnChange(open); });
            }

            if (widget.Open) RenderLayoutChildren(node, context, offset);
        }

        private void RenderClosableCollapsingHeaderAt(LayoutNode node, ClosableCollapsingHeader widget, RenderContext context, Vector2 offset) {
            if (!widget.Visible) return;

            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, controlRowHeight));
            float closeSize = Mathf.Min(headerRect.width, 32f);
            Rect closeRect = new Rect(headerRect.xMax - closeSize, headerRect.y, closeSize, headerRect.height);
            Rect toggleRect = new Rect(headerRect.x, headerRect.y, Mathf.Max(0f, headerRect.width - closeSize), headerRect.height);

            bool clicked = DoDisclosureButton(toggleRect, widget.Label, widget.Open, context, widget.Framed, headerRect) || ConsumeActivation(widget, context);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool open = !widget.Open;
                EnqueueCallback(delegate { widget.OnChange(open); });
            }

            if (DoIconButton(closeRect, Mdl2Icon.Close, context.Theme.QuietButton, false) && !IsInputBlockedByDialog()) {
                if (widget.OnClose != null) EnqueueCallback(widget.OnClose);
            }

            if (widget.Open) RenderLayoutChildren(node, context, offset);
        }

        private void RenderTabsAt(LayoutNode node, Tabs widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (widget.Children.Count == 0) return;

            string id = context.StateId(widget, "tabs");
            TabsState state = context.StateStore.GetOrCreate<TabsState>(id);
            float headerHeight = controlRowHeight;
            List<Tab> tabs = state.VisibleTabs;
            List<TabItemButton> leadingButtons = state.LeadingButtons;
            List<TabItemButton> trailingButtons = state.TrailingButtons;
            if (!context.CacheLayout || !ReferenceEquals(state.Owner, widget) || state.Revision != contentRevision) {
                tabs.Clear(); leadingButtons.Clear(); trailingButtons.Clear();
                state.Owner = widget;
                state.Revision = contentRevision;
                for (int i = 0; i < widget.Children.Count; i++) {
                    Tab tab = widget.Children[i] as Tab;
                    if (tab != null) {
                        if (tab.Visible) tabs.Add(tab);
                        continue;
                    }
                    TabItemButton button = widget.Children[i] as TabItemButton;
                    if (button == null) continue;
                    if (button.Placement == TabButtonPlacement.Leading) leadingButtons.Add(button);
                    else trailingButtons.Add(button);
                }
            }

            float x = rect.x;
            for (int i = 0; i < leadingButtons.Count; i++) {
                float width = Mathf.Max(20f, leadingButtons[i].ButtonWidth);
                DrawTabItemButton(new Rect(x, rect.y, width, headerHeight), leadingButtons[i], context);
                x += width + 2f;
            }

            float trailingWidth = 0f;
            for (int i = 0; i < trailingButtons.Count; i++) trailingWidth += Mathf.Max(20f, trailingButtons[i].ButtonWidth) + 2f;
            float tabAreaWidth = Mathf.Max(1f, rect.xMax - x - trailingWidth);
            int selected = Mathf.Clamp(widget.SelectedIndex, 0, Math.Max(0, tabs.Count - 1));
            if (HasNavigationKey(widget, context) && widget.OnChange != null) {
                int next;
                if (KeyboardNavigation.MoveSelection(selected, tabs.Count, Event.current.keyCode, 1, out next)) {
                    if (next != selected) {
                        int captured = next;
                        EnqueueCallback(delegate { widget.OnChange(captured); });
                    }
                    ConsumeKey();
                }
            }
            int dropTarget = -1;
            float tabWidth = tabs.Count <= 0 ? tabAreaWidth : tabAreaWidth / tabs.Count;
            Event current = Event.current;
            EventType rawType = current == null ? EventType.Ignore : current.rawType;
            bool acceptsInput = GUI.enabled && !IsInputBlockedByDialog() && current != null && current.type != EventType.Used;
            if (widget.Reorderable && acceptsInput && rawType == EventType.MouseDrag)
                state.MoveDrag(new MuiVec2(current.mousePosition.x, current.mousePosition.y));

            for (int i = 0; i < tabs.Count; i++) {
                Tab tab = tabs[i];
                Rect tabRect = new Rect(x + tabWidth * i, rect.y, tabWidth, headerHeight);
                if (current != null && MouseOverVisibleRect(tabRect)) dropTarget = i;

                bool closeTriggered = false;
                if (tab.Closable) {
                    float closeSize = Mathf.Min(headerHeight - 4f, 18f);
                    Rect closeRect = new Rect(tabRect.xMax - closeSize - 2f, tabRect.y + 2f, closeSize, closeSize);
                    if (DoIconButton(closeRect, Mdl2Icon.Close, context.Theme.QuietButton, false)) {
                        closeTriggered = true;
                        CloseTab(widget, tab, i);
                    }
                }

                Rect labelRect = tab.Closable ? new Rect(tabRect.x, tabRect.y, Mathf.Max(1f, tabRect.width - headerHeight), tabRect.height) : tabRect;
                // Capture the press before DoButton consumes it. A close-button
                // press is deliberately excluded from a possible reorder.
                if (widget.Reorderable && acceptsInput && rawType == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(labelRect)) {
                    state.BeginDrag(i, new MuiVec2(current.mousePosition.x, current.mousePosition.y));
                    activeTabs = state;
                }
                string title = FormatTabTitle(tab);
                if (!closeTriggered && DoButton(labelRect, title, context.Theme.TabButton, i == selected) && !state.Dragging) {
                    int captured = i;
                    if (captured != selected && widget.OnChange != null && !IsInputBlockedByDialog()) {
                        EnqueueCallback(delegate { widget.OnChange(captured); });
                    }
                }

                if (widget.CloseWithMiddleMouse && tab.Closable && acceptsInput && rawType == EventType.MouseDown && current.button == 2 && MouseOverVisibleRect(tabRect)) {
                    CloseTab(widget, tab, i);
                    current.Use();
                }

            }

            if (widget.Reorderable && current != null && rawType == EventType.MouseUp && current.button == 0 && state.DragFrom >= 0) {
                int from;
                int to;
                if (state.CompleteDrag(dropTarget, acceptsInput && widget.OnReorder != null, out from, out to)) {
                    EnqueueCallback(delegate { widget.OnReorder(from, to); });
                    if (current.type != EventType.Used) current.Use();
                }
            }

            float trailingX = rect.xMax - trailingWidth;
            for (int i = 0; i < trailingButtons.Count; i++) {
                float width = Mathf.Max(20f, trailingButtons[i].ButtonWidth);
                DrawTabItemButton(new Rect(trailingX, rect.y, width, headerHeight), trailingButtons[i], context);
                trailingX += width + 2f;
            }

            RenderLayoutChildren(node, context, offset);
        }

        private void RenderMenuBarAt(LayoutNode node, MenuBar widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "menubar");
            MenuBarState state = context.StateStore.GetOrCreate<MenuBarState>(id);
            state.Seen = true;
            bool ownerEnabled = state.PopupOwner.Capture(ownerAncestors, modelRootEnabled) && modelEnabled;
            if (!ownerEnabled || (state.Open && !RefreshMenuOwnerPath(widget, state))) {
                state.Open = false;
                state.OpenIndex = -1;
                state.PopupOwner.Invalidate();
            }
            bool beginOwner = false;
            Rect rect = OffsetRect(node.Rect, offset);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.FieldBg));
                DrawStrokeRect(rect, ToUnityColor(context.Theme.Tokens.Border), 1f);
            }

            float x = rect.x + 2f;
            for (int i = 0; i < widget.Children.Count; i++) {
                Menu menu = widget.Children[i] as Menu;
                if (menu == null) continue;

                float width = Mathf.Max(44f, context.Theme.Label.CalcSize(GetContent(menu.Title)).x + 20f);
                Rect menuRect = new Rect(x, rect.y + 2f, width, Mathf.Max(1f, rect.height - 4f));
                bool selected = state.Open && state.OpenIndex == i;
                bool oldEnabled = GUI.enabled;
                GUI.enabled = GUI.enabled && menu.Enabled;
                bool clicked = DoButton(menuRect, string.Empty, context.Theme.TabButton, selected);
                DrawStyle(GUI.enabled ? context.Theme.CenterLabel : disabledCenterLabel, menuRect, GetContent(menu.Title), false, false, false, false);
                if (clicked && !IsInputBlockedByDialog()) {
                    if (selected) {
                        state.Open = false;
                        state.OpenIndex = -1;
                        state.PopupOwner.Close();
                    } else {
                        context.CloseMenus();
                        context.HideOtherComboBoxes(null);
                        state.Open = true;
                        state.OpenIndex = i;
                        beginOwner = true;
                        state.Anchor = ToUnityRect(coordinates.ToScreen(ToMuiRect(menuRect)));
                        state.ModalDialog = currentDialog;
                        context.ActiveMenuBar = state;
                    }
                } else if (GUI.enabled && !context.SuppressInput && state.Open && Event.current != null && MouseOverVisibleRect(menuRect)) {
                    if (state.OpenIndex != i) { state.SubmenuPath.Clear(); beginOwner = true; }
                    state.OpenIndex = i;
                    state.Anchor = ToUnityRect(coordinates.ToScreen(ToMuiRect(menuRect)));
                }
                if (state.Open && state.OpenIndex == i) state.Anchor = ToUnityRect(coordinates.ToScreen(ToMuiRect(menuRect)));
                GUI.enabled = oldEnabled;
                x += width + 2f;
            }

            if (state.Open && !RefreshMenuOwnerPath(widget, state)) { state.Open = false; state.OpenIndex = -1; }
            if (state.Open && (beginOwner || !state.PopupOwner.Open)) state.PopupOwner.BeginOpen();
            if (state.Open && state.OpenIndex >= 0 && state.OpenIndex < widget.Children.Count) {
                Menu menu = widget.Children[state.OpenIndex] as Menu;
                if (menu != null) {
                    context.Overlay.Register(new OverlayRequest {
                        SortOrder = 1200,
                        Render = delegate(RenderContext renderContext) {
                            DrawPopup(renderContext, state.ModalDialog, delegate { DrawMenuPopup(renderContext, state, menu); }, state.PopupOwner);
                        }
                    });
                }
            }

            RegisterInputRegion(rect, true);
        }

        private void DrawMenuPopup(RenderContext context, MenuBarState state, Menu menu) {
            Event current = Event.current;
            if (!state.Open) return;
            if (!state.PopupOwner.IsEnabled) { state.Open = false; state.OpenIndex = -1; state.PopupOwner.Invalidate(); return; }
            for (int i = 0; i < state.SubmenuPath.Count; i++) {
                int index = state.SubmenuPath[i];
                Menu child = index >= 0 && index < menu.Children.Count ? menu.Children[index] as Menu : null;
                if (child == null) { state.SubmenuPath.RemoveRange(i, state.SubmenuPath.Count - i); break; }
                menu = child;
            }
            if (GUI.enabled && current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) {
                state.Open = false;
                state.OpenIndex = -1;
                current.Use();
                return;
            }

            bool hasBack = state.SubmenuPath.Count > 0;
            PopupLayout popup = PopupGeometry.Menu(ToMuiRect(state.Anchor), new MuiRect(0f, 0f, Screen.width, Screen.height),
                menu, hasBack, controlRowHeight, popupTextMeasure);
            state.PopupRect = ToUnityRect(popup.Bounds);
            if (!ReferenceEquals(state.PopupMenu, menu)) {
                state.PopupMenu = menu;
                state.ScrollPosition = Vector2.zero;
            }
            state.ScrollPosition.y = popup.ClampScroll(state.ScrollPosition.y);
            if (GUI.enabled && current != null && current.type == EventType.MouseDown && !MouseOverVisibleRect(state.PopupRect) && !MouseOverVisibleRect(state.Anchor)) {
                state.Open = false;
                state.OpenIndex = -1;
                current.Use();
                return;
            }

            DrawFilledRect(state.PopupRect, WithAlpha(ToUnityColor(context.Theme.Tokens.DropdownBg), context.Theme.WindowOpacity));
            DrawStrokeRect(state.PopupRect, ToUnityColor(context.Theme.Tokens.ControlBorder), 1f);
            GUI.BeginGroup(state.PopupRect);
            coordinates.PushGroup(ToMuiRect(state.PopupRect));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = GUI.enabled && menu.Enabled;
            try {
                if (popup.Scrollable) {
                    Rect viewport = new Rect(popup.ScrollViewport.X - popup.Bounds.X, popup.ScrollViewport.Y - popup.Bounds.Y,
                        popup.ScrollViewport.Width, popup.ScrollViewport.Height);
                    state.ScrollPosition = BeginScrollView(context, viewport, state.ScrollPosition,
                        new Rect(0f, 0f, popup.ContentViewport.Width, popup.ContentHeight), false, false);
                }
                try {
                    if (hasBack) {
                        Rect back = MenuPopupLocalRect(popup.Row(0, 0f), popup);
                        bool hover, active;
                        if (DoButton(back, string.Empty, null, false, out hover, out active))
                            state.SubmenuPath.RemoveAt(state.SubmenuPath.Count - 1);
                        DrawMenuRowBackground(back, context, hover, active);
                        DrawIcon(ToUnityRect(PopupGeometry.MenuIconBounds(ToMuiRect(back))), Mdl2Icon.ChevronLeft, NativeTextColor(context), 1f);
                        DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel, ToUnityRect(PopupGeometry.MenuTextBounds(ToMuiRect(back), 0f, false)),
                            GetContent("Back"), false, false, false, false);
                    }
                    float rowY = popup.ContentViewport.Y + (hasBack ? popup.RowHeight : 0f);
                    for (int i = 0; i < menu.Children.Count; i++) {
                        Widget child = menu.Children[i];
                        float rowHeight = PopupGeometry.MenuRowHeight(child, popup.RowHeight);
                        Rect row = MenuPopupLocalRect(new MuiRect(popup.ContentViewport.X, rowY, popup.ContentViewport.Width, rowHeight), popup);
                        rowY += rowHeight;
                        if (child == null) continue;
                        MenuItem item = child as MenuItem;
                        MenuSeparator separator = child as MenuSeparator;
                        Menu childMenu = child as Menu;
                        bool oldEnabled = GUI.enabled;
                        GUI.enabled = GUI.enabled && child.Enabled;
                        try {
                            if (item != null) {
                                bool hover, active;
                                bool clicked = DoButton(row, string.Empty, null, false, out hover, out active);
                                DrawMenuRowBackground(row, context, hover, active);
                                if (item.Checked) DrawIcon(ToUnityRect(PopupGeometry.MenuIconBounds(ToMuiRect(row))), Mdl2Icon.Check, NativeTextColor(context), 1.5f);
                                float shortcutWidth = string.IsNullOrEmpty(item.Shortcut) ? 0f : MeasurePopupText(item.Shortcut) + 16f;
                                DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                                    ToUnityRect(PopupGeometry.MenuTextBounds(ToMuiRect(row), shortcutWidth, false)), GetContent(item.Text), false, false, false, false);
                                if (shortcutWidth > 0f) DrawStyle(GUI.enabled ? popupShortcutStyle : disabledRightLabel,
                                    ToUnityRect(PopupGeometry.MenuShortcutBounds(ToMuiRect(row), shortcutWidth)), GetContent(item.Shortcut), false, false, false, false);
                                if (clicked && item.OnClick != null && !IsInputBlockedByDialog()) {
                                    Action action = item.OnClick;
                                    state.Open = false;
                                    state.OpenIndex = -1;
                                    state.PopupOwner.Close();
                                    EnqueueCallback(action, item);
                                }
                            } else if (separator != null) {
                                if (string.IsNullOrEmpty(separator.Text))
                                    DrawFilledRect(new Rect(row.x + 4f, row.y + 3f, Mathf.Max(0f, row.width - 8f), 1f), ToUnityColor(context.Theme.Tokens.Separator));
                                else DrawStyle(context.Theme.MutedLabel, ToUnityRect(PopupGeometry.Inset(ToMuiRect(row), 10f, 0f, 10f, 0f)),
                                    GetContent(separator.Text), false, false, false, false);
                            } else if (childMenu != null) {
                                bool hover, active;
                                bool clicked = DoButton(row, string.Empty, null, false, out hover, out active);
                                DrawMenuRowBackground(row, context, hover, active);
                                DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                                    ToUnityRect(PopupGeometry.MenuTextBounds(ToMuiRect(row), 0f, true)), GetContent(childMenu.Title), false, false, false, false);
                                DrawIcon(ToUnityRect(PopupGeometry.MenuIconBounds(ToMuiRect(row), true)), Mdl2Icon.ChevronRight, NativeTextColor(context), 1f);
                                if (clicked && !IsInputBlockedByDialog()) state.SubmenuPath.Add(i);
                            }
                        } finally { GUI.enabled = oldEnabled; }
                    }
                } finally { if (popup.Scrollable) EndScrollView(); }
            } finally {
                GUI.enabled = previousEnabled;
                coordinates.Pop();
                GUI.EndGroup();
            }

            RegisterInputRegion(state.PopupRect, state.Open, true);
        }

        private static Rect MenuPopupLocalRect(MuiRect rect, PopupLayout popup) {
            MuiRect origin = popup.Scrollable ? popup.ContentViewport : popup.Bounds;
            return new Rect(rect.X - origin.X, rect.Y - origin.Y, rect.Width, rect.Height);
        }

        private bool RefreshMenuOwnerPath(MenuBar widget, MenuBarState state) {
            Menu menu = state.OpenIndex >= 0 && state.OpenIndex < widget.Children.Count ? widget.Children[state.OpenIndex] as Menu : null;
            if (menu == null) { state.PopupOwner.Invalidate(); return false; }
            int first = ownerAncestors.Count;
            try {
                ownerAncestors.Add(menu);
                for (int i = 0; i < state.SubmenuPath.Count; i++) {
                    int index = state.SubmenuPath[i];
                    Menu child = index >= 0 && index < menu.Children.Count ? menu.Children[index] as Menu : null;
                    if (child == null) break;
                    menu = child;
                    ownerAncestors.Add(menu);
                }
                return state.PopupOwner.CapturePopupPath(ownerAncestors, first, ownerAncestors.Count - first);
            } finally { ownerAncestors.RemoveRange(first, ownerAncestors.Count - first); }
        }

        private static void DrawMenuRowBackground(Rect row, RenderContext context, bool hover, bool active) {
            DrawFilledRect(row, ToUnityColor(PopupPresentation.RowBackground(context.Theme.Tokens, GUI.enabled, hover, false, active)));
        }

        private float MeasurePopupText(string text) {
            return layoutTheme == null ? ThemeTypography.EstimateTextWidth(text, ThemeTypography.DefaultFontSize)
                : layoutTheme.Label.CalcSize(GetContent(text)).x;
        }

        private float MeasureTooltipTextHeight(string text, float width) {
            if (layoutTheme == null) return 20f;
            return Mathf.Max(0f, layoutTheme.HelpBox.CalcHeight(GetContent(text), width + PopupGeometry.TooltipHorizontalPadding * 2f) -
                PopupGeometry.TooltipVerticalPadding * 2f);
        }

        private void RenderSelectableAt(LayoutNode node, Selectable widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            bool additive = current != null && (current.control || current.command);
            bool range = current != null && current.shift;
            bool doubleClick = current != null && current.clickCount > 1;
            bool clicked = DoButton(rect, widget.ShowCheckbox ? string.Empty : widget.Text, context.Theme.NavigationItem, widget.Selected) || ConsumeActivation(widget, context);
            if (widget.ShowCheckbox) DrawSelectionControl(new Rect(rect.x + 8f, rect.y, Mathf.Max(0f, rect.width - 16f), rect.height),
                widget.Text, widget.Selected, false, context);
            if (clicked && !IsInputBlockedByDialog()) {
                bool captured = !widget.Selected;
                if (widget.OnSelect != null) {
                    SelectionRequest request = new SelectionRequest {
                        Value = widget.Value,
                        Selected = captured,
                        Additive = additive,
                        Range = range,
                        DoubleClick = doubleClick
                    };
                    EnqueueCallback(delegate { widget.OnSelect(request); });
                }

                if (widget.OnChange != null) {
                    EnqueueCallback(delegate { widget.OnChange(captured); });
                }

                if (doubleClick && widget.AllowDoubleClick && widget.OnDoubleClick != null) {
                    EnqueueCallback(widget.OnDoubleClick);
                }
            }
        }

        private void RenderTreeNodeAt(LayoutNode node, TreeNode widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, controlRowHeight));
            float arrowWidth = Mathf.Min(22f, headerRect.height);
            Rect arrowRect = new Rect(headerRect.x, headerRect.y, arrowWidth, headerRect.height);
            Rect labelRect = new Rect(headerRect.x + arrowWidth, headerRect.y, Mathf.Max(1f, headerRect.width - arrowWidth), headerRect.height);
            bool clicked = false;
            bool toggled = false;
            bool hover, active;
            if (!widget.Leaf && widget.OpenOnArrow) {
                bool arrowHover, arrowActive;
                toggled = DoButton(arrowRect, string.Empty, null, false, out arrowHover, out arrowActive);
                clicked = DoButton(labelRect, string.Empty, null, false, out hover, out active);
                hover = hover || arrowHover;
                active = active || arrowActive;
            } else {
                clicked = DoButton(headerRect, string.Empty, null, false, out hover, out active);
                toggled = !widget.Leaf && clicked;
            }
            DrawDisclosureChrome(headerRect, widget.Framed, widget.Selected, hover, active, context);
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel, labelRect, GetContent(widget.Label), false, false, false, false);
            if (!widget.Leaf)
                DrawIcon(arrowRect, widget.Open ? Mdl2Icon.ChevronDown : Mdl2Icon.ChevronRight, NativeTextColor(context), 1f);
            else if (widget.Bullet)
                DrawDisc(new Rect(arrowRect.center.x - 2f, arrowRect.center.y - 2f, 4f, 4f), NativeTextColor(context));

            Event current = Event.current;
            if (ConsumeActivation(widget, context)) {
                clicked = true;
                if (!widget.Leaf && !widget.OpenOnArrow) toggled = true;
            } else if (!widget.Leaf && HasNavigationKey(widget, context) &&
                (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.RightArrow)) {
                toggled = widget.Open != (current.keyCode == KeyCode.RightArrow);
                ConsumeKey();
            }
            if (!widget.Leaf && widget.OpenOnDoubleClick && clicked && current != null && current.clickCount > 1 && MouseOverVisibleRect(headerRect)) {
                toggled = true;
            }

            if (!IsInputBlockedByDialog()) {
                if (toggled && widget.OnOpenChange != null) {
                    bool open = !widget.Open;
                    EnqueueCallback(delegate { widget.OnOpenChange(open); });
                }
                if (clicked && widget.OnSelect != null) EnqueueCallback(widget.OnSelect);
            }

            if (widget.Open && !widget.Leaf) {
                int children = LayoutEngine.FlowChildCount(node);
                if (widget.DrawLines && children > 0 && Event.current != null && Event.current.type == EventType.Repaint) {
                    Rect last = OffsetRect(node.Children[children - 1].Rect, offset);
                    DrawGeometryLine(new MuiVec2(headerRect.x + 9f, headerRect.yMax),
                        new MuiVec2(headerRect.x + 9f, last.y + Mathf.Min(controlRowHeight, last.height) * 0.5f), context.Theme.Tokens.Separator, 1f);
                }
                RenderLayoutChildren(node, context, offset);
            }
        }

        private void RenderTreeLeafAt(LayoutNode node, TreeLeaf widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool hover, active;
            bool clicked = DoButton(rect, string.Empty, null, false, out hover, out active) || ConsumeActivation(widget, context);
            if (clicked && widget.OnSelect != null && !IsInputBlockedByDialog()) EnqueueCallback(widget.OnSelect);
            DrawDisclosureChrome(rect, widget.Framed, widget.Selected, hover, active, context);
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                new Rect(rect.x + 24f, rect.y, Mathf.Max(0f, rect.width - 24f), rect.height), GetContent(widget.Label), false, false, false, false);
            if (widget.Bullet) DrawDisc(new Rect(rect.x + 9f, rect.center.y - 2f, 4f, 4f), NativeTextColor(context));
        }

        private void RenderColorButtonAt(LayoutNode node, ColorButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, string.Empty, context.Theme.Button, false) || ConsumeActivation(widget, context);
            float swatchSize = Mathf.Min(rect.height - 4f, 22f);
            Rect swatchRect = new Rect(rect.x + 3f, rect.y + Mathf.Max(0f, (rect.height - swatchSize) * 0.5f), swatchSize, swatchSize);
            DrawColorSwatch(swatchRect, widget.Value, widget.ShowCheckerboard, context);

            string text = widget.Label;
            if (string.IsNullOrEmpty(text)) text = FormatColor(widget.Value, widget.ShowAlpha, ColorDisplayMode.Hex);
            DrawStyle(context.Theme.Label, new Rect(swatchRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.xMax - swatchRect.xMax - 9f), rect.height), GetContent(text), false, false, false, false);

            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderColorEdit3At(LayoutNode node, ColorEdit3 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorEditCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, false, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorEdit4At(LayoutNode node, ColorEdit4 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorEditCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, true, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorPicker3At(LayoutNode node, ColorPicker3 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorPickerCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, false, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorPicker4At(LayoutNode node, ColorPicker4 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorPickerCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, true, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorEditAt(LayoutNode node, ColorEdit widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorEditCore(OffsetRect(node.Rect, offset), string.Empty, widget.Value, widget.ShowAlpha, widget.DisplayMode, true, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private bool RenderColorEditCore(Rect rect, string label, MuiColor value, bool alpha, ColorDisplayMode displayMode, bool showPreview, RenderContext context, out MuiColor next, float fixedLabelWidth = 0f) {
            next = value;
            Rect contentRect = rect;
            if (!string.IsNullOrEmpty(label)) {
                float labelWidth = fixedLabelWidth > 0f ? fixedLabelWidth : Mathf.Min(context.Theme.Label.CalcSize(GetContent(label)).x + 8f, rect.width * 0.4f);
                Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
                DrawStyle(context.Theme.Label, labelRect, GetContent(label), false, false, false, false);
                contentRect = new Rect(labelRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.xMax - labelRect.xMax - 6f), rect.height);
            }

            if (showPreview) {
                float swatchWidth = Mathf.Min(32f, contentRect.width * 0.22f);
                Rect swatchRect = new Rect(contentRect.x, contentRect.y, swatchWidth, contentRect.height);
                DrawColorSwatch(swatchRect, value, true, context);
                contentRect = new Rect(swatchRect.xMax + 6f, contentRect.y, Mathf.Max(1f, contentRect.xMax - swatchRect.xMax - 6f), contentRect.height);
            }

            return RenderColorComponents(contentRect, value, alpha, displayMode, context, out next);
        }

        private bool RenderColorPickerCore(Rect rect, string label, MuiColor value, bool alpha, ColorDisplayMode displayMode, bool showPreview, RenderContext context, out MuiColor next) {
            next = value;
            DrawStyle(context.Theme.TextField, rect, GUIContent.none, false, false, false, false);

            float rowHeight = Mathf.Max(20f, Mathf.Min(controlRowHeight, (rect.height - 20f) / (alpha ? 5f : 4f)));
            Rect headerRect = new Rect(rect.x + 4f, rect.y + 4f, Mathf.Max(1f, rect.width - 8f), rowHeight);
            if (showPreview) {
                Rect swatchRect = new Rect(headerRect.x, headerRect.y, Mathf.Min(44f, headerRect.height * 1.8f), headerRect.height);
                DrawColorSwatch(swatchRect, value, true, context);
                string text = string.IsNullOrEmpty(label) ? FormatColor(value, alpha, displayMode) : label;
                DrawStyle(context.Theme.Label, new Rect(swatchRect.xMax + 6f, headerRect.y, Mathf.Max(1f, headerRect.xMax - swatchRect.xMax - 6f), headerRect.height), GetContent(text), false, false, false, false);
            } else {
                DrawStyle(context.Theme.Label, headerRect, GetContent(string.IsNullOrEmpty(label) ? FormatColor(value, alpha, displayMode) : label), false, false, false, false);
            }

            bool changed = false;
            int components = alpha ? 4 : 3;
            for (int i = 0; i < components; i++) {
                Rect rowRect = new Rect(rect.x + 4f, headerRect.yMax + 4f + i * (rowHeight + 3f), Mathf.Max(1f, rect.width - 8f), rowHeight);
                changed = RenderColorComponent(rowRect, i, next, displayMode, context, out next) || changed;
            }

            if (!alpha) next.A = 1f;
            return changed;
        }

        private bool RenderColorComponents(Rect rect, MuiColor value, bool alpha, ColorDisplayMode displayMode, RenderContext context, out MuiColor next) {
            next = value;
            int components = alpha ? 4 : 3;
            float gap = 3f;
            float width = Mathf.Max(1f, (rect.width - gap * (components - 1)) / components);
            bool changed = false;
            for (int i = 0; i < components; i++) {
                Rect componentRect = new Rect(rect.x + (width + gap) * i, rect.y, width, rect.height);
                changed = RenderColorComponent(componentRect, i, next, displayMode, context, out next) || changed;
            }
            if (!alpha) next.A = 1f;
            return changed;
        }

        private bool RenderColorComponent(Rect rect, int component, MuiColor value, ColorDisplayMode displayMode, RenderContext context, out MuiColor next) {
            next = value;
            float currentValue = GetColorComponent(value, component);
            bool changed = DoColorComponent(rect, component, currentValue, displayMode, context, out currentValue);
            if (changed) next = SetColorComponent(value, component, currentValue);
            return changed;
        }

        private void RenderVectorFieldAt(LayoutNode node, VectorField widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float labelWidth = string.IsNullOrEmpty(widget.Label) ? 0f : Mathf.Min(72f, rect.width * 0.35f);
            if (labelWidth > 0f) DrawStyle(context.Theme.Label, new Rect(rect.x, rect.y, labelWidth, rect.height), GetContent(widget.Label), false, false, false, false);

            int count = Mathf.Clamp(widget.Components, 2, 3);
            float startX = rect.x + labelWidth + (labelWidth > 0f ? 6f : 0f);
            float fieldWidth = Mathf.Max(1f, (rect.xMax - startX - (count - 1) * 4f) / count);
            RenderVectorComponent(context, new Rect(startX, rect.y, fieldWidth, rect.height), widget, 0);
            RenderVectorComponent(context, new Rect(startX + (fieldWidth + 4f), rect.y, fieldWidth, rect.height), widget, 1);
            if (count >= 3) RenderVectorComponent(context, new Rect(startX + (fieldWidth + 4f) * 2f, rect.y, fieldWidth, rect.height), widget, 2);
        }

        private void RenderVectorComponent(RenderContext context, Rect rect, VectorField widget, int index) {
            float value = index == 0 ? widget.X : (index == 1 ? widget.Y : widget.Z);
            string label = (index == 0 ? "X " : (index == 1 ? "Y " : "Z ")) + value.ToString("0.##");
            bool clicked = DoButton(rect, label, context.Theme.TextField, false);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float x = widget.X;
                float y = widget.Y;
                float z = widget.Z;
                if (index == 0) x += 0.1f;
                else if (index == 1) y += 0.1f;
                else z += 0.1f;
                EnqueueCallback(delegate { widget.OnChange(x, y, z); });
            }
        }

        private void RenderPlotLinesAt(LayoutNode node, PlotLines widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.FieldBg));
            DrawStrokeRect(rect, ToUnityColor(context.Theme.Tokens.Border), context.Theme.Tokens.BorderWidth);
            if (widget.Values != null && widget.Values.Count > 0 && Event.current != null && Event.current.type == EventType.Repaint) {
                MuiRect plot = PlotGeometry.ContentRect(ToMuiRect(rect));
                float min, max;
                PlotGeometry.Range(widget.Values, widget.HasRange, widget.Min, widget.Max, out min, out max);
                MuiVec2 previous = new MuiVec2();
                bool hasPrevious = false;
                MuiColor color = GUI.enabled ? context.Theme.Tokens.Accent : context.Theme.Tokens.TextDisabled;
                for (int i = 0; i < widget.Values.Count; i++) {
                    MuiVec2 point;
                    if (!PlotGeometry.TryPoint(widget.Values, i, plot, min, max, out point)) { hasPrevious = false; continue; }
                    if (hasPrevious) DrawGeometryLine(previous, point, color, 1.5f);
                    else if (widget.Values.Count == 1) DrawDisc(new Rect(point.X - 1.5f, point.Y - 1.5f, 3f, 3f), color);
                    previous = point;
                    hasPrevious = true;
                }
            }
            DrawPlotOverlay(rect, widget.OverlayText, context);
        }

        private void RenderPlotHistogramAt(LayoutNode node, PlotHistogram widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.FieldBg));
            DrawStrokeRect(rect, ToUnityColor(context.Theme.Tokens.Border), context.Theme.Tokens.BorderWidth);
            if (widget.Values != null && widget.Values.Count > 0 && Event.current != null && Event.current.type == EventType.Repaint) {
                MuiRect plot = PlotGeometry.ContentRect(ToMuiRect(rect));
                float min, max;
                PlotGeometry.Range(widget.Values, widget.HasRange, widget.Min, widget.Max, out min, out max);
                float barWidth = plot.Width / widget.Values.Count;
                MuiColor color = GUI.enabled ? context.Theme.Tokens.Accent : context.Theme.Tokens.TextDisabled;
                for (int i = 0; i < widget.Values.Count; i++) {
                    MuiVec2 point;
                    if (!PlotGeometry.TryPoint(widget.Values, i, plot, min, max, out point)) continue;
                    DrawFilledRect(new Rect(plot.X + i * barWidth, point.Y, Mathf.Max(0f, barWidth - Mathf.Min(1f, barWidth * 0.1f)), plot.Bottom - point.Y), ToUnityColor(color));
                }
            }
            DrawPlotOverlay(rect, widget.OverlayText, context);
        }

        private void DrawPlotOverlay(Rect rect, string text, RenderContext context) {
            if (string.IsNullOrEmpty(text) || Event.current == null || !GuiEventPolicy.ShouldPaint(Event.current.type)) return;
            MuiRect backing = PlotGeometry.OverlayBounds(ToMuiRect(rect), MeasurePopupText(text),
                ThemeTypography.LineHeight(context.Theme.Tokens, LabelRole.Body));
            DrawFilledRect(ToUnityRect(backing), ToUnityColor(context.Theme.Tokens.FieldBg));
            DrawStyle(GUI.enabled ? context.Theme.CenterLabel : disabledCenterLabel, rect, GetContent(text), false, false, false, false);
        }

        private void RenderDrawCanvasAt(LayoutNode node, DrawCanvas widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                if (widget.DrawBackground) {
                    DrawStyle(context.Theme.TextField, rect, GUIContent.none, false, false, false, false);
                }

                DrawList drawList = new DrawList();
                MuiRect canvasRect = ToMuiRect(rect);
                if (widget.Clip) drawList.PushClip(canvasRect);
                if (widget.OnDraw != null) widget.OnDraw(drawList, canvasRect);
                if (widget.Clip) drawList.PopClip();
                Paint(drawList);
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderDrawListWidgetAt(LayoutNode node, DrawListWidget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                if (widget.DrawBackground) {
                    DrawStyle(context.Theme.TextField, rect, GUIContent.none, false, false, false, false);
                }

                DrawList drawList = new DrawList();
                MuiRect canvasRect = ToMuiRect(rect);
                if (widget.Clip) drawList.PushClip(canvasRect);
                if (widget.Source != null) {
                    drawList.Append(widget.Source, widget.LocalCoordinates ? new MuiVec2(canvasRect.X, canvasRect.Y) : new MuiVec2(0f, 0f));
                }
                if (widget.Clip) drawList.PopClip();
                Paint(drawList);
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderVirtualListAt(LayoutNode node, VirtualList widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "virtual-list");
            VirtualListState state = context.StateStore.GetOrCreate<VirtualListState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            float rowHeight = Mathf.Max(1f, widget.RowHeight);
            ScrollFrameLayout frame = ScrollGeometry.VerticalFrame(ToMuiRect(rect), Math.Max(0, widget.ItemCount) * rowHeight, widget.AlwaysShowVertical);
            Rect viewRect = ToUnityRect(frame.Content);
            DrawFilledRect(rect, ToUnityColor(context.Theme.Tokens.FieldBg));
            Vector2 before = widget.ScrollPosition == null ? new Vector2(0f, frame.ClampY(state.Position.y)) : widget.ScrollPosition.Resolve(frame.Viewport, frame.Content, false, frame.Vertical);
            Vector2 requested = before;
            float keyboardScroll;
            if (HasNavigationKey(widget, context) && KeyboardNavigation.ScrollVertical(before.y, rowHeight,
                frame.ContentViewport.Height, frame.Content.Height, Event.current.keyCode, out keyboardScroll)) {
                requested.y = keyboardScroll;
                ConsumeKey();
            }
            state.Position = BeginScrollView(context, ToUnityRect(frame.Viewport), requested, viewRect, false, frame.Vertical);
            if (widget.ScrollPosition != null) widget.ScrollPosition.Publish(before, state.Position, GUI.enabled && !IsInputBlockedByDialog());
            try {
                int first;
                int last;
                ScrollGeometry.VisibleRows(state.Position.y, frame.ContentViewport.Height, rowHeight, widget.ItemCount, out first, out last);
                bool cache = context.CacheLayout && widget.CacheItems;
                if (cache) state.Rows.BeginRange(widget, first, last, contentRevision, layoutEngine);

                for (int i = first; i < last; i++) {
                    string oldPath = context.Path;
                    context.Path = node.Path + ".row" + i.ToString();
                    try {
                        MuiRect rowRect = new MuiRect(0f, i * rowHeight, viewRect.width, rowHeight);
                        LayoutNode rowNode;
                        if (cache) {
                            rowNode = state.Rows.GetRow(i, rowRect, context.Path);
                        } else {
                            Widget rowWidget = widget.BuildItem == null ? null : widget.BuildItem(i);
                            rowNode = rowWidget == null || rowWidget is EmptyWidget ? null : layoutEngine.Layout(rowWidget, rowRect, context.Path);
                        }
                        RenderLayoutNode(rowNode, context, Vector2.zero);
                    } catch (Exception ex) {
                        Debug.LogException(ex);
                    } finally {
                        context.Path = oldPath;
                    }
                }
            } finally {
                EndScrollView();
            }

            DrawScrollFrame(frame.Bounds, context);
            RegisterInputRegion(rect, true);
        }

        private void RenderComboBoxAt(LayoutNode node, Widget widget, IComboBoxWidget combo, RenderContext context, Vector2 offset) {
            string label = combo.ComboLabel ?? string.Empty;
            string id = context.StateId(widget, "combo");
            ComboBoxState state = context.StateStore.GetOrCreate<ComboBoxState>(id);
            bool ownerEnabled = state.PopupOwner.Capture(ownerAncestors, modelRootEnabled) && modelEnabled;
            if (!ownerEnabled) {
                if (state.Dropdown != null) state.Dropdown.Hide();
                state.Open = false;
                state.PopupOwner.Invalidate();
            }
            if (state.Dropdown != null && !ReferenceEquals(state.Dropdown.Theme, context.Theme)) {
                state.Dropdown.Hide();
                state.Dropdown = null;
                state.Open = false;
            }
            state.ModalDialog = currentDialog;
            if (string.IsNullOrEmpty(state.ControlName)) state.ControlName = id;
            if (string.IsNullOrEmpty(state.FilterControlName)) state.FilterControlName = id + ":filter";

            Rect rect = OffsetRect(node.Rect, offset);
            Rect buttonRect = rect;
            if (label.Length > 0) {
                float labelWidth = combo.ComboLabelWidth > 0f ? combo.ComboLabelWidth : Mathf.Min(72f, rect.width * 0.4f);
                Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
                DrawStyle(context.Theme.Label, labelRect, GetContent(label), false, false, false, false);
                buttonRect = new Rect(labelRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.width - labelWidth - 6f), rect.height);
            }

            string selectedText = combo.GetComboText(combo.ComboValue);
            GUI.SetNextControlName(state.ControlName);
            bool hover, active;
            bool clicked = DoButton(buttonRect, string.Empty, null, false, out hover, out active) || (!state.Open && ConsumeActivation(widget, context));
            bool focused = context.Navigation != null && context.Navigation.IsKeyboardFocused(widget, node.Path);
            ThemeTokens colors = context.Theme.Tokens;
            DrawFilledRect(buttonRect, ToUnityColor(!GUI.enabled ? colors.ButtonDisabledBg : state.Open || focused || active ? colors.FieldActiveBg : hover ? colors.FieldHoverBg : colors.FieldBg));
            DrawStrokeRect(buttonRect, ToUnityColor(Mdl2ControlChrome.FieldBorderColor(colors, GUI.enabled, hover, state.Open || focused)), 2f);
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                new Rect(buttonRect.x + 8f, buttonRect.y, Mathf.Max(0f, buttonRect.width - 40f), buttonRect.height), GetContent(selectedText), false, false, false, false);
            DrawIcon(new Rect(buttonRect.xMax - 28f, buttonRect.y, 24f, buttonRect.height), Mdl2Icon.ChevronDown, NativeTextColor(context), 1f);
            if (IsUsableLayoutRect(buttonRect) && coordinates.IsVisible(ToMuiRect(buttonRect))) {
                state.ButtonRect = ToComboButtonStateRect(buttonRect);
                state.HasButtonRect = true;
            }

            if (ownerEnabled && clicked && !IsInputBlockedByDialog()) {
                if (state.Dropdown != null && state.Dropdown.Show) {
                    state.Dropdown.Hide();
                    state.Open = false;
                } else {
                    OpenComboOverlay(widget, combo, context, state, true);
                }
            } else if (ownerEnabled && state.Dropdown != null && state.Dropdown.Show && state.HasButtonRect) {
                OpenComboOverlay(widget, combo, context, state, false);
            }
        }

        private void OpenComboOverlay(Widget widget, IComboBoxWidget combo, RenderContext context, ComboBoxState state, bool focus) {
            if (!state.PopupOwner.IsEnabled || (focus && !state.PopupOwner.BeginOpen())) return;
            if (focus && popupDepth == 0) context.CloseMenus();
            ISearchableComboBoxWidget searchable = combo as ISearchableComboBoxWidget;
            string filter = state.FilterText ?? string.Empty;
            if (!ReferenceEquals(state.Owner, combo)) {
                state.Owner = combo;
                state.Formatter = combo.GetComboText;
            }
            state.Choices.Refresh(combo, combo.ComboOptions, state.Formatter, searchable == null ? string.Empty : filter,
                contentRevision, context.CacheLayout);

            string id = context.StateId(widget, "combo");
            if (state.Dropdown == null) {
                state.Dropdown = new DropdownOverlay(StableHash(id), context.Theme.FontSize, context.Theme);
            }

            Rect buttonRect = state.HasButtonRect ? state.ButtonRect : new Rect(0f, 0f, 1f, 1f);
            state.WindowStateId = currentWindowStateId;
            state.Items = state.Choices.Labels;
            state.MaxVisibleItems = combo.ComboMaxVisibleItems;
            state.Searchable = searchable != null;
            state.FilterPlaceholder = searchable == null ? string.Empty : searchable.ComboFilterPlaceholder;
            if (state.Callback == null) state.Callback = delegate(int index) {
                if (!state.PopupOwner.IsEnabled || index < 0 || index >= state.Choices.Values.Count) return;
                Delegate callback = state.Owner.ComboOnChange;
                if (callback == null) return;
                object selected = state.Choices.Values[index];
                state.PopupOwner.Close();
                EnqueueCallback(state.PopupOwner.Guard(delegate { callback.DynamicInvoke(selected); }));
            };

            context.HideOtherComboBoxes(state);
            state.Open = true;
            int selectedIndex = 0;
            if (focus) {
                for (int i = 0; i < state.Choices.Values.Count; i++) {
                    if (object.Equals(state.Choices.Values[i], combo.ComboValue)) { selectedIndex = i; break; }
                }
            }
            if (searchable != null) {
                string placeholder = state.FilterPlaceholder;
                if (state.FilterChanged == null) state.FilterChanged = delegate(string value) {
                    if (state.PopupOwner.IsEnabled) state.FilterText = value ?? string.Empty;
                };
                state.Dropdown.Set(GetTopWindowRect(), buttonRect, state.Items, state.Callback, state.MaxVisibleItems, state.FilterText, state.FilterChanged, placeholder);
                if (focus) state.Dropdown.Focus(state.FilterControlName, selectedIndex);
            } else {
                state.Dropdown.Set(GetTopWindowRect(), buttonRect, state.Items, state.Callback, state.MaxVisibleItems);
                if (focus) state.Dropdown.Focus(state.ControlName, selectedIndex);
            }
            context.RegisterComboBox(state);
        }

        private Rect GetTopWindowRect() {
            if (string.IsNullOrEmpty(currentWindowStateId) || currentContext == null) return new Rect(0f, 0f, 0f, 0f);
            WindowState state = currentContext.StateStore.GetOrCreate<WindowState>(currentWindowStateId);
            return state.Rect;
        }

        private void RenderDialog(Dialog widget, RenderContext context) {
            string id = context.StateId(widget, "dialog");
            string dialogPath = context.Path;
            PopupOwnerState owner = context.StateStore.GetOrCreate<PopupOwnerState>(id + ":owner");
            owner.Capture(ownerAncestors, modelRootEnabled);
            context.Overlay.Register(new OverlayRequest {
                SortOrder = 1000,
                ModalDialog = widget,
                Render = delegate(RenderContext renderContext) {
                    DrawDialogOverlay(renderContext, widget, id, dialogPath, true, delegate {
                        if (widget.OnClose != null) EnqueueCallback(widget.OnClose);
                    }, owner);
                }
            });
        }

        private void RegisterServiceDialogs(RenderContext context) {
            if (context.Dialogs == null) return;

            for (int i = 0; i < context.Dialogs.Dialogs.Count; i++) {
                Dialog dialog = context.Dialogs.Dialogs[i];
                int index = i;
                string id = context.MountId + ":service-dialog:" + index;
                context.Overlay.Register(new OverlayRequest {
                    SortOrder = 1100 + index,
                    ModalDialog = dialog,
                    Render = delegate(RenderContext renderContext) {
                        DrawDialogOverlay(renderContext, dialog, id, id, true, delegate {
                            renderContext.Dialogs.Close(dialog);
                        });
                    }
                });
            }
        }

        private void RegisterDragDropPreview(RenderContext context) {
            if (context == null || context.DragDrop == null || !context.DragDrop.Active || context.DragDrop.Payload == null) return;

            DragPayload payload = context.DragDrop.Payload;
            Vector2 position = context.DragDrop.CurrentPosition;
            context.Overlay.Register(new OverlayRequest {
                SortOrder = 990,
                Render = delegate(RenderContext renderContext) {
                    DrawDragDropPreview(renderContext, payload, position);
                }
            });
        }

        private void DrawDragDropPreview(RenderContext context, DragPayload payload, Vector2 position) {
            string text = payload == null ? string.Empty : payload.PreviewText;
            if (string.IsNullOrEmpty(text) && payload != null) text = payload.Type;
            if (string.IsNullOrEmpty(text)) text = "Dragging";

            float width = Mathf.Min(260f, Mathf.Max(80f, text.Length * context.Theme.FontSize * 0.62f + 18f));
            float height = Mathf.Max(22f, context.Theme.FontSize + 10f);
            Rect rect = ClampOverlayRect(new Rect(position.x + 12f, position.y + 12f, width, height));
            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                DrawStyle(context.Theme.Dropdown, rect, GetContent(text), false, false, false, false);
            } finally {
                GUI.color = oldColor;
            }
            RegisterInputRegion(rect, true);
        }

        private static bool DragPayloadMatches(DragPayload payload, string acceptedType) {
            if (payload == null) return false;
            if (string.IsNullOrEmpty(acceptedType)) return true;
            return string.Equals(payload.Type, acceptedType, StringComparison.Ordinal);
        }

        private void CompleteDragDropFrame(RenderContext context) {
            if (context == null || context.DragDrop == null || !context.DragDrop.Active) return;

            Event current = Event.current;
            if (current != null && current.type == EventType.MouseUp && current.button == 0) {
                context.DragDrop.Clear();
            }
        }

        private void DrawDialogOverlay(RenderContext context, Dialog dialog, string stateId, string dialogPath, bool drawBackdrop, Action close, PopupOwnerState owner = null) {
            DialogState state = context.StateStore.GetOrCreate<DialogState>(stateId);
            if (!state.Initialized) {
                state.Rect = CenterRect(new Rect(0f, 0f, dialog.Width, dialog.Height));
                state.Initialized = true;
            }

            state.Rect.width = dialog.Width;
            state.Rect.height = dialog.Height;
            state.Rect = ClipDialogPosition(state.Rect);

            bool acceptsInput = ReferenceEquals(context.InputDialog, dialog);
            if (drawBackdrop) {
                Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
                DrawFilledRect(screenRect, ToUnityColor(context.Theme.Tokens.Backdrop));
                RegisterInputRegion(screenRect, true);
            }

            Event current = Event.current;
            if (acceptsInput && current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && dialog.CloseOnEscape) {
                if (close != null) close();
                current.Use();
            }

            if (acceptsInput && current != null && current.type == EventType.MouseDown && dialog.CloseOnOutsideClick) {
                Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!state.Rect.Contains(mousePosition)) {
                    if (close != null) close();
                    current.Use();
                }
            }

            string oldPath = context.Path;
            Dialog oldDialog = currentDialog;
            Vector2 oldContentOffset = currentWindowContentOffset;
            bool oldEnabled = GUI.enabled;
            bool oldModelEnabled = modelEnabled;
            bool oldRootEnabled = modelRootEnabled;
            int oldAncestorCount = ownerAncestors.Count;
            if (owner != null) {
                owner.AppendAncestorsTo(ownerAncestors);
                modelRootEnabled = owner.RootEnabled;
                modelEnabled = owner.IsEnabled;
            } else {
                ownerAncestors.Add(dialog);
                modelEnabled = modelRootEnabled && dialog.Enabled;
            }
            context.Path = dialogPath;
            currentDialog = dialog;
            GUI.enabled = GuiEventPolicy.ControlEnabled(oldEnabled, modelEnabled, !acceptsInput || context.SuppressInput,
                current == null ? EventType.Layout : current.type);
            dialogDepth++;
            try {
                DrawDialogFrame(context, dialog, state, stateId);
                RectOffset padding = context.Theme.DialogContentPadding;
                currentWindowContentOffset = new Vector2(padding.left, padding.top);
                LayoutNode root = Layout(dialog, ToMuiRect(state.Rect), dialogPath, context);
                RenderLayoutChildren(root, context, Vector2.zero);
            } finally {
                dialogDepth--;
                currentDialog = oldDialog;
                currentWindowContentOffset = oldContentOffset;
                GUI.enabled = oldEnabled;
                modelEnabled = oldModelEnabled;
                modelRootEnabled = oldRootEnabled;
                ownerAncestors.RemoveRange(oldAncestorCount, ownerAncestors.Count - oldAncestorCount);
                context.Path = oldPath;
            }

            state.Rect = ClipDialogPosition(state.Rect);
            RegisterInputRegion(state.Rect, true);
        }

        private void DrawDialogFrame(RenderContext context, Dialog dialog, DialogState state, string stateId) {
            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    if (context.Theme.Dialog.fontSize == context.Theme.Heading.fontSize) {
                        DrawStyle(context.Theme.Dialog, state.Rect, GUIContent.none, false, false, false, false);
                        DrawStyle(context.Theme.Heading, ToUnityRect(ThemeTypography.DialogTitleBounds(ToMuiRect(state.Rect), context.Theme.Tokens)),
                            GetContent(dialog.Title), false, false, false, false);
                    } else {
                        // Legacy themes keep their compact window-style title.
                        DrawStyle(context.Theme.Dialog, state.Rect, GetContent(dialog.Title), false, false, false, false);
                    }
                }
            } finally {
                GUI.color = oldColor;
            }

            if (!IsInputBlockedByDialog()) DoDrag(state, StableHash(stateId + ":drag"), new Rect(state.Rect.x, state.Rect.y, state.Rect.width, 24f));
        }

        private static Rect CenterRect(Rect rect) {
            rect.x = Mathf.Max(0f, (Screen.width - rect.width) * 0.5f);
            rect.y = Mathf.Max(0f, (Screen.height - rect.height) * 0.5f);
            return rect;
        }

        private static Rect ClipDialogPosition(Rect rect) {
            rect.width = Mathf.Min(rect.width, Mathf.Max(120f, Screen.width - 20f));
            rect.height = Mathf.Min(rect.height, Mathf.Max(60f, Screen.height - 20f));
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private static Rect ClampOverlayRect(Rect rect) {
            rect.width = Mathf.Min(rect.width, Mathf.Max(20f, Screen.width - 12f));
            rect.height = Mathf.Min(rect.height, Mathf.Max(20f, Screen.height - 12f));
            rect.x = Mathf.Clamp(rect.x, 4f, Mathf.Max(4f, Screen.width - rect.width - 4f));
            rect.y = Mathf.Clamp(rect.y, 4f, Mathf.Max(4f, Screen.height - rect.height - 4f));
            return rect;
        }

        private bool PreparePopupInput(RenderContext context) {
            Event current = Event.current;
            bool mayConsume = !context.SuppressInput && context.OwnsKeyboardInput;
            if (!context.OwnsKeyboardInput || context.SuppressDialogs) {
                context.CloseMenus();
                context.HideOtherComboBoxes(null);
                return false;
            }
            bool open = false;
            bool invalidOwner = false;
            for (int i = 0; i < context.ComboBoxStates.Count; i++) {
                ComboBoxState state = context.ComboBoxStates[i];
                if (state.Dropdown == null || !state.Dropdown.Show) continue;
                if (!state.PopupOwner.IsEnabled) {
                    state.Dropdown.Hide();
                    state.Open = false;
                    state.PopupOwner.Invalidate();
                    invalidOwner = true;
                    continue;
                }
                bool dismiss = context.SuppressDialogs || (current != null &&
                    ((current.type == EventType.MouseDown && !MouseOverVisibleRect(state.Dropdown.Rect)) ||
                     (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Escape || current.keyCode == KeyCode.Tab))));
                if (dismiss) {
                    state.Dropdown.Hide();
                    state.Open = false;
                    state.PopupOwner.Invalidate();
                    if (mayConsume && current != null && (current.type == EventType.MouseDown || current.type == EventType.KeyDown)) {
                        current.Use();
                        Input.ResetInputAxes();
                    }
                } else open = true;
            }
            if (open) return true;
            ContextMenuState contextMenu = context.ActiveContextMenu;
            MenuBarState menuBar = context.ActiveMenuBar;
            if ((contextMenu != null && contextMenu.Open && !contextMenu.PopupOwner.IsEnabled) ||
                (menuBar != null && menuBar.Open && !menuBar.PopupOwner.IsEnabled)) {
                context.CloseMenus();
                invalidOwner = true;
            }
            if (invalidOwner) CancelInput();
            bool menuOpen = (contextMenu != null && contextMenu.Open) || (menuBar != null && menuBar.Open);
            if (!menuOpen) return invalidOwner;
            Rect rect = contextMenu != null && contextMenu.Open ? contextMenu.Rect : menuBar.PopupRect;
            bool dismissMenu = current != null &&
                ((current.type == EventType.MouseDown && !MouseOverVisibleRect(rect) &&
                  (menuBar == null || !menuBar.Open || !MouseOverVisibleRect(menuBar.Anchor))) ||
                 (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Escape || current.keyCode == KeyCode.Tab)));
            if (dismissMenu) {
                context.CloseMenus();
                if (mayConsume) { current.Use(); Input.ResetInputAxes(); }
                return false;
            }
            return true;
        }

        private void DrawPopup(RenderContext context, Dialog owner, Action draw, PopupOwnerState popupOwner = null) {
            if (context.IsModalOpen && !ReferenceEquals(owner, context.InputDialog)) return;
            bool oldBlocked = popupBlocksInput;
            bool oldEnabled = GUI.enabled;
            Dialog oldDialog = currentDialog;
            int oldDepth = dialogDepth;
            PopupOwnerState oldPopupOwner = currentPopupOwner;
            bool oldModelEnabled = modelEnabled;
            bool oldRootEnabled = modelRootEnabled;
            int oldAncestorCount = ownerAncestors.Count;
            currentPopupOwner = popupOwner;
            if (popupOwner != null) {
                popupOwner.AppendAncestorsTo(ownerAncestors);
                modelRootEnabled = popupOwner.RootEnabled;
                modelEnabled = popupOwner.IsEnabled;
            }
            popupBlocksInput = context.HasOpenDropdown;
            currentDialog = owner;
            dialogDepth = owner == null ? 0 : 1;
            GUI.enabled = GuiEventPolicy.ControlEnabled(oldEnabled, modelEnabled, context.SuppressInput || popupBlocksInput,
                Event.current == null ? EventType.Layout : Event.current.type);
            popupDepth++;
            try { draw(); }
            finally {
                popupDepth--;
                popupBlocksInput = oldBlocked;
                currentDialog = oldDialog;
                dialogDepth = oldDepth;
                GUI.enabled = oldEnabled;
                currentPopupOwner = oldPopupOwner;
                modelEnabled = oldModelEnabled;
                modelRootEnabled = oldRootEnabled;
                ownerAncestors.RemoveRange(oldAncestorCount, ownerAncestors.Count - oldAncestorCount);
            }
        }

        private void EnqueueCallback(Action callback, Widget actionSource = null) {
            if (callback == null) return;
            MaximumUIRuntime.EnqueueCallback(currentPopupOwner == null ? callback : currentPopupOwner.Guard(callback, actionSource ?? currentWidget));
        }

        private bool ConsumeActivation(Widget widget, RenderContext context) {
            Event current = Event.current;
            if (!HasNavigationKey(widget, context) ||
                !KeyboardNavigation.IsActivation(current.keyCode, current.control, current.alt, current.command)) return false;
            CommitFocusedText();
            GUI.FocusControl(null);
            ConsumeKey();
            return true;
        }

        private bool HasNavigationKey(Widget widget, RenderContext context) {
            Event current = Event.current;
            return GUI.enabled && !IsInputBlockedByDialog() && context.OwnsKeyboardInput &&
                current != null && current.type == EventType.KeyDown && !current.control && !current.alt && !current.command &&
                context.Navigation != null && context.Navigation.IsKeyboardFocused(widget, context.Path);
        }

        private static void ConsumeKey() {
            Event current = Event.current;
            current.Use();
            Input.ResetInputAxes();
        }

        private void DoWindowResize(RenderContext context, Window widget, WindowState state) {
            Rect grip = new Rect(state.Rect.xMax - 14f, state.Rect.yMax - 14f, 14f, 14f);
            int control = GUIUtility.GetControlID(StableHash(context.StateId(widget, "window-resize")), FocusType.Passive, grip);
            Event current = Event.current;
            if (current != null) {
                if (state.Resizing && GuiEventPolicy.ShouldReleasePointer(current.rawType, current.button)) {
                    state.Resizing = false;
                    ReleasePointer(control);
                    if (current.type != EventType.Used) current.Use();
                } else if (GUI.enabled && !IsInputBlockedByDialog()) {
                    if (current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(grip)) {
                        state.Resizing = true;
                        resizingWindow = state;
                        state.ResizeStart = state.Rect;
                        state.ResizeMouseStart = current.mousePosition;
                        CapturePointer(control);
                        current.Use();
                    } else if (state.Resizing && GUIUtility.hotControl == control && current.type == EventType.MouseDrag) {
                        Vector2 delta = current.mousePosition - state.ResizeMouseStart;
                        state.Rect = ToUnityRect(WindowGeometry.Resize(ToMuiRect(state.ResizeStart), new MuiVec2(delta.x, delta.y),
                            Screen.width, Screen.height, widget.MinWidth, widget.MinHeight));
                        state.ExpandedHeight = state.Rect.height;
                        current.Use();
                    }
                }
            }
            Color color = ToUnityColor(context.Theme.Tokens.TextMuted);
            DrawFilledRect(new Rect(grip.xMax - 8f, grip.yMax - 3f, 6f, 1f), color);
            DrawFilledRect(new Rect(grip.xMax - 3f, grip.yMax - 8f, 1f, 6f), color);
        }

        private bool IsInputBlockedByDialog() {
            return popupBlocksInput || (currentContext != null && currentContext.SuppressInput) || currentContext != null && currentContext.IsModalOpen &&
                (dialogDepth == 0 || !ReferenceEquals(currentDialog, currentContext.InputDialog));
        }

        private static bool HasDialogWidget(Widget widget) {
            return WidgetVisibility.ContainsDialog(widget);
        }

        private static float GetSliderHeight(RenderContext context) {
            float height = 12f;
            if (context != null && context.Theme != null) {
                height = Mathf.Max(height, context.Theme.FontSize);
                if (context.Theme.HorizontalSlider.fixedHeight > 0f) height = Mathf.Max(height, context.Theme.HorizontalSlider.fixedHeight);
                if (context.Theme.HorizontalSliderThumb.fixedHeight > 0f) height = Mathf.Max(height, context.Theme.HorizontalSliderThumb.fixedHeight);
            }
            return height;
        }

        private static GUIContent GetContent(string text) {
            TempContent.text = text ?? string.Empty;
            TempContent.tooltip = string.Empty;
            TempContent.image = null;
            return TempContent;
        }

        private bool MouseOverVisibleRect(Rect rect) {
            Event current = Event.current;
            return current != null && coordinates.ContainsLocalPoint(ToMuiRect(rect),
                new MuiVec2(current.mousePosition.x, current.mousePosition.y));
        }

        private bool DoButton(Rect rect, string text, GUIStyle style, bool on) {
            bool hover, active;
            return DoButton(rect, text, style, on, out hover, out active);
        }

        private bool DoButton(Rect rect, string text, GUIStyle style, bool on, out bool hover, out bool active, bool preserveTextFocus = false) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            bool acceptsInput = GUI.enabled && !IsInputBlockedByDialog();
            hover = acceptsInput && current != null && MouseOverVisibleRect(rect);
            active = acceptsInput && GUIUtility.hotControl == controlId;
            bool clicked = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    // Commit a prior text field on press; button callbacks run on
                    // release. Native scrollbar capture does not use this path.
                    if (GuiEventPolicy.ShouldBlurTextOnPress(current.type, current.button, acceptsInput, hover, preserveTextFocus))
                    {
                        CommitFocusedText();
                        GUI.FocusControl(null);
                    }
                    CapturePointer(controlId);
                    active = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    ReleasePointer(controlId);
                    clicked = hover;
                    active = false;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (style != null && (current == null || current.type == EventType.Repaint)) {
                bool themedButton = layoutTheme != null && (ReferenceEquals(style, layoutTheme.Button) || ReferenceEquals(style, layoutTheme.PrimaryButton) ||
                    ReferenceEquals(style, layoutTheme.QuietButton) || ReferenceEquals(style, layoutTheme.DangerButton));
                if (!GUI.enabled && themedButton) {
                    if (!ReferenceEquals(style, layoutTheme.QuietButton)) DrawFilledRect(rect, ToUnityColor(layoutTheme.Tokens.ButtonDisabledBg));
                    // Draw the explicit disabled colors once; Unity's generic disabled
                    // tint must not turn an accent button into a faded active button.
                    GUI.enabled = true;
                    try { DrawStyle(disabledButtonText, rect, GetContent(text), false, false, false, false); }
                    finally { GUI.enabled = false; }
                } else DrawStyle(style, rect, GetContent(text), hover, active, on, GUIUtility.keyboardControl == controlId);
            }

            return clicked;
        }

        private bool DoIconButton(Rect rect, Mdl2Icon icon, GUIStyle style, bool on) {
            bool hover, active;
            bool clicked = DoButton(rect, string.Empty, style, on, out hover, out active);
            DrawIcon(rect, icon, NativeTextColor(currentContext), icon == Mdl2Icon.More ? 2f : 1f);
            return clicked;
        }

        private bool DoDisclosureButton(Rect rect, string label, bool open, RenderContext context, bool framed, Rect? backgroundRect = null) {
            bool hover, active;
            bool clicked = DoButton(rect, string.Empty, null, false, out hover, out active);
            DrawDisclosureChrome(backgroundRect ?? rect, framed, false, hover, active, context);
            DrawIcon(new Rect(rect.x + 4f, rect.y, 20f, rect.height), open ? Mdl2Icon.ChevronDown : Mdl2Icon.ChevronRight, NativeTextColor(context), 1f);
            DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel,
                new Rect(rect.x + 32f, rect.y, Mathf.Max(0f, rect.width - 36f), rect.height), GetContent(label), false, false, false, false);
            return clicked;
        }

        private static void DrawDisclosureChrome(Rect rect, bool framed, bool selected, bool hover, bool active, RenderContext context) {
            ThemeTokens theme = context.Theme.Tokens;
            if (framed || selected || hover || active) {
                MuiColor color = !GUI.enabled ? theme.ButtonDisabledBg : selected ? theme.SelectedBg : active ? theme.ButtonActiveBg : hover ? theme.ButtonHoverBg : theme.ButtonBg;
                DrawFilledRect(rect, ToUnityColor(color));
            }
            if (framed) DrawStrokeRect(rect, ToUnityColor(theme.Border), 1f);
        }

        private static MuiColor NativeTextColor(RenderContext context) {
            return GUI.enabled ? context.Theme.Tokens.Text : context.Theme.Tokens.TextDisabled;
        }

        private void DrawSelectionControl(Rect rect, string text, bool selected, bool radio, RenderContext context) {
            ThemeTokens theme = context.Theme.Tokens;
            MuiRect bounds = Mdl2ControlChrome.SelectionBounds(ToMuiRect(rect), theme.ToggleBoxSize);
            Rect box = ToUnityRect(bounds);
            bool hover = GUI.enabled && !IsInputBlockedByDialog() && MouseOverVisibleRect(rect);
            MuiColor selectedColor = Mdl2ControlChrome.SelectionColor(theme, GUI.enabled, hover, false);
            MuiColor border = Mdl2ControlChrome.ControlBorderColor(theme, GUI.enabled, hover);
            MuiColor background = GUI.enabled ? theme.FieldBg : theme.ButtonDisabledBg;
            if (radio) {
                DrawDisc(box, selected ? selectedColor : border);
                DrawDisc(new Rect(box.x + 2f, box.y + 2f, Mathf.Max(0f, box.width - 4f), Mathf.Max(0f, box.height - 4f)), background);
                if (selected) DrawDisc(new Rect(box.x + 5f, box.y + 5f, Mathf.Max(0f, box.width - 10f), Mathf.Max(0f, box.height - 10f)), selectedColor);
            } else {
                DrawFilledRect(box, ToUnityColor(selected ? selectedColor : background));
                if (selected) DrawIcon(box, Mdl2Icon.Check, theme.OnAccent, 1.5f);
                else DrawStrokeRect(box, ToUnityColor(border), 2f);
            }
            if (!string.IsNullOrEmpty(text)) {
                Rect label = new Rect(box.xMax + 8f, rect.y, Mathf.Max(0f, rect.xMax - box.xMax - 8f), rect.height);
                DrawStyle(GUI.enabled ? context.Theme.Label : disabledLabel, label, GetContent(text), false, false, false, false);
            }
        }

        private void DrawDisc(Rect rect, MuiColor color) {
            if (Event.current == null || !GuiEventPolicy.ShouldPaint(Event.current.type) || rect.width <= 0f || rect.height <= 0f) return;
            if (selectionDisc == null) selectionDisc = Textures.Disc();
            Color previous = GUI.color;
            try {
                GUI.color = ToUnityColor(color);
                GUI.DrawTexture(rect, selectionDisc);
            } finally { GUI.color = previous; }
        }

        private void DrawCapsule(Rect rect, MuiColor color) {
            float diameter = Mathf.Min(rect.width, rect.height);
            if (diameter <= 0f) return;
            DrawDisc(new Rect(rect.x, rect.y, diameter, diameter), color);
            DrawDisc(new Rect(rect.xMax - diameter, rect.y, diameter, diameter), color);
            DrawFilledRect(new Rect(rect.x + diameter * 0.5f, rect.y, Mathf.Max(0f, rect.width - diameter), diameter), ToUnityColor(color));
        }

        private static void DrawIcon(Rect rect, Mdl2Icon icon, MuiColor color, float thickness) {
            if (Event.current == null || !GuiEventPolicy.ShouldPaint(Event.current.type)) return;
            MuiRect bounds = ToMuiRect(rect);
            for (int i = 0; i < Mdl2IconGeometry.SegmentCount(icon); i++) {
                MuiVec2 a, b;
                Mdl2IconGeometry.Segment(icon, i, bounds, out a, out b);
                DrawGeometryLine(a, b, color, thickness);
            }
        }

        private static void DrawGeometryLine(MuiVec2 a, MuiVec2 b, MuiColor color, float thickness) {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length <= 0f) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            try {
                GUI.color = ToUnityColor(color);
                GUIUtility.RotateAroundPivot(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, new Vector2(a.X, a.Y));
                GUI.DrawTexture(new Rect(a.X, a.Y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            } finally { GUI.matrix = previousMatrix; GUI.color = previousColor; }
        }

        private static void DrawKeyboardFocus(Rect rect, ThemeTokens theme) {
            MuiRect inner = Mdl2ControlChrome.FocusBounds(ToMuiRect(rect));
            DrawStrokeRect(ToUnityRect(inner), ToUnityColor(theme.FocusRing), 1f);
            DrawStrokeRect(new Rect(inner.X + 1f, inner.Y + 1f, Mathf.Max(0f, inner.Width - 2f), Mathf.Max(0f, inner.Height - 2f)), ToUnityColor(theme.PanelBg), 1f);
        }

        private Rect DrawNumericLabel(Rect rect, string label, RenderContext context) {
            if (string.IsNullOrEmpty(label)) return rect;

            float labelWidth = Mathf.Min(Mathf.Max(82f, context.Theme.Label.CalcSize(GetContent(label)).x + 4f), rect.width * 0.5f);
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            DrawStyle(context.Theme.Label, labelRect, GetContent(label), false, false, false, false);
            return new Rect(labelRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.width - labelWidth - 6f), rect.height);
        }

        private bool DoNumericDrag(Rect rect, string valueText, RenderContext context, Action<float> onDelta) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            bool acceptsInput = GUI.enabled && !IsInputBlockedByDialog();
            bool hover = acceptsInput && current != null && MouseOverVisibleRect(rect);
            bool active = acceptsInput && GUIUtility.hotControl == controlId;
            bool changed = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    CapturePointer(controlId);
                    active = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseDrag && active) {
                    float speed = current.shift ? 10f : (current.alt ? 0.1f : 1f);
                    if (onDelta != null) onDelta(current.delta.x * speed);
                    changed = Math.Abs(current.delta.x) > 0.001f;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    ReleasePointer(controlId);
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                DrawStyle(context.Theme.TextField, rect, GetContent(valueText), hover, active, false, false);
            }

            return changed;
        }

        private void SplitSliderValueRect(Rect rect, string valueText, RenderContext context, out Rect sliderRect, out Rect textRect) {
            float textWidth = Mathf.Min(Mathf.Max(58f, context.Theme.Label.CalcSize(GetContent(valueText)).x + 8f), rect.width * 0.45f);
            textRect = new Rect(rect.xMax - textWidth, rect.y, textWidth, rect.height);
            sliderRect = new Rect(rect.x, rect.y, Mathf.Max(1f, rect.width - textWidth - 6f), rect.height);
        }

        private Rect SliderTrackRect(Rect rect, RenderContext context) {
            float sliderHeight = Mathf.Min(rect.height, GetSliderHeight(context));
            return new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - sliderHeight) * 0.5f), rect.width, sliderHeight);
        }

        private bool DoTextClick(Rect rect, out bool hover) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            bool acceptsInput = GUI.enabled && !IsInputBlockedByDialog();
            hover = acceptsInput && current != null && MouseOverVisibleRect(rect);
            bool active = acceptsInput && GUIUtility.hotControl == controlId;
            bool clicked = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    CapturePointer(controlId);
                    active = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    ReleasePointer(controlId);
                    clicked = hover;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            return clicked;
        }

        private static void DrawStyle(GUIStyle style, Rect rect, GUIContent content, bool hover, bool active, bool on, bool focus) {
            Event current = Event.current;
            if (current == null || !GuiEventPolicy.ShouldPaint(current.type)) return;
            style.Draw(rect, content, hover, active, on, focus);
        }

        private static void DrawFilledRect(Rect rect, Color color) {
            Event current = Event.current;
            if (current == null || !GuiEventPolicy.ShouldPaint(current.type)) return;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawStrokeRect(Rect rect, Color color, float thickness) {
            float t = thickness <= 0f ? 1f : thickness;
            DrawFilledRect(new Rect(rect.x, rect.y, rect.width, t), color);
            DrawFilledRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);
            DrawFilledRect(new Rect(rect.x, rect.y, t, rect.height), color);
            DrawFilledRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);
        }

        private float DoSlider(Rect controlRect, Rect trackRect, float value, float min, float max, RenderContext context) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, controlRect);
            Event current = Event.current;
            bool acceptsInput = GUI.enabled && !IsInputBlockedByDialog();
            bool hover = acceptsInput && current != null && MouseOverVisibleRect(controlRect);
            bool active = acceptsInput && GUIUtility.hotControl == controlId;
            float next = value;

            if (HasNavigationKey(currentWidget, context) &&
                KeyboardNavigation.MoveSlider(value, min, max, current.keyCode, currentWidget is SliderInt, current.shift, out next)) ConsumeKey();

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    CapturePointer(controlId);
                    active = true;
                    next = SliderValueFromMouse(current.mousePosition.x, trackRect, min, max);
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseDrag && active) {
                    next = SliderValueFromMouse(current.mousePosition.x, trackRect, min, max);
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    ReleasePointer(controlId);
                    active = false;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                ThemeTokens tokens = context.Theme.Tokens;
                MuiColor selectedColor = Mdl2ControlChrome.SelectionColor(tokens, GUI.enabled, hover, active);
                DrawFilledRect(trackRect, ToUnityColor(GUI.enabled ? tokens.ControlBorder : tokens.ButtonDisabledBg));
                float t = max <= min ? 0f : Mathf.InverseLerp(min, max, next);
                float thumbWidth = Mathf.Max(1f, context.Theme.HorizontalSliderThumb.fixedWidth);
                float thumbHeight = Mathf.Max(trackRect.height, context.Theme.HorizontalSliderThumb.fixedHeight);
                Rect thumbRect = new Rect(
                    trackRect.x + Mathf.Max(0f, trackRect.width - thumbWidth) * t,
                    controlRect.y + Mathf.Max(0f, (controlRect.height - thumbHeight) * 0.5f),
                    thumbWidth,
                    thumbHeight);
                DrawFilledRect(new Rect(trackRect.x, trackRect.y, Mathf.Max(0f, thumbRect.center.x - trackRect.x), trackRect.height), ToUnityColor(selectedColor));
                DrawFilledRect(thumbRect, ToUnityColor(selectedColor));
            }

            return next;
        }

        private static float SliderValueFromMouse(float mouseX, Rect trackRect, float min, float max) {
            if (max <= min || trackRect.width <= 0f) return min;
            float t = Mathf.Clamp01((mouseX - trackRect.x) / trackRect.width);
            return Mathf.Lerp(min, max, t);
        }

        private bool DoColorComponent(Rect rect, int component, float value, ColorDisplayMode displayMode, RenderContext context, out float next) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            bool acceptsInput = GUI.enabled && !IsInputBlockedByDialog();
            bool hover = acceptsInput && current != null && MouseOverVisibleRect(rect);
            bool active = acceptsInput && GUIUtility.hotControl == controlId;
            next = Mathf.Clamp01(value);
            bool changed = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    CapturePointer(controlId);
                    active = true;
                    next = ColorComponentFromMouse(rect, current.mousePosition.x);
                    changed = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseDrag && active) {
                    next = ColorComponentFromMouse(rect, current.mousePosition.x);
                    changed = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    ReleasePointer(controlId);
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                DrawFilledRect(rect, ToUnityColor(!GUI.enabled ? context.Theme.Tokens.ButtonDisabledBg : active ? context.Theme.Tokens.FieldActiveBg : hover ? context.Theme.Tokens.FieldHoverBg : context.Theme.Tokens.FieldBg));
                DrawStrokeRect(rect, ToUnityColor(Mdl2ControlChrome.FieldBorderColor(context.Theme.Tokens, GUI.enabled, hover, active)), 2f);
                Rect fillRect = new Rect(rect.x + 2f, rect.yMax - 5f, Mathf.Max(0f, (rect.width - 4f) * Mathf.Clamp01(next)), Mathf.Min(3f, rect.height));
                DrawFilledRect(fillRect, ToUnityColor(GUI.enabled ? ComponentColor(component, next) : context.Theme.Tokens.TextDisabled));
                DrawStyle(GUI.enabled ? context.Theme.CenterLabel : disabledCenterLabel, new Rect(rect.x + 4f, rect.y, Mathf.Max(0f, rect.width - 8f), Mathf.Max(0f, rect.height - 3f)), GetContent(ComponentName(component) + " " + FormatColorComponent(next, displayMode)), false, false, false, false);
            }

            return changed && !Mathf.Approximately(next, Mathf.Clamp01(value));
        }

        private static float ColorComponentFromMouse(Rect rect, float mouseX) {
            if (rect.width <= 0f) return 0f;
            return Mathf.Clamp01((mouseX - rect.x) / rect.width);
        }

        private static void DrawColorSwatch(Rect rect, MuiColor color, bool checkerboard, RenderContext context) {
            if (checkerboard && color.A < 0.999f) {
                Color a = new Color(0.78f, 0.78f, 0.78f, 1f);
                Color b = new Color(0.92f, 0.92f, 0.92f, 1f);
                float halfW = Mathf.Max(1f, rect.width * 0.5f);
                float halfH = Mathf.Max(1f, rect.height * 0.5f);
                DrawFilledRect(rect, b);
                DrawFilledRect(new Rect(rect.x, rect.y, halfW, halfH), a);
                DrawFilledRect(new Rect(rect.x + halfW, rect.y + halfH, Mathf.Max(1f, rect.width - halfW), Mathf.Max(1f, rect.height - halfH)), a);
            }

            DrawFilledRect(rect, ToUnityColor(color));
            DrawStrokeRect(rect, ToUnityColor(context.Theme.Tokens.Border), 1f);
        }

        private int DrawTabsAt(Rect rect, float height, string[] titles, int selected, RenderContext context) {
            int count = Mathf.Max(1, titles.Length);
            float tabWidth = rect.width / count;
            int next = selected;
            for (int i = 0; i < titles.Length; i++) {
                Rect tabRect = new Rect(rect.x + tabWidth * i, rect.y, tabWidth, height);
                if (DoButton(tabRect, titles[i], context.Theme.TabButton, i == selected)) {
                    next = i;
                }
            }

            return next;
        }

        private void DrawTabItemButton(Rect rect, TabItemButton button, RenderContext context) {
            if (button == null) return;
            if (DoButton(rect, button.Text, context.Theme.TabButton, false) && button.OnClick != null && !IsInputBlockedByDialog()) {
                Action action = button.OnClick;
                EnqueueCallback(action);
            }
        }

        private static string FormatTabTitle(Tab tab) {
            if (tab == null) return string.Empty;
            string title = tab.Title ?? string.Empty;
            if (tab.Unsaved) title += "*";
            return title;
        }

        private void CloseTab(Tabs tabs, Tab tab, int tabIndex) {
            if (tab != null && tab.OnClose != null) {
                Action action = tab.OnClose;
                EnqueueCallback(action);
                return;
            }

            if (tabs != null && tabs.OnClose != null) {
                int captured = tabIndex;
                EnqueueCallback(delegate { tabs.OnClose(captured); });
            }
        }

        private Rect CalculateScrollViewRect(LayoutNode node, Rect viewportRect) {
            return ToUnityRect(ScrollGeometry.ContentExtent(node, ToMuiRect(viewportRect)));
        }

        private Vector2 BeginScrollView(RenderContext context, Rect viewport, Vector2 position, Rect content,
            bool horizontal, bool vertical) {
            MuiRect visibleBounds = coordinates.ClipToScreen(ToMuiRect(viewport));
            Vector2 result = context.Theme.BeginScrollView(viewport, position, content, horizontal, vertical);
            MuiRect clippedViewport = ScrollGeometry.ContentViewport(ToMuiRect(viewport), ToMuiRect(content), horizontal, vertical, ScrollbarGeometry.GutterSize);
            if (scrollDepth == scrollInputs.Length) {
                ScrollInputFrame[] expanded = new ScrollInputFrame[scrollInputs.Length * 2];
                Array.Copy(scrollInputs, expanded, scrollInputs.Length);
                scrollInputs = expanded;
            }
            scrollInputs[scrollDepth++] = new ScrollInputFrame {
                VisibleBounds = visibleBounds,
                Position = new MuiVec2(result.x, result.y),
                Maximum = new MuiVec2(Math.Max(0f, content.width - clippedViewport.Width), Math.Max(0f, content.height - clippedViewport.Height)),
                Enabled = GUI.enabled && !context.SuppressInput && !IsInputBlockedByDialog()
            };
            coordinates.PushScroll(clippedViewport, new MuiVec2(result.x, result.y), ToMuiRect(content));
            return result;
        }

        private static void DrawScrollFrame(MuiRect bounds, RenderContext context) {
            DrawStrokeRect(ToUnityRect(bounds), ToUnityColor(GUI.enabled ? context.Theme.Tokens.ControlBorder : context.Theme.Tokens.Border), ScrollGeometry.FrameBorder);
        }

        private void EndScrollView() {
            ScrollInputFrame input = scrollInputs[--scrollDepth];
            Event current = Event.current;
            bool handleWheel = true;
            if (current != null && current.type == EventType.ScrollWheel) {
                MuiVec2 mouse = coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y));
                handleWheel = input.Enabled && input.VisibleBounds.Contains(mouse) &&
                    ScrollWheelRouting.CanConsume(input.Position, new MuiVec2(current.delta.x, current.delta.y), input.Maximum);
            }
            coordinates.Pop();
            GUI.EndScrollView(handleWheel);
        }

        private Rect ToComboButtonStateRect(Rect buttonRect) {
            buttonRect = ToUnityRect(coordinates.ToScreen(ToMuiRect(buttonRect)));
            if (string.IsNullOrEmpty(currentWindowStateId) || currentContext == null) return buttonRect;

            WindowState state = currentContext.StateStore.GetOrCreate<WindowState>(currentWindowStateId);
            return new Rect(
                buttonRect.x - state.Rect.x,
                buttonRect.y - state.Rect.y,
                buttonRect.width,
                buttonRect.height);
        }

        private static Rect OffsetRect(MuiRect rect, Vector2 offset) {
            return new Rect(rect.X + offset.x, rect.Y + offset.y, rect.Width, rect.Height);
        }

        private void DoDrag(WindowState state, int hint, Rect dragRect) {
            if (state == null) return;

            Event current = Event.current;
            if (current == null) return;
            bool releasePointer = GuiEventPolicy.ShouldReleasePointer(current.type, current.button);

            int controlId = GUIUtility.GetControlID(hint, FocusType.Passive, dragRect);
            bool ownsDrag = GUIUtility.hotControl == controlId;
            if (current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(dragRect)) {
                CapturePointer(controlId);
                activeDrag = state.Drag;
                ownsDrag = true;
            }

            if (!ownsDrag && current.type != EventType.MouseDown) return;

            MuiInputEvent input = ToMuiInputEvent(current);
            MuiRect target = ToMuiRect(state.Rect);
            if (dragController.Dispatch(input, ToMuiRect(dragRect), state.Drag, ref target)) {
                state.Rect = ToUnityRect(target);
                current.Use();
                Input.ResetInputAxes();
            }

            if (releasePointer && GUIUtility.hotControl == controlId) {
                ReleasePointer(controlId);
            }
        }

        private void DoDrag(DialogState state, int hint, Rect dragRect) {
            if (state == null) return;

            Event current = Event.current;
            if (current == null) return;
            bool releasePointer = GuiEventPolicy.ShouldReleasePointer(current.type, current.button);

            int controlId = GUIUtility.GetControlID(hint, FocusType.Passive, dragRect);
            bool ownsDrag = GUIUtility.hotControl == controlId;
            if (current.type == EventType.MouseDown && current.button == 0 && MouseOverVisibleRect(dragRect)) {
                CapturePointer(controlId);
                activeDrag = state.Drag;
                ownsDrag = true;
            }

            if (!ownsDrag && current.type != EventType.MouseDown) return;

            MuiInputEvent input = ToMuiInputEvent(current);
            MuiRect target = ToMuiRect(state.Rect);
            if (dragController.Dispatch(input, ToMuiRect(dragRect), state.Drag, ref target)) {
                state.Rect = ToUnityRect(target);
                current.Use();
                Input.ResetInputAxes();
            }

            if (releasePointer && GUIUtility.hotControl == controlId) {
                ReleasePointer(controlId);
            }
        }

        private static MuiInputEvent ToMuiInputEvent(Event current) {
            if (current == null) return null;

            MuiEventType type = MuiEventType.None;
            if (current.type == EventType.MouseDown) type = MuiEventType.MouseDown;
            else if (current.type == EventType.MouseDrag) type = MuiEventType.MouseDrag;
            else if (current.type == EventType.MouseUp) type = MuiEventType.MouseUp;
            else if (current.type == EventType.ScrollWheel) type = MuiEventType.ScrollWheel;
            else if (current.type == EventType.KeyDown) type = MuiEventType.KeyDown;
            else if (current.type == EventType.KeyUp) type = MuiEventType.KeyUp;
            else if (current.type == EventType.Repaint) type = MuiEventType.Repaint;

            return new MuiInputEvent {
                Type = type,
                MousePosition = new MuiVec2(current.mousePosition.x, current.mousePosition.y),
                MouseButton = current.button,
                KeyCode = (int)current.keyCode,
                Character = current.character,
                ScrollDeltaY = current.delta.y,
                Control = current.control,
                Shift = current.shift,
                Alt = current.alt,
                Command = current.command
            };
        }

        private static MuiRect ToMuiRect(Rect rect) {
            return new MuiRect(rect.x, rect.y, rect.width, rect.height);
        }

        private static Rect ToUnityRect(MuiRect rect) {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static Color ToUnityColor(MuiColor color) {
            return new Color(color.R, color.G, color.B, color.A);
        }

        private static string FormatColor(MuiColor color, bool alpha) {
            return FormatColor(color, alpha, ColorDisplayMode.Float);
        }

        private static string FormatColor(MuiColor color, bool alpha, ColorDisplayMode displayMode) {
            if (displayMode == ColorDisplayMode.Hex) {
                string hex = "#" + ToByte(color.R).ToString("X2") + ToByte(color.G).ToString("X2") + ToByte(color.B).ToString("X2");
                if (alpha) hex += ToByte(color.A).ToString("X2");
                return hex;
            }

            if (displayMode == ColorDisplayMode.Byte) {
                string bytes = ToByte(color.R).ToString() + ", " + ToByte(color.G).ToString() + ", " + ToByte(color.B).ToString();
                if (alpha) bytes += ", " + ToByte(color.A).ToString();
                return bytes;
            }

            string text = color.R.ToString("0.##") + ", " + color.G.ToString("0.##") + ", " + color.B.ToString("0.##");
            if (alpha) text += ", " + color.A.ToString("0.##");
            return text;
        }

        private static string FormatColorComponent(float value, ColorDisplayMode displayMode) {
            if (displayMode == ColorDisplayMode.Hex) return ToByte(value).ToString("X2");
            if (displayMode == ColorDisplayMode.Byte) return ToByte(value).ToString();
            return Mathf.Clamp01(value).ToString("0.##");
        }

        private static int ToByte(float value) {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static float GetColorComponent(MuiColor color, int component) {
            if (component == 0) return color.R;
            if (component == 1) return color.G;
            if (component == 2) return color.B;
            return color.A;
        }

        private static MuiColor SetColorComponent(MuiColor color, int component, float value) {
            float clamped = Mathf.Clamp01(value);
            if (component == 0) color.R = clamped;
            else if (component == 1) color.G = clamped;
            else if (component == 2) color.B = clamped;
            else color.A = clamped;
            return color;
        }

        private static string ComponentName(int component) {
            if (component == 0) return "R";
            if (component == 1) return "G";
            if (component == 2) return "B";
            return "A";
        }

        private static MuiColor ComponentColor(int component, float value) {
            float clamped = Mathf.Clamp01(value);
            if (component == 0) return new MuiColor(clamped, 0.16f, 0.16f, 1f);
            if (component == 1) return new MuiColor(0.16f, clamped, 0.16f, 1f);
            if (component == 2) return new MuiColor(0.16f, 0.22f, clamped, 1f);
            return new MuiColor(0.22f, 0.22f, 0.22f, clamped);
        }

        private static float ClampThemeScalar(ThemeScalarToken token, float value) {
            if (token == ThemeScalarToken.BorderWidth) return Mathf.Clamp(value, 0f, 8f);
            if (token == ThemeScalarToken.Radius) return Mathf.Clamp(value, 0f, 16f);
            if (token == ThemeScalarToken.FontSize) return Mathf.Clamp(value, 8f, 32f);
            if (token == ThemeScalarToken.SliderTrackHeight) return Mathf.Clamp(value, 1f, 16f);
            if (token == ThemeScalarToken.SliderThumbWidth) return Mathf.Clamp(value, 4f, 32f);
            if (token == ThemeScalarToken.SliderThumbHeight) return Mathf.Clamp(value, 4f, 32f);
            return Mathf.Clamp(value, 8f, 32f);
        }

        private static int ClampInt(int value, bool hasMin, int min, bool hasMax, int max) {
            if (hasMin && value < min) value = min;
            if (hasMax && value > max) value = max;
            return value;
        }

        private static float ClampFloat(float value, bool hasMin, float min, bool hasMax, float max) {
            if (hasMin && value < min) value = min;
            if (hasMax && value > max) value = max;
            return value;
        }

        private static double ClampDouble(double value, bool hasMin, double min, bool hasMax, double max) {
            if (hasMin && value < min) value = min;
            if (hasMax && value > max) value = max;
            return value;
        }

        private static string FormatInt(int value, string format) {
            return string.IsNullOrEmpty(format) ? value.ToString() : string.Format(format, value);
        }

        private static string FormatFloat(float value, string format) {
            return string.IsNullOrEmpty(format) ? value.ToString("0.###") : value.ToString(format);
        }

        private static string FormatDouble(double value, string format) {
            return string.IsNullOrEmpty(format) ? value.ToString("0.###") : value.ToString(format);
        }

        private static Color StatusColor(StatusBadgeKind kind, ThemeTokens tokens) {
            if (kind == StatusBadgeKind.Success) return ToUnityColor(tokens.SuccessBg);
            if (kind == StatusBadgeKind.Warning) return ToUnityColor(tokens.WarningBg);
            if (kind == StatusBadgeKind.Error) return ToUnityColor(tokens.ErrorBg);
            return ToUnityColor(tokens.InfoBg);
        }

        private static bool MatchesFilter(string text, string filter) {
            if (string.IsNullOrEmpty(filter)) return true;
            if (text == null) text = string.Empty;
            return text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasVisibleTableColumns(Table table) {
            for (int i = 0; i < table.Children.Count; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column != null && column.Visible) return true;
            }

            return false;
        }

        private static bool HasExplicitHeaderRow(LayoutNode node) {
            for (int i = 0; i < node.Children.Count; i++) {
                if (node.Children[i].Widget is TableHeaderRow) return true;
            }

            return false;
        }

        private static int GetTableColumnCount(Table table, LayoutNode node) {
            int defined = 0;
            int visibleDefined = 0;
            for (int i = 0; i < table.Children.Count; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null) continue;
                defined++;
                if (column.Visible) visibleDefined++;
            }

            if (defined > 0) return Mathf.Max(1, visibleDefined);

            int count = Mathf.Max(1, table.Columns);
            for (int i = 0; i < node.Children.Count; i++) {
                count = Mathf.Max(count, node.Children[i].Children.Count);
            }

            return count;
        }

        private static float[] GetTableColumnWidths(Table table, int columns, float totalWidth) {
            float[] widths = new float[Mathf.Max(1, columns)];
            float fixedWidth = 0f;
            int stretchCount = 0;
            int columnIndex = 0;

            for (int i = 0; i < table.Children.Count && columnIndex < widths.Length; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null || !column.Visible) continue;

                if (!column.Stretch || column.Width > 0f) {
                    widths[columnIndex] = ClampColumnWidth(column, Mathf.Max(1f, column.Width > 0f ? column.Width : 80f));
                    fixedWidth += widths[columnIndex];
                } else {
                    stretchCount++;
                }
                columnIndex++;
            }

            while (columnIndex < widths.Length) {
                stretchCount++;
                columnIndex++;
            }

            float gaps = table.ColumnGap * Mathf.Max(0, widths.Length - 1);
            float stretchWidth = stretchCount <= 0 ? 0f : Mathf.Max(1f, (totalWidth - fixedWidth - gaps) / stretchCount);
            for (int i = 0; i < widths.Length; i++) {
                if (widths[i] <= 0f) widths[i] = ClampColumnWidth(GetVisibleTableColumn(table, i), stretchWidth);
            }

            return widths;
        }

        private static TableColumn GetVisibleTableColumn(Table table, int visibleIndex) {
            int index = 0;
            for (int i = 0; i < table.Children.Count; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null || !column.Visible) continue;
                if (index == visibleIndex) return column;
                index++;
            }

            return null;
        }

        private static float ClampColumnWidth(TableColumn column, float width) {
            float value = Mathf.Max(1f, width);
            if (column != null && column.MinWidth > 0f) value = Mathf.Max(column.MinWidth, value);
            if (column != null && column.MaxWidth > 0f) value = Mathf.Min(column.MaxWidth, value);
            return Mathf.Max(1f, value);
        }

        private float GetTableHeaderHeight(Table table, RenderContext context) {
            if (table != null && table.HeaderHeight > 0f) return table.HeaderHeight;
            if (context != null && context.Theme != null) return controlRowHeight;
            return 22f;
        }


        private static TableSortDirection NextSortDirection(TableSortDirection direction) {
            if (direction == TableSortDirection.Ascending) return TableSortDirection.Descending;
            return TableSortDirection.Ascending;
        }

        private static int StableHash(string text) {
            unchecked {
                int hash = 23;
                if (text != null) {
                    for (int i = 0; i < text.Length; i++) {
                        hash = hash * 31 + text[i];
                    }
                }
                return hash;
            }
        }

        private static Color WithAlpha(Color color, float alpha) {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static bool IsUsableLayoutRect(Rect rect) {
            return rect.width > 4f && rect.height > 4f;
        }

        private void RegisterInputRegion(Rect rect, bool active, bool overlay = false) {
            if (!active) return;

            MuiRect screenRect = coordinates.ClipToScreen(ToMuiRect(rect));
            if (screenRect.Width <= 0f || screenRect.Height <= 0f) return;
            if (currentContext != null && currentContext.Query != null) currentContext.Query.RecordInputRegion(screenRect, overlay);
            Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool containsMouse = screenRect.Contains(new MuiVec2(mousePosition.x, mousePosition.y)) && coordinates.ContainsScreenPoint(new MuiVec2(mousePosition.x, mousePosition.y));
            if (containsMouse) MaximumUIRuntime.IsMouseOverAnyWindow = true;
            if (containsMouse && AnyMouseButtonDown()) mouseDownOnMaximumUi = true;
        }

        private void CaptureInputIfNeeded() {
            if (!AnyMouseButton()) {
                mouseDownOnMaximumUi = false;
                return;
            }

            if ((currentContext == null || currentContext.SoftMouseCapture) && (mouseDownOnMaximumUi || MaximumUIRuntime.IsMouseOverAnyWindow)) {
                Input.ResetInputAxes();
            }
        }

        private void RecordItemQuery(RenderContext context, LayoutNode node, Widget widget, Vector2 offset) {
            if (context == null || node == null || widget == null) return;

            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            MuiRect screenRect = coordinates.ToScreen(ToMuiRect(rect));
            MuiVec2 screenMouse = current == null ? new MuiVec2() : coordinates.ToScreen(new MuiVec2(current.mousePosition.x, current.mousePosition.y));
            bool hovered = current != null && MouseOverVisibleRect(rect) && coordinates.ContainsScreenPoint(screenMouse);
            bool acceptsInput = GUI.enabled && !context.SuppressInput && !IsInputBlockedByDialog();
            bool active = acceptsInput && hovered && current != null &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseDrag);
            bool clicked = acceptsInput && hovered && current != null && current.type == EventType.MouseUp && current.button == 0;
            if (IsNavigationFocusable(widget)) {
                if (context.Navigation != null && !context.SuppressInput) context.Navigation.Register(widget, node.Path, screenRect, acceptsInput && widget.Enabled);
                if (context.Query != null && coordinates.IsVisible(ToMuiRect(rect))) context.Query.RecordInputRegion(coordinates.ClipToScreen(ToMuiRect(rect)));
                if (!context.SuppressInput && GUI.enabled && context.Navigation != null && hovered && current != null && current.type == EventType.MouseDown && current.button == 0) {
                    context.Navigation.FocusPointer(widget, node.Path);
                }
            }
            bool focused = context.Navigation != null && context.Navigation.IsFocused(widget, node.Path);
            if (context.Query == null) return;
            context.Query.RecordItem(
                widget,
                node.Path,
                screenRect,
                hovered,
                active,
                focused,
                clicked);
        }

        private void RecordWindowQuery(RenderContext context, Window widget, WindowState state) {
            if (context == null || context.Query == null || widget == null || state == null) return;

            float buttonSize = WindowTitleHeight(widget, context);
            float closeX = state.Rect.xMax - buttonSize;
            float foldX = widget.Closable ? closeX - buttonSize : closeX;
            Rect titleRect = new Rect(state.Rect.x, state.Rect.y, Mathf.Max(0f, foldX - state.Rect.x), buttonSize);
            Event current = Event.current;
            bool hovered = current != null && MouseOverVisibleRect(state.Rect);
            bool titleHovered = current != null && MouseOverVisibleRect(titleRect);
            bool titleActive = titleHovered && current != null &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseDrag);
            context.Query.RecordWindow(
                widget,
                context.Path,
                ToMuiRect(state.Rect),
                ToMuiRect(titleRect),
                hovered,
                hovered && GUIUtility.keyboardControl != 0,
                titleHovered,
                titleActive);
        }

        private void UpdateQueryCapture(RenderContext context) {
            if (context == null || context.Query == null) return;

            context.Query.SetCapture(
                MaximumUIRuntime.IsMouseOverAnyWindow || mouseDownOnMaximumUi,
                context.OwnsKeyboardInput && (MaximumUIRuntime.IsAnyTextFieldFocused || MaximumUIRuntime.IsModalOpen || (context.Navigation != null && context.Navigation.HasFocus)),
                context.OwnsKeyboardInput && MaximumUIRuntime.IsAnyTextFieldFocused);
            if (context.OwnsKeyboardInput && context.Navigation != null && context.Navigation.Count > 0) {
                context.Query.SetNavigation(context.Navigation.HasFocus, true);
            }
        }

        private static Rect ClipWindowPosition(Rect rect, bool keepFullyVisible, float minWidth, float minHeight, float foldedHeight) {
            return ToUnityRect(WindowGeometry.Clamp(ToMuiRect(rect), Screen.width, Screen.height,
                minWidth, keepFullyVisible ? minHeight : foldedHeight));
        }

        internal static float WindowTitleHeight(Window widget, RenderContext context) {
            return Mathf.Max(widget.FoldedHeight, context.Theme.TitleBarHeight);
        }

        private static bool AnyMouseButtonDown() {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        }

        private static bool AnyMouseButton() {
            return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
        }

        private static bool IsNavigationFocusable(Widget widget) {
            if (widget == null || !widget.Focusable || !widget.Enabled) return false;
            return widget is Button ||
                   widget is SmallButton ||
                   widget is ArrowButton ||
                   widget is ImageButton ||
                   widget is TextLink ||
                   widget is Toggle || widget is ToggleSwitch ||
                   widget is Checkbox ||
                   widget is RadioButton ||
                   widget is ISegmentedControlWidget ||
                   widget is Slider ||
                   widget is InputInt ||
                   widget is InputFloat ||
                   widget is InputDouble ||
                   widget is DragInt ||
                   widget is DragFloat ||
                   widget is SliderInt ||
                   widget is SliderFloat ||
                   widget is SliderAngle ||
                   widget is TextField ||
                   widget is TextArea ||
                   widget is PasswordField ||
                   widget is SearchBox ||
                   widget is IComboBoxWidget ||
                   widget is IListBoxWidget ||
                   widget is ThemeEditor ||
                   widget is Foldout ||
                   widget is CollapsingHeader ||
                   widget is ClosableCollapsingHeader ||
                   widget is MenuBar ||
                   widget is Selectable ||
                   widget is TreeNode ||
                   widget is TreeLeaf ||
                   widget is ColorButton ||
                   widget is ColorEdit ||
                   widget is ColorEdit3 ||
                   widget is ColorEdit4 ||
                   widget is ColorPicker3 ||
                   widget is ColorPicker4 ||
                   widget is VectorField ||
                   widget is VirtualList ||
                   widget is TwoPaneLayout ||
                   widget is DragSource ||
                   widget is DropTarget ||
                   widget is Tabs;
        }
    }
}
