using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class ImguiRenderer {
        private static readonly GUIContent TempContent = new GUIContent();
        private readonly DragController dragController = new DragController();
        private readonly LayoutEngine layoutEngine = new LayoutEngine();
        private readonly UnityPainter unityPainter = new UnityPainter();

        private RenderContext currentContext;
        private Window currentWindow;
        private Vector2 currentWindowContentOffset;
        private bool mouseDownOnMaximumUi;
        private int dialogDepth;

        public void Render(Widget widget, RenderContext context) {
            if (widget == null || context == null) return;

            RenderContext oldContext = currentContext;
            currentContext = context;
            try {
                MaximumUIRuntime.IsMouseOverAnyWindow = false;
                if (context.Query != null) {
                    context.Query.BeginFrame();
                    context.Query.RecordInput(ToMuiInputEvent(Event.current));
                }
                if (context.Navigation != null) context.Navigation.BeginFrame();
                context.Overlay.Clear();
                bool externalModalOpen = context.IsModalOpen;
                context.IsModalOpen = externalModalOpen || HasDialogWidget(widget) || (context.Dialogs != null && context.Dialogs.HasOpenDialogs);
                MaximumUIRuntime.IsModalOpen = context.IsModalOpen;
                RenderWidget(widget, context, "0");

                for (int i = 0; i < context.ComboBoxStates.Count; i++) {
                    ComboBoxState state = context.ComboBoxStates[i];
                    if (state == null || state.Dropdown == null || !state.Dropdown.Show || !state.HasButtonRect || string.IsNullOrEmpty(state.WindowStateId)) continue;

                    WindowState windowState = context.StateStore.GetOrCreate<WindowState>(state.WindowStateId);
                    if (state.Searchable) {
                        ComboBoxState capturedState = state;
                        state.Dropdown.Set(windowState.Rect, state.ButtonRect, state.Items, state.Callback, state.MaxVisibleItems, state.FilterText, delegate(string value) {
                            capturedState.FilterText = value ?? string.Empty;
                        }, state.FilterPlaceholder);
                    } else {
                        state.Dropdown.Set(windowState.Rect, state.ButtonRect, state.Items, state.Callback, state.MaxVisibleItems);
                    }
                }

                for (int i = 0; i < context.Dropdowns.Count; i++) {
                    DropdownOverlay dropdown = context.Dropdowns[i];
                    dropdown.Draw();
                    RegisterInputRegion(dropdown.Rect, dropdown.Show);
                }

                for (int i = 0; i < context.ComboBoxStates.Count; i++) {
                    ComboBoxState state = context.ComboBoxStates[i];
                    state.Open = state.Dropdown != null && state.Dropdown.Show;
                }

                RegisterServiceDialogs(context);
                RegisterDragDropPreview(context);
                context.Overlay.Render(context);
                CompleteDragDropFrame(context);
                if (context.Navigation != null) context.Navigation.EndFrame();
                CaptureInputIfNeeded();
                UpdateQueryCapture(context);
            } finally {
                currentContext = oldContext;
            }
        }

        private void RenderWidget(Widget widget, RenderContext context, string path) {
            if (widget == null || widget is EmptyWidget) return;

            string oldPath = context.Path;
            context.Path = oldPath == null || oldPath.Length == 0 ? path : oldPath + "." + path;

            bool oldEnabled = GUI.enabled;
            bool blockInput = IsInputBlockedByDialog() && !(widget is Dialog) && !HasDialogWidget(widget);
            GUI.enabled = oldEnabled && widget.Enabled && !blockInput;
            try {
                if (widget is Window) {
                    RenderWindow((Window)widget, context);
                } else if (widget is Dialog) {
                    RenderDialog((Dialog)widget, context);
                } else {
                    LayoutNode root = layoutEngine.Layout(widget, new MuiRect(0f, 0f, Screen.width, Screen.height), context.Path);
                    RenderLayoutNode(root, context, Vector2.zero);
                }
            } finally {
                GUI.enabled = oldEnabled;
                context.Path = oldPath;
            }
        }

        private void RenderWindow(Window widget, RenderContext context) {
            if (!widget.Visible) return;

            string id = context.StateId(widget, "window");
            WindowState state = context.StateStore.GetOrCreate<WindowState>(id);
            if (!state.Initialized) {
                state.Rect = new Rect(80f, 60f, widget.Width, widget.Height);
                state.Initialized = true;
            }

            state.Rect.width = Mathf.Max(state.Rect.width, widget.MinWidth);
            state.Rect.height = state.Folded ? widget.FoldedHeight : Mathf.Max(state.Rect.height, widget.MinHeight);

            RenderContext oldContext = currentContext;
            Window oldWindow = currentWindow;
            Vector2 oldWindowContentOffset = currentWindowContentOffset;
            string windowPath = context.Path;
            currentContext = context;
            currentWindow = widget;
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
                state.Rect = ClipWindowPosition(state.Rect, !state.Folded, widget.MinWidth, widget.MinHeight, widget.FoldedHeight);
                RegisterInputRegion(state.Rect, true);
                RecordWindowQuery(context, widget, state);
            } finally {
                currentContext = oldContext;
                currentWindow = oldWindow;
                currentWindowContentOffset = oldWindowContentOffset;
            }
        }

        private void DrawWindowFrame(RenderContext context, Window widget, WindowState state) {
            GUIStyle style = state.Folded ? context.Theme.FoldedWindow : context.Theme.Window;
            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    style.Draw(state.Rect, GetContent(widget.Title), false, false, false, false);
                }
            } finally {
                GUI.color = oldColor;
            }

            float buttonSize = context.Theme.Window.fontSize * 1.4f;
            float closeX = state.Rect.xMax - buttonSize;
            float foldX = widget.Closable ? closeX - buttonSize : closeX;
            bool inputBlocked = IsInputBlockedByDialog();

            if (!inputBlocked && widget.Foldable && DoButton(new Rect(foldX, state.Rect.y, buttonSize, buttonSize), state.Folded ? "+" : "-", context.Theme.Button, false)) {
                state.Folded = !state.Folded;
                if (!state.Folded) state.Rect.height = widget.Height;
            }

            if (!inputBlocked && widget.Closable && DoButton(new Rect(closeX, state.Rect.y, buttonSize, buttonSize), "×", context.Theme.Button, false)) {
                MaximumUIRuntime.EnqueueCallback(delegate {
                    widget.Visible = false;
                    if (widget.OnVisibleChange != null) widget.OnVisibleChange(false);
                });
            }

            if (!inputBlocked) {
                Rect dragRect = new Rect(state.Rect.x, state.Rect.y, Mathf.Max(0f, foldX - state.Rect.x), widget.FoldedHeight);
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
                LayoutNode root = layoutEngine.Layout(widget, ToMuiRect(state.Rect), context.Path);
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
            context.Theme.Label.Draw(rect, GetContent("MaximumUI render error: " + ex.GetType().Name), false, false, false, false);
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
            string oldPath = context.Path;
            context.Path = node.Path;

            bool oldEnabled = GUI.enabled;
            bool blockInput = IsInputBlockedByDialog() && !(widget is Dialog) && !HasDialogWidget(widget);
            GUI.enabled = oldEnabled && widget.Enabled && !blockInput;
            try {
                RecordItemQuery(context, node, widget, offset);
                if (widget is VStack || widget is HStack || widget is Panel || widget is Grid || widget is TableRow || widget is TableCell || widget is Tab) {
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
            } finally {
                GUI.enabled = oldEnabled;
                context.Path = oldPath;
            }
        }

        private void RenderLabelAt(LayoutNode node, Label widget, RenderContext context, Vector2 offset) {
            GUIStyle style = widget.Style as GUIStyle ?? context.Theme.Label;
            if (widget.WordWrap && !style.wordWrap) {
                style = new GUIStyle(style) {
                    wordWrap = true
                };
            }

            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                style.Draw(rect, GetContent(widget.Text), false, false, false, false);
            }
        }

        private void RenderTableAt(LayoutNode node, Table table, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawFilledRect(rect, new Color(0.78f, 0.78f, 0.78f, context.Theme.WindowOpacity));
                if (table.ShowOuterBorder) DrawStrokeRect(rect, Color.black, 1f);
                DrawTableRowsBackground(node, table, offset);
            }

            if (table.ShowHeader && HasVisibleTableColumns(table) && !HasExplicitHeaderRow(node)) {
                RenderTableHeader(context, node, table, rect);
            }

            RenderLayoutChildren(node, context, offset);
        }

        private void DrawTableRowsBackground(LayoutNode node, Table table, Vector2 offset) {
            int dataRow = 0;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode rowNode = node.Children[i];
                TableRow row = rowNode.Widget as TableRow;
                if (row == null) continue;

                Rect rowRect = OffsetRect(rowNode.Rect, offset);
                if (row.HasBackground) {
                    DrawFilledRect(rowRect, ToUnityColor(row.Background));
                } else if (table.AlternatingRows && dataRow % 2 == 1) {
                    DrawFilledRect(rowRect, new Color(0f, 0f, 0f, 0.05f));
                }

                if (table.ShowInnerBorders) {
                    DrawFilledRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), new Color(0f, 0f, 0f, 0.35f));
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
                bool clicked = DoButton(rect, FormatColumnTitle(column), context.Theme.TabButton, column.SortDirection != TableSortDirection.None);
                if (clicked && column.Sortable && column.OnSort != null && !IsInputBlockedByDialog()) {
                    TableSortDirection next = NextSortDirection(column.SortDirection);
                    MaximumUIRuntime.EnqueueCallback(delegate { column.OnSort(next); });
                }

                x += widths[columnIndex] + table.ColumnGap;
                columnIndex++;
            }
        }

        private void RenderTwoPaneLayoutAt(LayoutNode node, TwoPaneLayout widget, RenderContext context, Vector2 offset) {
            RenderLayoutChildren(node, context, offset);
            if (node.Children.Count < 2) return;

            Rect first = OffsetRect(node.Children[0].Rect, offset);
            Rect second = OffsetRect(node.Children[1].Rect, offset);
            Rect whole = OffsetRect(node.Rect, offset);
            Rect splitter = widget.Orientation == TwoPaneOrientation.Horizontal
                ? new Rect(first.xMax, whole.y, Mathf.Max(1f, second.x - first.xMax), whole.height)
                : new Rect(whole.x, first.yMax, whole.width, Mathf.Max(1f, second.y - first.yMax));

            DrawFilledRect(splitter, Color.black);
            RegisterInputRegion(splitter, true);

            if (widget.OnChange == null || IsInputBlockedByDialog()) return;

            int controlId = GUIUtility.GetControlID(StableHash(context.StateId(widget, "splitter")), FocusType.Passive, splitter);
            Event current = Event.current;
            if (current == null) return;

            bool active = GUIUtility.hotControl == controlId;
            if (current.type == EventType.MouseDown && current.button == 0 && splitter.Contains(current.mousePosition)) {
                GUIUtility.hotControl = controlId;
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
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(next); });
                current.Use();
                Input.ResetInputAxes();
            }

            if (active && current.type == EventType.MouseUp && current.button == 0) {
                GUIUtility.hotControl = 0;
                current.Use();
                Input.ResetInputAxes();
            }
        }

        private void RenderDragSourceAt(LayoutNode node, DragSource widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            string id = context.StateId(widget, "drag-source");

            if (GUI.enabled && widget.Payload != null && current != null && !IsInputBlockedByDialog()) {
                if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition)) {
                    context.DragDrop.Active = true;
                    context.DragDrop.SourceId = id;
                    context.DragDrop.Payload = widget.Payload;
                    context.DragDrop.StartPosition = current.mousePosition;
                    context.DragDrop.CurrentPosition = current.mousePosition;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (context.DragDrop.Active && string.Equals(context.DragDrop.SourceId, id, StringComparison.Ordinal) &&
                    (current.type == EventType.MouseDrag || current.type == EventType.MouseMove || current.type == EventType.MouseDown)) {
                    context.DragDrop.CurrentPosition = current.mousePosition;
                    if (current.type == EventType.MouseDrag) {
                        current.Use();
                        Input.ResetInputAxes();
                    }
                }
            }

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                DrawStrokeRect(rect, context.DragDrop.Active && string.Equals(context.DragDrop.SourceId, id, StringComparison.Ordinal) ? Color.black : new Color(0f, 0f, 0f, 0.55f), 1f);
            }

            RenderLayoutChildren(node, context, offset);
            RegisterInputRegion(rect, true);
        }

        private void RenderDropTargetAt(LayoutNode node, DropTarget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool accepts = DragPayloadMatches(context.DragDrop.Payload, widget.AcceptedType);
            bool hovered = context.DragDrop.Active && accepts && Event.current != null && rect.Contains(Event.current.mousePosition);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                if (hovered) DrawFilledRect(rect, new Color(0.86f, 0.86f, 0.86f, 0.65f));
                DrawStrokeRect(rect, hovered ? Color.black : new Color(0f, 0f, 0f, 0.55f), 1f);
                if (node.Children.Count == 0 && !string.IsNullOrEmpty(widget.EmptyText)) {
                    context.Theme.CenterLabel.Draw(rect, GetContent(widget.EmptyText), false, false, false, false);
                }
            }

            RenderLayoutChildren(node, context, offset);

            Event current = Event.current;
            if (GUI.enabled && current != null && current.type == EventType.MouseUp && current.button == 0 &&
                context.DragDrop.Active && accepts && rect.Contains(current.mousePosition)) {
                DragPayload payload = context.DragDrop.Payload;
                context.DragDrop.Clear();
                if (widget.OnDrop != null) MaximumUIRuntime.EnqueueCallback(delegate { widget.OnDrop(payload); });
                current.Use();
                Input.ResetInputAxes();
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderBulletTextAt(LayoutNode node, BulletText widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float size = Mathf.Min(6f, Mathf.Max(3f, rect.height * 0.35f));
            Rect bullet = new Rect(rect.x + 4f, rect.y + Mathf.Max(0f, (rect.height - size) * 0.5f), size, size);
            DrawFilledRect(bullet, Color.black);
            context.Theme.Label.Draw(new Rect(rect.x + 16f, rect.y, Mathf.Max(1f, rect.width - 16f), rect.height), GetContent(widget.Text), false, false, false, false);
        }

        private void RenderValueLabelAt(LayoutNode node, ValueLabel widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float labelWidth = widget.LabelWidth > 0f ? widget.LabelWidth : Mathf.Min(96f, rect.width * 0.45f);
            Color oldContentColor = GUI.contentColor;
            GUI.contentColor = new Color(0f, 0f, 0f, 0.55f);
            context.Theme.Label.Draw(new Rect(rect.x, rect.y, labelWidth, rect.height), GetContent(widget.Label), false, false, false, false);
            GUI.contentColor = oldContentColor;
            context.Theme.Label.Draw(new Rect(rect.x + labelWidth + 6f, rect.y, Mathf.Max(1f, rect.width - labelWidth - 6f), rect.height), GetContent(widget.Value), false, false, false, false);
        }

        private void RenderTextLinkAt(LayoutNode node, TextLink widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool hover;
            bool clicked = DoTextClick(rect, out hover);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnClick);
            }

            Color color = hover ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.02f, 0.14f, 0.34f, 1f);
            Color oldContentColor = GUI.contentColor;
            GUI.contentColor = color;
            context.Theme.Label.Draw(rect, GetContent(widget.Text), false, false, false, false);
            GUI.contentColor = oldContentColor;
            DrawFilledRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 1f), color);
        }

        private void RegisterWidgetOverlays(LayoutNode node, Widget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            bool hovered = current != null && rect.Contains(current.mousePosition);

            if (hovered && !string.IsNullOrEmpty(widget.TooltipText)) {
                string tooltip = widget.TooltipText;
                Vector2 mouse = current.mousePosition;
                context.Overlay.Register(new OverlayRequest {
                    SortOrder = 900,
                    Render = delegate(RenderContext renderContext) {
                        DrawTooltip(renderContext, tooltip, mouse);
                    }
                });
            }

            if (widget.ContextMenuContent == null) return;

            string id = context.StateId(widget, "context-menu");
            ContextMenuState state = context.StateStore.GetOrCreate<ContextMenuState>(id);
            if (current != null && current.type == EventType.MouseDown && current.button == 1 && rect.Contains(current.mousePosition)) {
                state.Open = true;
                state.Position = current.mousePosition;
                current.Use();
                Input.ResetInputAxes();
            }

            if (!state.Open) return;

            Widget content = widget.ContextMenuContent;
            context.Overlay.Register(new OverlayRequest {
                SortOrder = 950,
                Render = delegate(RenderContext renderContext) {
                    DrawContextMenu(renderContext, id, state, content);
                }
            });
        }

        private void DrawTooltip(RenderContext context, string text, Vector2 mousePosition) {
            if (string.IsNullOrEmpty(text)) return;

            float width = Mathf.Min(320f, Mathf.Max(80f, text.Length * 7f + 16f));
            float height = Mathf.Max(22f, context.Theme.FontSize + 10f);
            Rect rect = ClampOverlayRect(new Rect(mousePosition.x + 12f, mousePosition.y + 18f, width, height));
            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                context.Theme.HelpBox.Draw(rect, GetContent(text), false, false, false, false);
            } finally {
                GUI.color = oldColor;
            }
            RegisterInputRegion(rect, true);
        }

        private void DrawContextMenu(RenderContext context, string id, ContextMenuState state, Widget content) {
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) {
                state.Open = false;
                current.Use();
                return;
            }

            LayoutNode measured = layoutEngine.Measure(content, new LayoutConstraints(0f, 260f, 0f, 420f), id + ":content");
            float width = Mathf.Max(120f, measured.DesiredSize.X + 8f);
            float height = Mathf.Max(20f, measured.DesiredSize.Y + 8f);
            state.Rect = ClampOverlayRect(new Rect(state.Position.x, state.Position.y, width, height));

            if (current != null && current.type == EventType.MouseDown && !state.Rect.Contains(current.mousePosition)) {
                state.Open = false;
                current.Use();
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                context.Theme.Dropdown.Draw(state.Rect, GUIContent.none, false, false, false, false);
            } finally {
                GUI.color = oldColor;
            }

            LayoutNode root = layoutEngine.Layout(content, ToMuiRect(new Rect(state.Rect.x + 4f, state.Rect.y + 4f, Mathf.Max(1f, state.Rect.width - 8f), Mathf.Max(1f, state.Rect.height - 8f))), id + ":content");
            RenderLayoutNode(root, context, Vector2.zero);
            RegisterInputRegion(state.Rect, true);
        }

        private void RenderButtonAt(LayoutNode node, Button widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, widget.Text, context.Theme.Button, false);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderSmallButtonAt(LayoutNode node, SmallButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, widget.Text, context.Theme.Button, false);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderArrowButtonAt(LayoutNode node, ArrowButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, ArrowText(widget.Direction), context.Theme.Button, false);
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderToggleAt(LayoutNode node, Toggle widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool inputEnabled = GUI.enabled && !IsInputBlockedByDialog();
            Event current = Event.current;
            if (inputEnabled && current != null && current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition)) {
                bool captured = !widget.Value;
                if (widget.OnChange != null) MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
                current.Use();
                Input.ResetInputAxes();
            }

            float boxSize = Mathf.Min(14f, Mathf.Max(10f, rect.height - 4f));
            Rect boxRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - boxSize) * 0.5f), boxSize, boxSize);
            context.Theme.ToggleBox.Draw(boxRect, GUIContent.none, false, false, widget.Value, false);
            if (widget.Value) {
                Rect checkRect = new Rect(boxRect.x + 3f, boxRect.y + 3f, Mathf.Max(1f, boxRect.width - 6f), Mathf.Max(1f, boxRect.height - 6f));
                context.Theme.ToggleCheck.Draw(checkRect, GUIContent.none, false, false, false, false);
            }

            if (!string.IsNullOrEmpty(widget.Text)) {
                Rect labelRect = new Rect(boxRect.xMax + 5f, rect.y, Mathf.Max(1f, rect.xMax - boxRect.xMax - 5f), rect.height);
                context.Theme.Toggle.Draw(labelRect, GetContent(widget.Text), false, false, false, false);
            }
        }

        private void RenderCheckboxAt(LayoutNode node, Checkbox widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool inputEnabled = GUI.enabled && !IsInputBlockedByDialog();
            Event current = Event.current;
            if (inputEnabled && current != null && current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition)) {
                bool captured = !widget.Value;
                if (widget.OnChange != null) MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
                current.Use();
                Input.ResetInputAxes();
            }

            float boxSize = Mathf.Min(14f, Mathf.Max(10f, rect.height - 4f));
            Rect boxRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - boxSize) * 0.5f), boxSize, boxSize);
            context.Theme.ToggleBox.Draw(boxRect, GUIContent.none, false, false, widget.Value, false);
            if (widget.Value) {
                Rect checkRect = new Rect(boxRect.x + 3f, boxRect.y + 3f, Mathf.Max(1f, boxRect.width - 6f), Mathf.Max(1f, boxRect.height - 6f));
                context.Theme.ToggleCheck.Draw(checkRect, GUIContent.none, false, false, false, false);
            }

            if (!string.IsNullOrEmpty(widget.Text)) {
                Rect labelRect = new Rect(boxRect.xMax + 5f, rect.y, Mathf.Max(1f, rect.xMax - boxRect.xMax - 5f), rect.height);
                context.Theme.Toggle.Draw(labelRect, GetContent(widget.Text), false, false, false, false);
            }
        }

        private void RenderRadioButtonAt(LayoutNode node, RadioButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool inputEnabled = GUI.enabled && !IsInputBlockedByDialog();
            Event current = Event.current;
            if (inputEnabled && current != null && current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition)) {
                if (!widget.Selected && widget.OnSelect != null) MaximumUIRuntime.EnqueueCallback(widget.OnSelect);
                current.Use();
                Input.ResetInputAxes();
            }

            float boxSize = Mathf.Min(14f, Mathf.Max(10f, rect.height - 4f));
            Rect boxRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - boxSize) * 0.5f), boxSize, boxSize);
            context.Theme.ToggleBox.Draw(boxRect, GUIContent.none, false, false, widget.Selected, false);
            if (widget.Selected) {
                Rect dotRect = new Rect(boxRect.x + 4f, boxRect.y + 4f, Mathf.Max(1f, boxRect.width - 8f), Mathf.Max(1f, boxRect.height - 8f));
                context.Theme.ToggleCheck.Draw(dotRect, GUIContent.none, false, false, false, false);
            }

            if (!string.IsNullOrEmpty(widget.Text)) {
                Rect labelRect = new Rect(boxRect.xMax + 5f, rect.y, Mathf.Max(1f, rect.xMax - boxRect.xMax - 5f), rect.height);
                context.Theme.Toggle.Draw(labelRect, GetContent(widget.Text), false, false, false, false);
            }
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
                    MaximumUIRuntime.EnqueueCallback(delegate { callback.DynamicInvoke(captured); });
                }
            }
        }

        private void RenderSliderAt(LayoutNode node, Slider widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float sliderHeight = Mathf.Min(rect.height, GetSliderHeight(context));
            Rect sliderRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - sliderHeight) * 0.5f), rect.width, sliderHeight);
            float value = DoSlider(rect, sliderRect, widget.Value, widget.Min, widget.Max, context);
            if (!Mathf.Approximately(value, widget.Value) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(value); });
            }
        }

        private void RenderInputIntAt(LayoutNode node, InputInt widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            int step = Event.current != null && Event.current.shift ? widget.StepFast : widget.Step;
            int next = widget.Value;
            DrawNumericInputShell(valueRect, FormatInt(widget.Value, widget.Format), context, delegate { next = ClampInt(widget.Value - step, widget.HasMin, widget.Min, widget.HasMax, widget.Max); }, delegate { next = ClampInt(widget.Value + step, widget.HasMin, widget.Min, widget.HasMax, widget.Max); });
            if (next != widget.Value && widget.OnChange != null && !IsInputBlockedByDialog()) {
                int captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderInputFloatAt(LayoutNode node, InputFloat widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            float step = Event.current != null && Event.current.shift ? widget.StepFast : widget.Step;
            float next = widget.Value;
            DrawNumericInputShell(valueRect, FormatFloat(widget.Value, widget.Format), context, delegate { next = ClampFloat(widget.Value - step, widget.HasMin, widget.Min, widget.HasMax, widget.Max); }, delegate { next = ClampFloat(widget.Value + step, widget.HasMin, widget.Min, widget.HasMax, widget.Max); });
            if (!Mathf.Approximately(next, widget.Value) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderInputDoubleAt(LayoutNode node, InputDouble widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            double step = Event.current != null && Event.current.shift ? widget.StepFast : widget.Step;
            double next = widget.Value;
            DrawNumericInputShell(valueRect, FormatDouble(widget.Value, widget.Format), context, delegate { next = ClampDouble(widget.Value - step, widget.HasMin, widget.Min, widget.HasMax, widget.Max); }, delegate { next = ClampDouble(widget.Value + step, widget.HasMin, widget.Min, widget.HasMax, widget.Max); });
            if (Math.Abs(next - widget.Value) > 0.0000001 && widget.OnChange != null && !IsInputBlockedByDialog()) {
                double captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
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
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
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
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderSliderIntAt(LayoutNode node, SliderInt widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            Rect sliderRect;
            Rect textRect;
            SplitSliderValueRect(valueRect, out sliderRect, out textRect);
            int min = Math.Min(widget.Min, widget.Max);
            int max = Math.Max(widget.Min, widget.Max);
            float raw = DoSlider(valueRect, SliderTrackRect(sliderRect, context), widget.Value, min, max, context);
            int next = Mathf.Clamp(Mathf.RoundToInt(raw), min, max);
            context.Theme.Label.Draw(textRect, GetContent(FormatInt(widget.Value, widget.Format)), false, false, false, false);
            if (next != widget.Value && widget.OnChange != null && !IsInputBlockedByDialog()) {
                int captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderSliderFloatAt(LayoutNode node, SliderFloat widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            Rect sliderRect;
            Rect textRect;
            SplitSliderValueRect(valueRect, out sliderRect, out textRect);
            float value = DoSlider(valueRect, SliderTrackRect(sliderRect, context), widget.Value, widget.Min, widget.Max, context);
            context.Theme.Label.Draw(textRect, GetContent(FormatFloat(widget.Value, widget.Format)), false, false, false, false);
            if (!Mathf.Approximately(value, widget.Value) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float captured = value;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderSliderAngleAt(LayoutNode node, SliderAngle widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect valueRect = DrawNumericLabel(rect, widget.Label, context);
            Rect sliderRect;
            Rect textRect;
            SplitSliderValueRect(valueRect, out sliderRect, out textRect);
            float value = DoSlider(valueRect, SliderTrackRect(sliderRect, context), widget.ValueDegrees, widget.MinDegrees, widget.MaxDegrees, context);
            context.Theme.Label.Draw(textRect, GetContent(widget.ValueDegrees.ToString("0") + " deg"), false, false, false, false);
            if (!Mathf.Approximately(value, widget.ValueDegrees) && widget.OnChange != null && !IsInputBlockedByDialog()) {
                float captured = value;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderProgressBarAt(LayoutNode node, ProgressBar widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            float t = widget.Indeterminate ? 0.35f : Mathf.Clamp01(widget.Value);
            Rect fillRect = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, (rect.width - 2f) * t), Mathf.Max(0f, rect.height - 2f));
            context.Theme.TabButton.Draw(fillRect, GUIContent.none, false, false, true, false);
            string text = widget.Text;
            if (string.IsNullOrEmpty(text)) text = ((int)(Mathf.Clamp01(widget.Value) * 100f)).ToString() + "%";
            context.Theme.CenterLabel.Draw(rect, GetContent(text), false, false, false, false);
        }

        private void RenderStatusBadgeAt(LayoutNode node, StatusBadge widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            DrawFilledRect(rect, StatusColor(widget.Kind));
            DrawStrokeRect(rect, Color.black, 1f);
            context.Theme.CenterLabel.Draw(rect, GetContent(widget.Text), false, false, false, false);
        }

        private void RenderLoadingIndicatorAt(LayoutNode node, LoadingIndicator widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float size = Mathf.Min(10f, Mathf.Max(4f, rect.height - 6f));
            float phase = Time.realtimeSinceStartup % 1f;
            float x = rect.x + 3f + (widget.Indeterminate ? Mathf.Round(phase * 4f) : 0f);
            Rect mark = new Rect(x, rect.y + Mathf.Max(0f, (rect.height - size) * 0.5f), size, size);
            DrawFilledRect(mark, Color.black);
            string text = string.IsNullOrEmpty(widget.Text) ? "Loading" : widget.Text;
            context.Theme.Label.Draw(new Rect(rect.x + 20f, rect.y, Mathf.Max(1f, rect.width - 20f), rect.height), GetContent(text), false, false, false, false);
        }

        private void RenderImageAt(LayoutNode node, Image widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
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
            bool clicked = DoButton(rect, string.Empty, context.Theme.Button, false);
            Texture texture = widget.Texture as Texture;
            float imageSize = Mathf.Min(rect.height - 4f, 20f);
            if (texture != null && imageSize > 0f) {
                Color oldColor = GUI.color;
                GUI.color = ToUnityColor(widget.Tint);
                GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + Mathf.Max(0f, (rect.height - imageSize) * 0.5f), imageSize, imageSize), texture, ScaleMode.ScaleToFit, true);
                GUI.color = oldColor;
            }
            if (!string.IsNullOrEmpty(widget.Text)) {
                context.Theme.Label.Draw(new Rect(rect.x + imageSize + 8f, rect.y, Mathf.Max(1f, rect.width - imageSize - 10f), rect.height), GetContent(widget.Text), false, false, false, false);
            }
            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnClick);
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
            Rect textRect = new Rect(rect.x + 18f, rect.y, Mathf.Max(1f, rect.width - 20f), rect.height);
            context.Theme.Label.Draw(new Rect(rect.x + 4f, rect.y, 14f, rect.height), GetContent("S"), false, false, false, false);
            RenderTextInputCore(
                textRect,
                state,
                id,
                widget.Value,
                widget.Placeholder,
                widget.MaxLength,
                context.Theme.TextField,
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
            bool wasFocused = state.Focused || state.WasFocused || focusedBefore;
            if (!state.HasEditingValue || (!focusedBefore && !string.Equals(state.EditingValue, externalValue, StringComparison.Ordinal))) {
                state.EditingValue = ApplyTextInputFilter(externalValue, flags, charFilter, maxLength);
                state.HasEditingValue = true;
            }

            GUI.SetNextControlName(state.ControlName);
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            GUI.color = Color.black;
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
            if (autoFocus && !state.AutoFocusApplied) {
                GUI.FocusControl(state.ControlName);
                state.AutoFocusApplied = true;
                state.Focused = true;
            }

            if (state.Focused && !state.HasFocusStartValue) {
                state.FocusStartValue = state.EditingValue ?? string.Empty;
                state.HasFocusStartValue = true;
            }

            if (state.Focused) MaximumUIRuntime.IsAnyTextFieldFocused = true;

            Event current = Event.current;
            bool handledKey = false;
            if (state.Focused && current != null && current.type == EventType.KeyDown) {
                if (current.keyCode == KeyCode.Escape) {
                    if (HasTextInputFlag(flags, TextInputFlags.EscapeClearsAll)) {
                        next = string.Empty;
                        handledKey = true;
                    } else if (HasTextInputFlag(flags, TextInputFlags.EscapeReverts)) {
                        next = state.HasFocusStartValue ? state.FocusStartValue : externalValue;
                        handledKey = true;
                    }
                } else if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                    (kind != TextInputKind.TextArea || HasTextInputFlag(flags, TextInputFlags.EnterReturnsTrue))) {
                    if (onSubmit != null && !IsInputBlockedByDialog()) {
                        string capturedSubmit = next;
                        MaximumUIRuntime.EnqueueCallback(delegate { onSubmit(capturedSubmit); });
                    }
                    handledKey = true;
                }
            }

            if (!state.Focused && string.IsNullOrEmpty(next) && !string.IsNullOrEmpty(placeholder)) {
                Color oldContentColor = GUI.contentColor;
                GUI.contentColor = new Color(0f, 0f, 0f, 0.45f);
                style.Draw(rect, GetContent(placeholder), false, false, false, false);
                GUI.contentColor = oldContentColor;
            }

            if (!HasTextInputFlag(flags, TextInputFlags.ReadOnly) && !string.Equals(next, state.EditingValue, StringComparison.Ordinal)) {
                state.EditingValue = next;
                Input.ResetInputAxes();
                if (!IsInputBlockedByDialog()) {
                    if (onEdit != null) {
                        string capturedEdit = next;
                        MaximumUIRuntime.EnqueueCallback(delegate { onEdit(capturedEdit); });
                    }
                    if (onChange != null) {
                        string captured = next;
                        MaximumUIRuntime.EnqueueCallback(delegate { onChange(captured); });
                    }
                }
            }

            if (handledKey && current != null) {
                current.Use();
                Input.ResetInputAxes();
            }

            if (wasFocused && !state.Focused) {
                state.HasFocusStartValue = false;
                if (onBlur != null && !IsInputBlockedByDialog()) {
                    string capturedBlur = state.EditingValue ?? string.Empty;
                    MaximumUIRuntime.EnqueueCallback(delegate { onBlur(capturedBlur); });
                }
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
                context.Theme.HelpBox.Draw(rect, GetContent(widget.Message), false, false, false, false);
            }
        }

        private void RenderListBoxAt(LayoutNode node, IListBoxWidget widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(node.Widget, "listbox");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            ISearchableListBoxWidget searchable = node.Widget as ISearchableListBoxWidget;
            string filter = searchable == null ? string.Empty : searchable.ListFilterText;
            if (filter == null) filter = string.Empty;

            List<object> values = new List<object>();
            System.Collections.IEnumerable enumerable = widget.ListItems;
            if (enumerable != null) {
                foreach (object item in enumerable) {
                    string text = widget.GetListText(item);
                    if (!MatchesFilter(text, filter)) continue;
                    values.Add(item);
                }
            }

            float rowHeight = Mathf.Max(18f, context.Theme.FontSize + 8f);
            Rect listRect = rect;
            if (searchable != null) {
                Rect filterRect = new Rect(rect.x + 2f, rect.y + 2f, Mathf.Max(1f, rect.width - 4f), rowHeight);
                string nextFilter = GUI.TextField(filterRect, filter, context.Theme.TextField);
                if (!string.Equals(nextFilter, filter, StringComparison.Ordinal) && searchable.ListOnFilterChange != null && !IsInputBlockedByDialog()) {
                    string capturedFilter = nextFilter ?? string.Empty;
                    MaximumUIRuntime.EnqueueCallback(delegate { searchable.ListOnFilterChange(capturedFilter); });
                }
                if (string.IsNullOrEmpty(filter) && !string.IsNullOrEmpty(searchable.ListFilterPlaceholder) && (Event.current == null || Event.current.type == EventType.Repaint)) {
                    context.Theme.Label.Draw(filterRect, GetContent(searchable.ListFilterPlaceholder), false, false, false, false);
                }
                listRect = new Rect(rect.x, filterRect.yMax + 3f, rect.width, Mathf.Max(1f, rect.yMax - filterRect.yMax - 3f));
            }

            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, listRect.width - 16f), Mathf.Max(listRect.height, rowHeight * Mathf.Max(1, values.Count)));
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(listRect, GUIContent.none, false, false, false, false);
            }

            state.Position = GUI.BeginScrollView(listRect, state.Position, viewRect, false, viewRect.height > listRect.height);
            try {
                for (int i = 0; i < values.Count; i++) {
                    object item = values[i];
                    Rect rowRect = new Rect(2f, i * rowHeight + 1f, Mathf.Max(1f, viewRect.width - 4f), Mathf.Max(1f, rowHeight - 2f));
                    bool selected = object.Equals(item, widget.ListValue);
                    if (DoButton(rowRect, widget.GetListText(item), context.Theme.TabButton, selected) && widget.ListOnChange != null && !IsInputBlockedByDialog()) {
                        object captured = item;
                        Delegate callback = widget.ListOnChange;
                        MaximumUIRuntime.EnqueueCallback(delegate { callback.DynamicInvoke(captured); });
                    }
                }
            } finally {
                GUI.EndScrollView();
            }
        }

        private void RenderDebugLinesAt(LayoutNode node, IDebugLinesWidget widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(node.Widget, "debug-lines");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            float rowHeight = Mathf.Max(18f, context.Theme.FontSize + 8f);
            IList<string> lines = widget.GetDebugLines();
            int visibleRows = Mathf.Max(1, widget.VisibleRows);
            float contentHeight = rowHeight * Mathf.Max(visibleRows + 1, lines.Count + 1) + 4f;
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, rect.width - 16f), Mathf.Max(rect.height, contentHeight));

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            }

            state.Position = GUI.BeginScrollView(rect, state.Position, viewRect, false, viewRect.height > rect.height);
            try {
                Rect title = new Rect(2f, 2f, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    context.Theme.TabButton.Draw(title, GetContent(widget.DebugTitle), false, false, true, false);
                }

                for (int i = 0; i < lines.Count; i++) {
                    Rect line = new Rect(6f, title.yMax + i * rowHeight, Mathf.Max(1f, viewRect.width - 12f), rowHeight);
                    if (Event.current == null || Event.current.type == EventType.Repaint) {
                        context.Theme.Label.Draw(line, GetContent(lines[i]), false, false, false, false);
                    }
                }
            } finally {
                GUI.EndScrollView();
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderThemeEditorAt(LayoutNode node, ThemeEditor widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(node.Widget, "theme-editor");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            ThemeTokens tokens = widget.Tokens ?? new ThemeTokens();
            float rowHeight = Mathf.Max(18f, context.Theme.FontSize + 8f);
            int visibleRows = Mathf.Max(1, ThemeEditorTokens.VisibleRowCount(widget));
            int totalRows = Mathf.Max(1, ThemeEditorTokens.TotalRowCount(widget));
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, rect.width - 16f), rowHeight * (totalRows + 1) + 4f);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            }

            state.Position = GUI.BeginScrollView(rect, state.Position, viewRect, false, totalRows > visibleRows);
            try {
                Rect title = new Rect(2f, 2f, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    context.Theme.TabButton.Draw(title, GetContent("Theme Editor"), false, false, true, false);
                }

                int row = 0;
                if (widget.ShowColors) {
                    for (int i = 0; i < ThemeEditorTokens.ColorTokens.Length; i++) {
                        ThemeColorToken token = ThemeEditorTokens.ColorTokens[i];
                        Rect rowRect = new Rect(2f, title.yMax + row * rowHeight, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                        RenderThemeColorRow(context, widget, tokens, token, rowRect);
                        row++;
                    }
                }

                if (widget.ShowScalars) {
                    for (int i = 0; i < ThemeEditorTokens.ScalarTokens.Length; i++) {
                        ThemeScalarToken token = ThemeEditorTokens.ScalarTokens[i];
                        Rect rowRect = new Rect(2f, title.yMax + row * rowHeight, Mathf.Max(1f, viewRect.width - 4f), rowHeight);
                        RenderThemeScalarRow(context, widget, tokens, token, rowRect);
                        row++;
                    }
                }
            } finally {
                GUI.EndScrollView();
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderThemeColorRow(RenderContext context, ThemeEditor editor, ThemeTokens tokens, ThemeColorToken token, Rect rowRect) {
            Rect labelRect = new Rect(rowRect.x + 6f, rowRect.y, Mathf.Max(1f, rowRect.width - 96f), rowRect.height);
            Rect swatchRect = new Rect(rowRect.xMax - 84f, rowRect.y + 3f, 32f, Mathf.Max(1f, rowRect.height - 6f));
            Rect valueRect = new Rect(swatchRect.xMax + 5f, rowRect.y, 44f, rowRect.height);
            bool clicked = DoButton(swatchRect, string.Empty, context.Theme.ComboBoxItem, false);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                context.Theme.Label.Draw(labelRect, GetContent(ThemeEditorTokens.ColorName(token)), false, false, false, false);
                DrawFilledRect(swatchRect, ToUnityColor(tokens.GetColor(token)));
                DrawStrokeRect(swatchRect, Color.black, 1f);
                context.Theme.Label.Draw(valueRect, GetContent(ColorSummary(tokens.GetColor(token))), false, false, false, false);
            }

            if (clicked && !IsInputBlockedByDialog()) {
                ThemeTokens next = tokens.Clone();
                next.SetColor(token, NextColor(tokens.GetColor(token)));
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
                context.Theme.Label.Draw(labelRect, GetContent(ThemeEditorTokens.ScalarName(token)), false, false, false, false);
                context.Theme.Label.Draw(valueRect, GetContent(value.ToString("0.##")), false, false, false, false);
            }

            if (DoButton(minusRect, "-", context.Theme.Button, false) && !IsInputBlockedByDialog()) {
                ApplyThemeScalarEdit(editor, tokens, token, value - Mathf.Max(0.1f, editor.ScalarStep));
            }

            if (DoButton(plusRect, "+", context.Theme.Button, false) && !IsInputBlockedByDialog()) {
                ApplyThemeScalarEdit(editor, tokens, token, value + Mathf.Max(0.1f, editor.ScalarStep));
            }
        }

        private static void ApplyThemeScalarEdit(ThemeEditor editor, ThemeTokens tokens, ThemeScalarToken token, float value) {
            ThemeTokens next = tokens.Clone();
            next.SetScalar(token, ClampThemeScalar(token, value));
            ApplyThemeEdit(editor, next);
        }

        private static void ApplyThemeEdit(ThemeEditor editor, ThemeTokens next) {
            if (editor == null || next == null) return;

            if (editor.OnChange != null) {
                ThemeTokens captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { editor.OnChange(captured); });
            } else if (editor.Tokens != null) {
                editor.Tokens.CopyFrom(next);
            }
        }

        private void RenderSeparatorAt(LayoutNode node, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect lineRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - 1f) * 0.5f), rect.width, 1f);
            context.Theme.Separator.Draw(lineRect, GUIContent.none, false, false, false, false);
        }

        private void RenderSeparatorTextAt(LayoutNode node, SeparatorText widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            float y = rect.y + Mathf.Max(0f, (rect.height - 1f) * 0.5f);
            float textWidth = Mathf.Min(rect.width, Mathf.Max(20f, widget.Text.Length * context.Theme.FontSize * 0.55f + 12f));
            float lineWidth = Mathf.Max(0f, (rect.width - textWidth) * 0.5f);
            context.Theme.Separator.Draw(new Rect(rect.x, y, lineWidth, 1f), GUIContent.none, false, false, false, false);
            context.Theme.Separator.Draw(new Rect(rect.xMax - lineWidth, y, lineWidth, 1f), GUIContent.none, false, false, false, false);
            context.Theme.CenterLabel.Draw(new Rect(rect.x + lineWidth, rect.y, textWidth, rect.height), GetContent(widget.Text), false, false, false, false);
        }

        private void RenderScrollViewAt(LayoutNode node, ScrollView widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "scroll");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            Rect viewRect = CalculateScrollViewRect(node, rect);
            state.Position = GUI.BeginScrollView(rect, state.Position, viewRect, widget.AlwaysShowHorizontal, widget.AlwaysShowVertical);
            try {
                RenderLayoutChildren(node, context, new Vector2(-node.Rect.X, -node.Rect.Y));
            } finally {
                GUI.EndScrollView();
            }
        }

        private void RenderFoldoutAt(LayoutNode node, Foldout widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, Mathf.Max(20f, context.Theme.FontSize + 8f)));
            string text = (widget.Open ? "[-] " : "[+] ") + widget.Label;
            bool clicked = DoButton(headerRect, text, context.Theme.FoldoutHeader, widget.Open);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool open = !widget.Open;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(open); });
            }

            if (widget.Open) RenderLayoutChildren(node, context, offset);
        }

        private void RenderCollapsingHeaderAt(LayoutNode node, CollapsingHeader widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, Mathf.Max(20f, context.Theme.FontSize + 8f)));
            bool clicked = DoButton(headerRect, (widget.Open ? "[-] " : "[+] ") + widget.Label, context.Theme.FoldoutHeader, widget.Open);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool open = !widget.Open;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(open); });
            }

            if (widget.Open) RenderLayoutChildren(node, context, offset);
        }

        private void RenderClosableCollapsingHeaderAt(LayoutNode node, ClosableCollapsingHeader widget, RenderContext context, Vector2 offset) {
            if (!widget.Visible) return;

            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, Mathf.Max(20f, context.Theme.FontSize + 8f)));
            float closeSize = Mathf.Min(headerRect.height - 4f, 18f);
            Rect closeRect = new Rect(headerRect.xMax - closeSize - 2f, headerRect.y + 2f, closeSize, closeSize);
            Rect toggleRect = new Rect(headerRect.x, headerRect.y, Mathf.Max(1f, headerRect.width - closeSize - 4f), headerRect.height);

            bool clicked = DoButton(toggleRect, (widget.Open ? "[-] " : "[+] ") + widget.Label, context.Theme.FoldoutHeader, widget.Open);
            if (clicked && widget.OnChange != null && !IsInputBlockedByDialog()) {
                bool open = !widget.Open;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(open); });
            }

            if (DoButton(closeRect, "x", context.Theme.Button, false) && !IsInputBlockedByDialog()) {
                if (widget.OnClose != null) MaximumUIRuntime.EnqueueCallback(widget.OnClose);
            }

            if (widget.Open) RenderLayoutChildren(node, context, offset);
        }

        private void RenderTabsAt(LayoutNode node, Tabs widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (widget.Children.Count == 0) return;

            string id = context.StateId(widget, "tabs");
            TabsState state = context.StateStore.GetOrCreate<TabsState>(id);
            float headerHeight = Mathf.Max(20f, context.Theme.FontSize + 8f);
            List<Tab> tabs = new List<Tab>();
            List<TabItemButton> leadingButtons = new List<TabItemButton>();
            List<TabItemButton> trailingButtons = new List<TabItemButton>();
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
            Rect[] tabRects = new Rect[tabs.Count];
            int dropTarget = -1;
            float tabWidth = tabs.Count <= 0 ? tabAreaWidth : tabAreaWidth / tabs.Count;
            Event current = Event.current;

            for (int i = 0; i < tabs.Count; i++) {
                Tab tab = tabs[i];
                Rect tabRect = new Rect(x + tabWidth * i, rect.y, tabWidth, headerHeight);
                tabRects[i] = tabRect;
                if (current != null && tabRect.Contains(current.mousePosition)) dropTarget = i;

                bool closeTriggered = false;
                if (tab.Closable) {
                    float closeSize = Mathf.Min(headerHeight - 4f, 18f);
                    Rect closeRect = new Rect(tabRect.xMax - closeSize - 2f, tabRect.y + 2f, closeSize, closeSize);
                    if (DoButton(closeRect, "x", context.Theme.Button, false)) {
                        closeTriggered = true;
                        CloseTab(widget, tab, i);
                    }
                }

                Rect labelRect = tab.Closable ? new Rect(tabRect.x, tabRect.y, Mathf.Max(1f, tabRect.width - headerHeight), tabRect.height) : tabRect;
                string title = FormatTabTitle(tab);
                if (!closeTriggered && DoButton(labelRect, title, context.Theme.TabButton, i == selected)) {
                    int captured = i;
                    if (captured != selected && widget.OnChange != null && !IsInputBlockedByDialog()) {
                        MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
                    }
                }

                if (widget.CloseWithMiddleMouse && tab.Closable && current != null && current.type == EventType.MouseDown && current.button == 2 && tabRect.Contains(current.mousePosition)) {
                    CloseTab(widget, tab, i);
                    current.Use();
                }

                if (widget.Reorderable && current != null && current.type == EventType.MouseDown && current.button == 0 && tabRect.Contains(current.mousePosition)) {
                    state.DragFrom = i;
                    state.DragStart = current.mousePosition;
                    state.Dragging = false;
                }
            }

            if (widget.Reorderable && current != null && state.DragFrom >= 0) {
                if (current.type == EventType.MouseDrag && (current.mousePosition - state.DragStart).sqrMagnitude > 36f) {
                    state.Dragging = true;
                } else if (current.type == EventType.MouseUp) {
                    if (state.Dragging && dropTarget >= 0 && dropTarget != state.DragFrom && widget.OnReorder != null && !IsInputBlockedByDialog()) {
                        int from = state.DragFrom;
                        int to = dropTarget;
                        MaximumUIRuntime.EnqueueCallback(delegate { widget.OnReorder(from, to); });
                    }
                    state.ClearDrag();
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
            Rect rect = OffsetRect(node.Rect, offset);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            }

            float x = rect.x + 2f;
            for (int i = 0; i < widget.Children.Count; i++) {
                Menu menu = widget.Children[i] as Menu;
                if (menu == null) continue;

                float width = Mathf.Max(44f, menu.Title.Length * context.Theme.FontSize * 0.55f + 18f);
                Rect menuRect = new Rect(x, rect.y + 2f, width, Mathf.Max(1f, rect.height - 4f));
                bool selected = state.Open && state.OpenIndex == i;
                if (DoButton(menuRect, menu.Title, context.Theme.TabButton, selected) && !IsInputBlockedByDialog()) {
                    if (selected) {
                        state.Open = false;
                        state.OpenIndex = -1;
                    } else {
                        state.Open = true;
                        state.OpenIndex = i;
                        state.Anchor = menuRect;
                    }
                } else if (state.Open && Event.current != null && menuRect.Contains(Event.current.mousePosition)) {
                    state.OpenIndex = i;
                    state.Anchor = menuRect;
                }

                x += width + 2f;
            }

            if (state.Open && state.OpenIndex >= 0 && state.OpenIndex < widget.Children.Count) {
                Menu menu = widget.Children[state.OpenIndex] as Menu;
                if (menu != null) {
                    context.Overlay.Register(new OverlayRequest {
                        SortOrder = 940,
                        Render = delegate(RenderContext renderContext) {
                            DrawMenuPopup(renderContext, state, menu);
                        }
                    });
                }
            }

            RegisterInputRegion(rect, true);
        }

        private void DrawMenuPopup(RenderContext context, MenuBarState state, Menu menu) {
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) {
                state.Open = false;
                state.OpenIndex = -1;
                current.Use();
                return;
            }

            float itemHeight = Mathf.Max(20f, context.Theme.FontSize + 8f);
            float width = 120f;
            float height = 4f;
            for (int i = 0; i < menu.Children.Count; i++) {
                MenuItem item = menu.Children[i] as MenuItem;
                MenuSeparator separator = menu.Children[i] as MenuSeparator;
                if (item != null) {
                    string text = (item.Checked ? "[x] " : "    ") + item.Text;
                    if (!string.IsNullOrEmpty(item.Shortcut)) text += "    " + item.Shortcut;
                    width = Mathf.Max(width, text.Length * context.Theme.FontSize * 0.55f + 18f);
                    height += itemHeight;
                } else if (separator != null) {
                    width = Mathf.Max(width, separator.Text.Length * context.Theme.FontSize * 0.55f + 36f);
                    height += string.IsNullOrEmpty(separator.Text) ? 8f : itemHeight;
                } else if (menu.Children[i] is Menu) {
                    Menu childMenu = (Menu)menu.Children[i];
                    width = Mathf.Max(width, childMenu.Title.Length * context.Theme.FontSize * 0.55f + 32f);
                    height += itemHeight;
                }
            }

            state.PopupRect = ClampOverlayRect(new Rect(state.Anchor.x, state.Anchor.yMax + 2f, width, Mathf.Max(itemHeight, height)));
            if (current != null && current.type == EventType.MouseDown && !state.PopupRect.Contains(current.mousePosition) && !state.Anchor.Contains(current.mousePosition)) {
                state.Open = false;
                state.OpenIndex = -1;
                current.Use();
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = WithAlpha(oldColor, context.Theme.WindowOpacity);
            try {
                context.Theme.Dropdown.Draw(state.PopupRect, GUIContent.none, false, false, false, false);
            } finally {
                GUI.color = oldColor;
            }

            GUI.BeginGroup(state.PopupRect);
            try {
                float y = 2f;
                for (int i = 0; i < menu.Children.Count; i++) {
                    MenuItem item = menu.Children[i] as MenuItem;
                    MenuSeparator separator = menu.Children[i] as MenuSeparator;
                    Menu childMenu = menu.Children[i] as Menu;
                    if (item != null) {
                        Rect row = new Rect(2f, y, Mathf.Max(1f, state.PopupRect.width - 4f), itemHeight);
                        string text = (item.Checked ? "[x] " : "    ") + item.Text;
                        if (!string.IsNullOrEmpty(item.Shortcut)) text += "    " + item.Shortcut;
                        bool oldEnabled = GUI.enabled;
                        GUI.enabled = GUI.enabled && item.Enabled;
                        bool clicked = DoButton(row, text, context.Theme.ComboBoxItem, false);
                        GUI.enabled = oldEnabled;
                        if (clicked && item.OnClick != null && !IsInputBlockedByDialog()) {
                            Action action = item.OnClick;
                            state.Open = false;
                            state.OpenIndex = -1;
                            MaximumUIRuntime.EnqueueCallback(action);
                        }
                        y += itemHeight;
                    } else if (separator != null) {
                        if (string.IsNullOrEmpty(separator.Text)) {
                            context.Theme.Separator.Draw(new Rect(6f, y + 3f, Mathf.Max(1f, state.PopupRect.width - 12f), 1f), GUIContent.none, false, false, false, false);
                            y += 8f;
                        } else {
                            context.Theme.CenterLabel.Draw(new Rect(4f, y, Mathf.Max(1f, state.PopupRect.width - 8f), itemHeight), GetContent(separator.Text), false, false, false, false);
                            y += itemHeight;
                        }
                    } else if (childMenu != null) {
                        Rect row = new Rect(2f, y, Mathf.Max(1f, state.PopupRect.width - 4f), itemHeight);
                        context.Theme.ComboBoxItem.Draw(row, GetContent(childMenu.Title + " >"), false, false, false, false);
                        y += itemHeight;
                    }
                }
            } finally {
                GUI.EndGroup();
            }

            RegisterInputRegion(state.PopupRect, true);
        }

        private void RenderSelectableAt(LayoutNode node, Selectable widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            bool additive = current != null && (current.control || current.command);
            bool range = current != null && current.shift;
            bool doubleClick = current != null && current.clickCount > 1;
            bool clicked = DoButton(rect, widget.Text, context.Theme.TabButton, widget.Selected);
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
                    MaximumUIRuntime.EnqueueCallback(delegate { widget.OnSelect(request); });
                }

                if (widget.OnChange != null) {
                    MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
                }

                if (doubleClick && widget.AllowDoubleClick && widget.OnDoubleClick != null) {
                    MaximumUIRuntime.EnqueueCallback(widget.OnDoubleClick);
                }
            }
        }

        private void RenderTreeNodeAt(LayoutNode node, TreeNode widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, Mathf.Max(20f, context.Theme.FontSize + 8f)));
            float arrowWidth = Mathf.Min(22f, headerRect.height);
            Rect arrowRect = new Rect(headerRect.x, headerRect.y, arrowWidth, headerRect.height);
            Rect labelRect = new Rect(headerRect.x + arrowWidth, headerRect.y, Mathf.Max(1f, headerRect.width - arrowWidth), headerRect.height);
            bool clicked = false;
            bool toggled = false;
            string prefix = widget.Leaf ? (widget.Bullet ? "* " : "- ") : (widget.Open ? "v" : ">");

            if (widget.Leaf) {
                clicked = DoButton(headerRect, prefix + " " + widget.Label, context.Theme.FoldoutHeader, widget.Selected);
            } else if (widget.OpenOnArrow) {
                toggled = DoButton(arrowRect, prefix, context.Theme.FoldoutHeader, widget.Open);
                clicked = DoButton(labelRect, widget.Label, context.Theme.FoldoutHeader, widget.Selected);
            } else {
                clicked = DoButton(headerRect, prefix + " " + widget.Label, context.Theme.FoldoutHeader, widget.Selected);
                toggled = clicked;
            }

            Event current = Event.current;
            if (!widget.Leaf && widget.OpenOnDoubleClick && current != null && current.clickCount > 1 && headerRect.Contains(current.mousePosition)) {
                toggled = true;
            }

            if (!IsInputBlockedByDialog()) {
                if (toggled && widget.OnOpenChange != null) {
                    bool open = !widget.Open;
                    MaximumUIRuntime.EnqueueCallback(delegate { widget.OnOpenChange(open); });
                }
                if (clicked && widget.OnSelect != null) MaximumUIRuntime.EnqueueCallback(widget.OnSelect);
            }

            if (widget.Open && !widget.Leaf) RenderLayoutChildren(node, context, offset);
        }

        private void RenderTreeLeafAt(LayoutNode node, TreeLeaf widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            string text = (widget.Bullet ? "* " : "- ") + widget.Label;
            if (DoButton(rect, text, context.Theme.FoldoutHeader, widget.Selected) && widget.OnSelect != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnSelect);
            }
        }

        private void RenderColorButtonAt(LayoutNode node, ColorButton widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            bool clicked = DoButton(rect, string.Empty, context.Theme.Button, false);
            float swatchSize = Mathf.Min(rect.height - 4f, 22f);
            Rect swatchRect = new Rect(rect.x + 3f, rect.y + Mathf.Max(0f, (rect.height - swatchSize) * 0.5f), swatchSize, swatchSize);
            DrawColorSwatch(swatchRect, widget.Value, widget.ShowCheckerboard, context);

            string text = widget.Label;
            if (string.IsNullOrEmpty(text)) text = FormatColor(widget.Value, widget.ShowAlpha, ColorDisplayMode.Hex);
            context.Theme.Label.Draw(new Rect(swatchRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.xMax - swatchRect.xMax - 9f), rect.height), GetContent(text), false, false, false, false);

            if (clicked && widget.OnClick != null && !IsInputBlockedByDialog()) {
                MaximumUIRuntime.EnqueueCallback(widget.OnClick);
            }
        }

        private void RenderColorEdit3At(LayoutNode node, ColorEdit3 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorEditCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, false, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorEdit4At(LayoutNode node, ColorEdit4 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorEditCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, true, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorPicker3At(LayoutNode node, ColorPicker3 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorPickerCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, false, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorPicker4At(LayoutNode node, ColorPicker4 widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorPickerCore(OffsetRect(node.Rect, offset), widget.Label, widget.Value, true, widget.DisplayMode, widget.ShowPreview, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private void RenderColorEditAt(LayoutNode node, ColorEdit widget, RenderContext context, Vector2 offset) {
            MuiColor next;
            if (RenderColorEditCore(OffsetRect(node.Rect, offset), string.Empty, widget.Value, widget.ShowAlpha, widget.DisplayMode, true, context, out next) && widget.OnChange != null) {
                MuiColor captured = next;
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(captured); });
            }
        }

        private bool RenderColorEditCore(Rect rect, string label, MuiColor value, bool alpha, ColorDisplayMode displayMode, bool showPreview, RenderContext context, out MuiColor next) {
            next = value;
            Rect contentRect = rect;
            if (!string.IsNullOrEmpty(label)) {
                float labelWidth = Mathf.Min(72f, rect.width * 0.3f);
                Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
                context.Theme.Label.Draw(labelRect, GetContent(label), false, false, false, false);
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
            context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);

            float rowHeight = Mathf.Max(18f, Mathf.Min(24f, rect.height / (alpha ? 5f : 4f)));
            Rect headerRect = new Rect(rect.x + 4f, rect.y + 4f, Mathf.Max(1f, rect.width - 8f), rowHeight);
            if (showPreview) {
                Rect swatchRect = new Rect(headerRect.x, headerRect.y, Mathf.Min(44f, headerRect.height * 1.8f), headerRect.height);
                DrawColorSwatch(swatchRect, value, true, context);
                string text = string.IsNullOrEmpty(label) ? FormatColor(value, alpha, displayMode) : label;
                context.Theme.Label.Draw(new Rect(swatchRect.xMax + 6f, headerRect.y, Mathf.Max(1f, headerRect.xMax - swatchRect.xMax - 6f), headerRect.height), GetContent(text), false, false, false, false);
            } else {
                context.Theme.Label.Draw(headerRect, GetContent(string.IsNullOrEmpty(label) ? FormatColor(value, alpha, displayMode) : label), false, false, false, false);
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
            if (labelWidth > 0f) context.Theme.Label.Draw(new Rect(rect.x, rect.y, labelWidth, rect.height), GetContent(widget.Label), false, false, false, false);

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
                MaximumUIRuntime.EnqueueCallback(delegate { widget.OnChange(x, y, z); });
            }
        }

        private void RenderPlotLinesAt(LayoutNode node, PlotLines widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            if (widget.Values == null || widget.Values.Count == 0) return;

            float min;
            float max;
            GetPlotRange(widget.Values, widget.HasRange, widget.Min, widget.Max, out min, out max);
            float step = widget.Values.Count <= 1 ? rect.width : rect.width / (widget.Values.Count - 1);
            for (int i = 0; i < widget.Values.Count; i++) {
                float t = Normalize(widget.Values[i], min, max);
                float x = rect.x + step * i;
                float y = rect.yMax - t * rect.height;
                DrawFilledRect(new Rect(x, y, 2f, 2f), Color.black);
            }
            if (!string.IsNullOrEmpty(widget.OverlayText)) {
                context.Theme.CenterLabel.Draw(rect, GetContent(widget.OverlayText), false, false, false, false);
            }
        }

        private void RenderPlotHistogramAt(LayoutNode node, PlotHistogram widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            if (widget.Values == null || widget.Values.Count == 0) return;

            float min;
            float max;
            GetPlotRange(widget.Values, widget.HasRange, widget.Min, widget.Max, out min, out max);
            float barWidth = Mathf.Max(1f, rect.width / widget.Values.Count);
            for (int i = 0; i < widget.Values.Count; i++) {
                float t = Normalize(widget.Values[i], min, max);
                float height = rect.height * t;
                DrawFilledRect(new Rect(rect.x + i * barWidth, rect.yMax - height, Mathf.Max(1f, barWidth - 1f), height), new Color(0.66f, 0.66f, 0.66f, 1f));
            }
            if (!string.IsNullOrEmpty(widget.OverlayText)) {
                context.Theme.CenterLabel.Draw(rect, GetContent(widget.OverlayText), false, false, false, false);
            }
        }

        private void RenderDrawCanvasAt(LayoutNode node, DrawCanvas widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                if (widget.DrawBackground) {
                    context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
                }

                DrawList drawList = new DrawList();
                MuiRect canvasRect = ToMuiRect(rect);
                if (widget.Clip) drawList.PushClip(canvasRect);
                if (widget.OnDraw != null) widget.OnDraw(drawList, canvasRect);
                if (widget.Clip) drawList.PopClip();
                unityPainter.Paint(drawList);
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderDrawListWidgetAt(LayoutNode node, DrawListWidget widget, RenderContext context, Vector2 offset) {
            Rect rect = OffsetRect(node.Rect, offset);
            if (Event.current == null || Event.current.type == EventType.Repaint) {
                if (widget.DrawBackground) {
                    context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
                }

                DrawList drawList = new DrawList();
                MuiRect canvasRect = ToMuiRect(rect);
                if (widget.Clip) drawList.PushClip(canvasRect);
                if (widget.Source != null) {
                    drawList.Append(widget.Source, widget.LocalCoordinates ? new MuiVec2(canvasRect.X, canvasRect.Y) : new MuiVec2(0f, 0f));
                }
                if (widget.Clip) drawList.PopClip();
                unityPainter.Paint(drawList);
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderVirtualListAt(LayoutNode node, VirtualList widget, RenderContext context, Vector2 offset) {
            string id = context.StateId(widget, "virtual-list");
            ScrollViewState state = context.StateStore.GetOrCreate<ScrollViewState>(id);
            Rect rect = OffsetRect(node.Rect, offset);
            float rowHeight = Mathf.Max(1f, widget.RowHeight);
            float totalHeight = Mathf.Max(rect.height, widget.ItemCount * rowHeight);
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, rect.width - 16f), totalHeight);

            if (Event.current == null || Event.current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(rect, GUIContent.none, false, false, false, false);
            }

            state.Position = GUI.BeginScrollView(rect, state.Position, viewRect, false, widget.AlwaysShowVertical || totalHeight > rect.height);
            try {
                int first = Mathf.Max(0, Mathf.FloorToInt(state.Position.y / rowHeight) - 1);
                int visible = Mathf.CeilToInt(rect.height / rowHeight) + 3;
                int last = Mathf.Min(widget.ItemCount, first + visible);

                for (int i = first; i < last; i++) {
                    Widget rowWidget = widget.BuildItem == null ? null : widget.BuildItem(i);
                    if (rowWidget == null || rowWidget is EmptyWidget) continue;

                    string oldPath = context.Path;
                    context.Path = node.Path + ".row" + i.ToString();
                    try {
                        LayoutNode rowNode = layoutEngine.Layout(rowWidget, new MuiRect(0f, i * rowHeight, viewRect.width, rowHeight), context.Path);
                        RenderLayoutNode(rowNode, context, Vector2.zero);
                    } catch (Exception ex) {
                        Debug.LogException(ex);
                    } finally {
                        context.Path = oldPath;
                    }
                }
            } finally {
                GUI.EndScrollView();
            }

            RegisterInputRegion(rect, true);
        }

        private void RenderComboBoxAt(LayoutNode node, Widget widget, IComboBoxWidget combo, RenderContext context, Vector2 offset) {
            string label = combo.ComboLabel ?? string.Empty;
            string id = context.StateId(widget, "combo");
            ComboBoxState state = context.StateStore.GetOrCreate<ComboBoxState>(id);
            if (string.IsNullOrEmpty(state.ControlName)) state.ControlName = id;
            if (string.IsNullOrEmpty(state.FilterControlName)) state.FilterControlName = id + ":filter";

            Rect rect = OffsetRect(node.Rect, offset);
            Rect buttonRect = rect;
            if (label.Length > 0) {
                float labelWidth = combo.ComboLabelWidth > 0f ? combo.ComboLabelWidth : Mathf.Min(72f, rect.width * 0.4f);
                Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
                context.Theme.Label.Draw(labelRect, GetContent(label), false, false, false, false);
                buttonRect = new Rect(labelRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.width - labelWidth - 6f), rect.height);
            }

            string selectedText = combo.GetComboText(combo.ComboValue);
            GUI.SetNextControlName(state.ControlName);
            bool clicked = DoButton(buttonRect, selectedText, context.Theme.ComboBoxButton, state.Open);
            if (IsUsableLayoutRect(buttonRect)) {
                state.ButtonRect = ToComboButtonStateRect(buttonRect);
                state.HasButtonRect = true;
            }

            if (clicked && !IsInputBlockedByDialog()) {
                if (state.Dropdown != null && state.Dropdown.Show) {
                    state.Dropdown.Hide();
                    state.Open = false;
                } else {
                    OpenComboOverlay(widget, combo, context, state, true);
                }
            } else if (state.Dropdown != null && state.Dropdown.Show && state.HasButtonRect) {
                OpenComboOverlay(widget, combo, context, state, false);
            }
        }

        private void OpenComboOverlay(Widget widget, IComboBoxWidget combo, RenderContext context, ComboBoxState state, bool focus) {
            List<object> values = new List<object>();
            List<string> labels = new List<string>();
            ISearchableComboBoxWidget searchable = combo as ISearchableComboBoxWidget;
            string filter = state.FilterText ?? string.Empty;
            System.Collections.IEnumerable enumerable = combo.ComboOptions;
            if (enumerable != null) {
                foreach (object item in enumerable) {
                    string text = combo.GetComboText(item);
                    if (searchable != null && filter.Length > 0 && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    values.Add(item);
                    labels.Add(text);
                }
            }

            string id = context.StateId(widget, "combo");
            if (state.Dropdown == null) {
                state.Dropdown = new DropdownOverlay(StableHash(id), context.Theme.FontSize, context.Theme);
            }

            Rect buttonRect = state.HasButtonRect ? state.ButtonRect : new Rect(0f, 0f, 1f, 1f);
            state.WindowStateId = currentWindow == null ? null : context.StateId(currentWindow, "window");
            state.Items = labels.ToArray();
            state.MaxVisibleItems = combo.ComboMaxVisibleItems;
            state.Searchable = searchable != null;
            state.FilterPlaceholder = searchable == null ? string.Empty : searchable.ComboFilterPlaceholder;
            state.Callback = delegate(int index) {
                if (index < 0 || index >= values.Count) return;

                Delegate callback = combo.ComboOnChange;
                if (callback != null) {
                    object selected = values[index];
                    MaximumUIRuntime.EnqueueCallback(delegate { callback.DynamicInvoke(selected); });
                }
            };

            context.HideOtherComboBoxes(state);
            state.Open = true;
            if (searchable != null) {
                string placeholder = state.FilterPlaceholder;
                Action<string> onFilter = delegate(string value) {
                    state.FilterText = value ?? string.Empty;
                };
                state.Dropdown.Set(GetTopWindowRect(), buttonRect, state.Items, state.Callback, state.MaxVisibleItems, state.FilterText, onFilter, placeholder);
                if (focus) state.Dropdown.Focus(state.FilterControlName);
            } else {
                state.Dropdown.Set(GetTopWindowRect(), buttonRect, state.Items, state.Callback, state.MaxVisibleItems);
                if (focus) state.Dropdown.Focus(state.ControlName);
            }
            context.RegisterComboBox(state);
        }

        private Rect GetTopWindowRect() {
            if (currentWindow == null || currentContext == null) return new Rect(0f, 0f, 0f, 0f);
            WindowState state = currentContext.StateStore.GetOrCreate<WindowState>(currentContext.StateId(currentWindow, "window"));
            return state.Rect;
        }

        private void RenderDialog(Dialog widget, RenderContext context) {
            string id = context.StateId(widget, "dialog");
            string dialogPath = context.Path;
            context.Overlay.Register(new OverlayRequest {
                SortOrder = 1000,
                Render = delegate(RenderContext renderContext) {
                    DrawDialogOverlay(renderContext, widget, id, dialogPath, false, delegate {
                        if (widget.OnClose != null) MaximumUIRuntime.EnqueueCallback(widget.OnClose);
                    });
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
                context.Theme.Dropdown.Draw(rect, GetContent(text), false, false, false, false);
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

        private void DrawDialogOverlay(RenderContext context, Dialog dialog, string stateId, string dialogPath, bool drawBackdrop, Action close) {
            DialogState state = context.StateStore.GetOrCreate<DialogState>(stateId);
            if (!state.Initialized) {
                state.Rect = CenterRect(new Rect(0f, 0f, dialog.Width, dialog.Height));
                state.Initialized = true;
            }

            state.Rect.width = dialog.Width;
            state.Rect.height = dialog.Height;
            state.Rect = ClipDialogPosition(state.Rect);

            if (drawBackdrop) {
                Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(screenRect, context.Theme.Black);
                GUI.color = oldColor;
                RegisterInputRegion(screenRect, true);
            }

            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && dialog.CloseOnEscape) {
                if (close != null) close();
                current.Use();
            }

            if (current != null && current.type == EventType.MouseDown && dialog.CloseOnOutsideClick) {
                Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!state.Rect.Contains(mousePosition)) {
                    if (close != null) close();
                    current.Use();
                }
            }

            DrawDialogFrame(context, dialog, state, stateId);

            string oldPath = context.Path;
            context.Path = dialogPath;
            dialogDepth++;
            try {
                RectOffset padding = context.Theme.DialogContentPadding;
                currentWindowContentOffset = new Vector2(padding.left, padding.top);
                LayoutNode root = layoutEngine.Layout(dialog, ToMuiRect(state.Rect), dialogPath);
                RenderLayoutChildren(root, context, Vector2.zero);
            } finally {
                dialogDepth--;
                currentWindowContentOffset = Vector2.zero;
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
                    context.Theme.Dialog.Draw(state.Rect, GetContent(dialog.Title), false, false, false, false);
                }
            } finally {
                GUI.color = oldColor;
            }

            DoDrag(state, StableHash(stateId + ":drag"), new Rect(state.Rect.x, state.Rect.y, state.Rect.width, 24f));
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

        private bool IsInputBlockedByDialog() {
            return currentContext != null && currentContext.IsModalOpen && dialogDepth == 0;
        }

        private static bool HasDialogWidget(Widget widget) {
            if (widget == null) return false;
            if (widget is Dialog) return true;
            if (widget is EmptyWidget) return false;

            Window window = widget as Window;
            if (window != null && !window.Visible) return false;

            Foldout foldout = widget as Foldout;
            if (foldout != null && !foldout.Open) return false;

            Tabs tabs = widget as Tabs;
            if (tabs != null) {
                if (tabs.Children.Count == 0) return false;
                int selected = Mathf.Clamp(tabs.SelectedIndex, 0, tabs.Children.Count - 1);
                return HasDialogWidget(tabs.Children[selected]);
            }

            for (int i = 0; i < widget.Children.Count; i++) {
                if (HasDialogWidget(widget.Children[i])) return true;
            }

            return false;
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

        private bool DoButton(Rect rect, string text, GUIStyle style, bool on) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            bool hover = current != null && rect.Contains(current.mousePosition);
            bool active = GUIUtility.hotControl == controlId;
            bool clicked = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    GUIUtility.hotControl = controlId;
                    active = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    GUIUtility.hotControl = 0;
                    clicked = hover;
                    active = false;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                style.Draw(rect, GetContent(text), hover, active, on, GUIUtility.keyboardControl == controlId);
            }

            return clicked;
        }

        private Rect DrawNumericLabel(Rect rect, string label, RenderContext context) {
            if (string.IsNullOrEmpty(label)) return rect;

            float labelWidth = Mathf.Min(82f, rect.width * 0.36f);
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            context.Theme.Label.Draw(labelRect, GetContent(label), false, false, false, false);
            return new Rect(labelRect.xMax + 6f, rect.y, Mathf.Max(1f, rect.width - labelWidth - 6f), rect.height);
        }

        private void DrawNumericInputShell(Rect rect, string valueText, RenderContext context, Action minus, Action plus) {
            float buttonWidth = Mathf.Min(24f, Mathf.Max(18f, rect.height));
            Rect fieldRect = new Rect(rect.x, rect.y, Mathf.Max(1f, rect.width - buttonWidth * 2f), rect.height);
            Rect minusRect = new Rect(fieldRect.xMax, rect.y, buttonWidth, rect.height);
            Rect plusRect = new Rect(minusRect.xMax, rect.y, buttonWidth, rect.height);

            context.Theme.TextField.Draw(fieldRect, GetContent(valueText), false, false, false, false);
            if (DoButton(minusRect, "-", context.Theme.Button, false) && minus != null && !IsInputBlockedByDialog()) minus();
            if (DoButton(plusRect, "+", context.Theme.Button, false) && plus != null && !IsInputBlockedByDialog()) plus();
        }

        private bool DoNumericDrag(Rect rect, string valueText, RenderContext context, Action<float> onDelta) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            bool hover = current != null && rect.Contains(current.mousePosition);
            bool active = GUIUtility.hotControl == controlId;
            bool changed = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    GUIUtility.hotControl = controlId;
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
                    GUIUtility.hotControl = 0;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(rect, GetContent(valueText), hover, active, false, false);
            }

            return changed;
        }

        private void SplitSliderValueRect(Rect rect, out Rect sliderRect, out Rect textRect) {
            float textWidth = Mathf.Min(58f, Mathf.Max(42f, rect.width * 0.3f));
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
            hover = current != null && rect.Contains(current.mousePosition);
            bool active = GUIUtility.hotControl == controlId;
            bool clicked = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    GUIUtility.hotControl = controlId;
                    active = true;
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    GUIUtility.hotControl = 0;
                    clicked = hover;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            return clicked;
        }

        private static void DrawFilledRect(Rect rect, Color color) {
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
            bool hover = current != null && controlRect.Contains(current.mousePosition);
            bool active = GUIUtility.hotControl == controlId;
            float next = value;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    GUIUtility.hotControl = controlId;
                    active = true;
                    next = SliderValueFromMouse(current.mousePosition.x, trackRect, min, max);
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseDrag && active) {
                    next = SliderValueFromMouse(current.mousePosition.x, trackRect, min, max);
                    current.Use();
                    Input.ResetInputAxes();
                } else if (current.type == EventType.MouseUp && current.button == 0 && active) {
                    GUIUtility.hotControl = 0;
                    active = false;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                context.Theme.HorizontalSlider.Draw(trackRect, GUIContent.none, hover, active, false, false);
                float t = max <= min ? 0f : Mathf.InverseLerp(min, max, next);
                float thumbWidth = Mathf.Max(1f, context.Theme.HorizontalSliderThumb.fixedWidth);
                float thumbHeight = Mathf.Max(trackRect.height, context.Theme.HorizontalSliderThumb.fixedHeight);
                Rect thumbRect = new Rect(
                    trackRect.x + Mathf.Max(0f, trackRect.width - thumbWidth) * t,
                    controlRect.y + Mathf.Max(0f, (controlRect.height - thumbHeight) * 0.5f),
                    thumbWidth,
                    thumbHeight);
                context.Theme.HorizontalSliderThumb.Draw(thumbRect, GUIContent.none, hover, active, false, false);
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
            bool hover = current != null && rect.Contains(current.mousePosition);
            bool active = GUIUtility.hotControl == controlId;
            next = Mathf.Clamp01(value);
            bool changed = false;

            if (GUI.enabled && !IsInputBlockedByDialog() && current != null) {
                if (current.type == EventType.MouseDown && current.button == 0 && hover) {
                    GUIUtility.hotControl = controlId;
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
                    GUIUtility.hotControl = 0;
                    current.Use();
                    Input.ResetInputAxes();
                }
            }

            if (current == null || current.type == EventType.Repaint) {
                context.Theme.TextField.Draw(rect, GUIContent.none, hover, active, false, false);
                Rect fillRect = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, (rect.width - 2f) * Mathf.Clamp01(next)), Mathf.Max(0f, rect.height - 2f));
                DrawFilledRect(fillRect, ToUnityColor(ComponentColor(component, next)));
                context.Theme.Label.Draw(new Rect(rect.x + 5f, rect.y, Mathf.Max(1f, rect.width - 10f), rect.height), GetContent(ComponentName(component) + " " + FormatColorComponent(next, displayMode)), false, false, false, false);
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
            DrawStrokeRect(rect, Color.black, 1f);
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
                MaximumUIRuntime.EnqueueCallback(action);
            }
        }

        private static string FormatTabTitle(Tab tab) {
            if (tab == null) return string.Empty;
            string title = tab.Title ?? string.Empty;
            if (tab.Unsaved) title += "*";
            return title;
        }

        private static void CloseTab(Tabs tabs, Tab tab, int tabIndex) {
            if (tab != null && tab.OnClose != null) {
                Action action = tab.OnClose;
                MaximumUIRuntime.EnqueueCallback(action);
                return;
            }

            if (tabs != null && tabs.OnClose != null) {
                int captured = tabIndex;
                MaximumUIRuntime.EnqueueCallback(delegate { tabs.OnClose(captured); });
            }
        }

        private Rect CalculateScrollViewRect(LayoutNode node, Rect viewportRect) {
            float width = viewportRect.width;
            float height = viewportRect.height;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode child = node.Children[i];
                width = Mathf.Max(width, child.Rect.Right - node.Rect.X);
                height = Mathf.Max(height, child.Rect.Bottom - node.Rect.Y);
            }

            return new Rect(0f, 0f, Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        private Rect ToComboButtonStateRect(Rect buttonRect) {
            if (currentWindow == null || currentContext == null) return buttonRect;

            WindowState state = currentContext.StateStore.GetOrCreate<WindowState>(currentContext.StateId(currentWindow, "window"));
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

            int controlId = GUIUtility.GetControlID(hint, FocusType.Passive, dragRect);
            bool ownsDrag = GUIUtility.hotControl == controlId;
            if (current.type == EventType.MouseDown && current.button == 0 && dragRect.Contains(current.mousePosition)) {
                GUIUtility.hotControl = controlId;
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

            if (current.type == EventType.MouseUp && current.button == 0 && GUIUtility.hotControl == controlId) {
                GUIUtility.hotControl = 0;
            }
        }

        private void DoDrag(DialogState state, int hint, Rect dragRect) {
            if (state == null) return;

            Event current = Event.current;
            if (current == null) return;

            int controlId = GUIUtility.GetControlID(hint, FocusType.Passive, dragRect);
            bool ownsDrag = GUIUtility.hotControl == controlId;
            if (current.type == EventType.MouseDown && current.button == 0 && dragRect.Contains(current.mousePosition)) {
                GUIUtility.hotControl = controlId;
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

            if (current.type == EventType.MouseUp && current.button == 0 && GUIUtility.hotControl == controlId) {
                GUIUtility.hotControl = 0;
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

        private static MuiColor NextColor(MuiColor color) {
            if (color.R >= color.G && color.R >= color.B) return new MuiColor(0f, 0.75f, 0.25f, color.A);
            if (color.G >= color.R && color.G >= color.B) return new MuiColor(0.25f, 0.35f, 1f, color.A);
            return new MuiColor(1f, 0.25f, 0.25f, color.A);
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

        private static string ColorSummary(MuiColor color) {
            return ((int)(Mathf.Clamp01(color.A) * 100f)).ToString() + "%";
        }

        private static float ClampThemeScalar(ThemeScalarToken token, float value) {
            if (token == ThemeScalarToken.BorderWidth) return Mathf.Clamp(value, 0f, 8f);
            if (token == ThemeScalarToken.Radius) return Mathf.Clamp(value, 0f, 16f);
            if (token == ThemeScalarToken.FontSize) return Mathf.Clamp(value, 8f, 24f);
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

        private static Color StatusColor(StatusBadgeKind kind) {
            if (kind == StatusBadgeKind.Success) return new Color(0.58f, 0.82f, 0.62f, 1f);
            if (kind == StatusBadgeKind.Warning) return new Color(0.95f, 0.88f, 0.50f, 1f);
            if (kind == StatusBadgeKind.Error) return new Color(0.94f, 0.55f, 0.55f, 1f);
            return new Color(0.62f, 0.78f, 0.96f, 1f);
        }

        private static bool MatchesFilter(string text, string filter) {
            if (string.IsNullOrEmpty(filter)) return true;
            if (text == null) text = string.Empty;
            return text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void GetPlotRange(System.Collections.Generic.IList<float> values, bool hasRange, float explicitMin, float explicitMax, out float min, out float max) {
            if (hasRange) {
                min = explicitMin;
                max = explicitMax;
                if (max <= min) max = min + 1f;
                return;
            }

            min = values[0];
            max = values[0];
            for (int i = 1; i < values.Count; i++) {
                if (values[i] < min) min = values[i];
                if (values[i] > max) max = values[i];
            }
            if (max <= min) max = min + 1f;
        }

        private static float Normalize(float value, float min, float max) {
            if (max <= min) return 0f;
            return Mathf.Clamp01((value - min) / (max - min));
        }

        private static string ArrowText(ArrowDirection direction) {
            if (direction == ArrowDirection.Left) return "<";
            if (direction == ArrowDirection.Right) return ">";
            if (direction == ArrowDirection.Up) return "^";
            return "v";
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

        private static float GetTableHeaderHeight(Table table, RenderContext context) {
            if (table != null && table.HeaderHeight > 0f) return table.HeaderHeight;
            if (context != null && context.Theme != null) return Mathf.Max(20f, context.Theme.FontSize + 8f);
            return 22f;
        }

        private static string FormatColumnTitle(TableColumn column) {
            if (column == null) return string.Empty;

            string title = column.Title ?? string.Empty;
            if (column.SortDirection == TableSortDirection.Ascending) return title + " ^";
            if (column.SortDirection == TableSortDirection.Descending) return title + " v";
            return title;
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

        private void RegisterInputRegion(Rect rect, bool active) {
            if (!active) return;

            Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool containsMouse = rect.Contains(mousePosition);
            if (containsMouse) MaximumUIRuntime.IsMouseOverAnyWindow = true;
            if (containsMouse && AnyMouseButtonDown()) mouseDownOnMaximumUi = true;
        }

        private void CaptureInputIfNeeded() {
            if (!AnyMouseButton()) {
                mouseDownOnMaximumUi = false;
                return;
            }

            if (mouseDownOnMaximumUi || MaximumUIRuntime.IsMouseOverAnyWindow) {
                Input.ResetInputAxes();
            }
        }

        private void RecordItemQuery(RenderContext context, LayoutNode node, Widget widget, Vector2 offset) {
            if (context == null || context.Query == null || node == null || widget == null) return;

            Rect rect = OffsetRect(node.Rect, offset);
            Event current = Event.current;
            bool hovered = current != null && rect.Contains(current.mousePosition);
            bool active = hovered && current != null &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseDrag);
            bool clicked = hovered && current != null && current.type == EventType.MouseUp && current.button == 0;
            if (context.Navigation != null && IsNavigationFocusable(widget)) {
                context.Navigation.Register(widget, node.Path, ToMuiRect(rect), GUI.enabled && widget.Enabled);
                if (hovered && current != null && current.type == EventType.MouseDown && current.button == 0) {
                    if (widget.Key != null) context.Navigation.Focus(widget.Key);
                    else context.Navigation.FocusPath(node.Path);
                }
            }
            bool focused = context.Navigation != null && context.Navigation.IsFocused(widget, node.Path);
            context.Query.RecordItem(
                widget,
                node.Path,
                ToMuiRect(rect),
                hovered,
                active,
                focused,
                clicked);
        }

        private void RecordWindowQuery(RenderContext context, Window widget, WindowState state) {
            if (context == null || context.Query == null || widget == null || state == null) return;

            float buttonSize = context.Theme.Window.fontSize * 1.4f;
            float closeX = state.Rect.xMax - buttonSize;
            float foldX = widget.Closable ? closeX - buttonSize : closeX;
            Rect titleRect = new Rect(state.Rect.x, state.Rect.y, Mathf.Max(0f, foldX - state.Rect.x), widget.FoldedHeight);
            Event current = Event.current;
            bool hovered = current != null && state.Rect.Contains(current.mousePosition);
            bool titleHovered = current != null && titleRect.Contains(current.mousePosition);
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
                MaximumUIRuntime.IsAnyTextFieldFocused || MaximumUIRuntime.IsModalOpen || (context.Navigation != null && context.Navigation.HasFocus),
                MaximumUIRuntime.IsAnyTextFieldFocused);
            if (context.Navigation != null && context.Navigation.Count > 0) {
                context.Query.SetNavigation(context.Navigation.HasFocus, true);
            }
        }

        private static Rect ClipWindowPosition(Rect rect, bool keepFullyVisible, float minWidth, float minHeight, float foldedHeight) {
            float maxWidth = Mathf.Max(minWidth, Screen.width - 20f);
            float maxHeight = Mathf.Max(foldedHeight, Screen.height - 20f);
            rect.width = Mathf.Clamp(rect.width, Mathf.Min(minWidth, maxWidth), maxWidth);
            rect.height = Mathf.Clamp(rect.height, keepFullyVisible ? Mathf.Min(minHeight, maxHeight) : foldedHeight, maxHeight);
            if (keepFullyVisible) {
                rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
                rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            } else {
                rect.x = Mathf.Clamp(rect.x, -rect.width * 0.9f, Screen.width - rect.width * 0.1f);
                rect.y = Mathf.Clamp(rect.y, -rect.height * 0.9f, Screen.height - rect.height * 0.1f);
            }

            return rect;
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
                   widget is Toggle ||
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
