using System;
using System.Collections.Generic;

namespace MaximumUI {
    public sealed class WidgetPainter {
        private readonly ThemeTokens theme;

        public WidgetPainter()
            : this(ThemePalettes.MetroDark()) {
        }

        public WidgetPainter(ThemeTokens theme) {
            this.theme = theme ?? ThemePalettes.MetroDark();
        }

        public DrawList Paint(LayoutNode root, WidgetPaintState state) {
            DrawList drawList = new DrawList();
            Paint(root, state ?? new WidgetPaintState(), drawList);
            return drawList;
        }

        public void Paint(LayoutNode node, WidgetPaintState state, DrawList drawList) {
            if (node == null || node.Widget == null || node.Widget is EmptyWidget || drawList == null) return;

            Widget widget = node.Widget;
            if (widget is Window) PaintWindow(node, (Window)widget, state, drawList);
            else if (widget is Dialog) PaintDialog(node, (Dialog)widget, state, drawList);
            else if (widget is Panel) PaintPanel(node, state, drawList);
            else if (widget is TwoPaneLayout) PaintTwoPaneLayout(node, (TwoPaneLayout)widget, state, drawList);
            else if (widget is DragSource) PaintDragSource(node, state, drawList);
            else if (widget is DropTarget) PaintDropTarget(node, (DropTarget)widget, state, drawList);
            else if (widget is Table) PaintTable(node, (Table)widget, state, drawList);
            else if (widget is TableHeaderRow) PaintTableHeaderRow(node, state, drawList);
            else if (widget is TableCell) PaintTableCell(node, state, drawList);
            else if (widget is Label) PaintLabel(node, (Label)widget, state, drawList);
            else if (widget is BulletText) PaintBulletText(node, (BulletText)widget, state, drawList);
            else if (widget is ValueLabel) PaintValueLabel(node, (ValueLabel)widget, state, drawList);
            else if (widget is TextLink) PaintTextLink(node, (TextLink)widget, state, drawList);
            else if (widget is Button) PaintButton(node, (Button)widget, state, drawList);
            else if (widget is SmallButton) PaintSmallButton(node, (SmallButton)widget, state, drawList);
            else if (widget is ArrowButton) PaintArrowButton(node, (ArrowButton)widget, state, drawList);
            else if (widget is Toggle) PaintToggle(node, (Toggle)widget, state, drawList);
            else if (widget is Checkbox) PaintCheckbox(node, (Checkbox)widget, state, drawList);
            else if (widget is RadioButton) PaintRadioButton(node, (RadioButton)widget, state, drawList);
            else if (widget is ISegmentedControlWidget) PaintSegmentedControl(node, (ISegmentedControlWidget)widget, state, drawList);
            else if (widget is Slider) PaintSlider(node, (Slider)widget, state, drawList);
            else if (widget is InputInt) PaintNumericInput(node, ((InputInt)widget).Label, FormatInt(((InputInt)widget).Value, ((InputInt)widget).Format), state, drawList);
            else if (widget is InputFloat) PaintNumericInput(node, ((InputFloat)widget).Label, FormatFloat(((InputFloat)widget).Value, ((InputFloat)widget).Format), state, drawList);
            else if (widget is InputDouble) PaintNumericInput(node, ((InputDouble)widget).Label, FormatDouble(((InputDouble)widget).Value, ((InputDouble)widget).Format), state, drawList);
            else if (widget is DragInt) PaintDragNumeric(node, ((DragInt)widget).Label, FormatInt(((DragInt)widget).Value, ((DragInt)widget).Format), state, drawList);
            else if (widget is DragFloat) PaintDragNumeric(node, ((DragFloat)widget).Label, FormatFloat(((DragFloat)widget).Value, ((DragFloat)widget).Format), state, drawList);
            else if (widget is SliderInt) PaintSliderWithLabel(node, ((SliderInt)widget).Label, ((SliderInt)widget).Value, ((SliderInt)widget).Min, ((SliderInt)widget).Max, FormatInt(((SliderInt)widget).Value, ((SliderInt)widget).Format), state, drawList);
            else if (widget is SliderFloat) PaintSliderWithLabel(node, ((SliderFloat)widget).Label, ((SliderFloat)widget).Value, ((SliderFloat)widget).Min, ((SliderFloat)widget).Max, FormatFloat(((SliderFloat)widget).Value, ((SliderFloat)widget).Format), state, drawList);
            else if (widget is SliderAngle) PaintSliderWithLabel(node, ((SliderAngle)widget).Label, ((SliderAngle)widget).ValueDegrees, ((SliderAngle)widget).MinDegrees, ((SliderAngle)widget).MaxDegrees, ((SliderAngle)widget).ValueDegrees.ToString("0") + " deg", state, drawList);
            else if (widget is ProgressBar) PaintProgressBar(node, (ProgressBar)widget, state, drawList);
            else if (widget is StatusBadge) PaintStatusBadge(node, (StatusBadge)widget, state, drawList);
            else if (widget is LoadingIndicator) PaintLoadingIndicator(node, (LoadingIndicator)widget, state, drawList);
            else if (widget is Image) PaintImage(node, (Image)widget, state, drawList);
            else if (widget is ImageButton) PaintImageButton(node, (ImageButton)widget, state, drawList);
            else if (widget is TextField) PaintTextField(node, (TextField)widget, state, drawList);
            else if (widget is TextArea) PaintTextArea(node, (TextArea)widget, state, drawList);
            else if (widget is PasswordField) PaintPasswordField(node, (PasswordField)widget, state, drawList);
            else if (widget is SearchBox) PaintSearchBox(node, (SearchBox)widget, state, drawList);
            else if (widget is IListBoxWidget) PaintListBox(node, (IListBoxWidget)widget, state, drawList);
            else if (widget is IDebugLinesWidget) PaintDebugLines(node, (IDebugLinesWidget)widget, state, drawList);
            else if (widget is ThemeEditor) PaintThemeEditor(node, (ThemeEditor)widget, state, drawList);
            else if (widget is HelpBox) PaintHelpBox(node, (HelpBox)widget, state, drawList);
            else if (widget is Separator) PaintSeparator(node, drawList);
            else if (widget is SeparatorText) PaintSeparatorText(node, (SeparatorText)widget, drawList);
            else if (widget is IComboBoxWidget) PaintComboBox(node, (IComboBoxWidget)widget, state, drawList);
            else if (widget is Foldout) PaintFoldout(node, (Foldout)widget, state, drawList);
            else if (widget is CollapsingHeader) PaintCollapsingHeader(node, ((CollapsingHeader)widget).Label, ((CollapsingHeader)widget).Open, ((CollapsingHeader)widget).Framed, false, state, drawList);
            else if (widget is ClosableCollapsingHeader) PaintClosableCollapsingHeader(node, (ClosableCollapsingHeader)widget, state, drawList);
            else if (widget is Selectable) PaintSelectable(node, (Selectable)widget, state, drawList);
            else if (widget is TreeNode) PaintTreeNode(node, (TreeNode)widget, state, drawList);
            else if (widget is TreeLeaf) PaintTreeLeaf(node, (TreeLeaf)widget, state, drawList);
            else if (widget is ColorButton) PaintColorButton(node, (ColorButton)widget, state, drawList);
            else if (widget is ColorEdit3) PaintColorEditCore(node, ((ColorEdit3)widget).Label, ((ColorEdit3)widget).Value, false, ((ColorEdit3)widget).DisplayMode, ((ColorEdit3)widget).ShowPreview, state, drawList);
            else if (widget is ColorEdit4) PaintColorEditCore(node, ((ColorEdit4)widget).Label, ((ColorEdit4)widget).Value, true, ((ColorEdit4)widget).DisplayMode, ((ColorEdit4)widget).ShowPreview, state, drawList);
            else if (widget is ColorPicker3) PaintColorPickerCore(node, ((ColorPicker3)widget).Label, ((ColorPicker3)widget).Value, false, ((ColorPicker3)widget).DisplayMode, ((ColorPicker3)widget).ShowPreview, state, drawList);
            else if (widget is ColorPicker4) PaintColorPickerCore(node, ((ColorPicker4)widget).Label, ((ColorPicker4)widget).Value, true, ((ColorPicker4)widget).DisplayMode, ((ColorPicker4)widget).ShowPreview, state, drawList);
            else if (widget is ColorEdit) PaintColorEdit(node, (ColorEdit)widget, state, drawList);
            else if (widget is VectorField) PaintVectorField(node, (VectorField)widget, state, drawList);
            else if (widget is PlotLines) PaintPlotLines(node, (PlotLines)widget, state, drawList);
            else if (widget is PlotHistogram) PaintPlotHistogram(node, (PlotHistogram)widget, state, drawList);
            else if (widget is DrawCanvas) PaintDrawCanvas(node, (DrawCanvas)widget, state, drawList);
            else if (widget is DrawListWidget) PaintDrawListWidget(node, (DrawListWidget)widget, state, drawList);
            else if (widget is VirtualList) PaintVirtualList(node, (VirtualList)widget, state, drawList);
            else if (widget is Tabs) PaintTabs(node, (Tabs)widget, state, drawList);
            else if (widget is MenuBar) PaintMenuBar(node, (MenuBar)widget, state, drawList);

            for (int i = 0; i < node.Children.Count; i++) {
                Paint(node.Children[i], state, drawList);
            }
        }

        private void PaintWindow(LayoutNode node, Window window, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.WindowBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(node.Rect.X, node.Rect.Y + 2f, node.Rect.Width, 20f), window.Title, null, TextColor(node));
        }

        private void PaintDialog(LayoutNode node, Dialog dialog, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.DialogBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(node.Rect.X, node.Rect.Y + 2f, node.Rect.Width, 20f), dialog.Title, null, TextColor(node));
        }

        private void PaintPanel(LayoutNode node, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.PanelBg);
        }

        private void PaintTwoPaneLayout(LayoutNode node, TwoPaneLayout layout, WidgetPaintState state, DrawList drawList) {
            if (node.Children.Count < 2) return;

            MuiRect first = node.Children[0].Rect;
            MuiRect second = node.Children[1].Rect;
            MuiRect splitter;
            if (layout.Orientation == TwoPaneOrientation.Horizontal) {
                splitter = new MuiRect(first.Right, node.Rect.Y, Math.Max(1f, second.X - first.Right), node.Rect.Height);
            } else {
                splitter = new MuiRect(node.Rect.X, first.Bottom, node.Rect.Width, Math.Max(1f, second.Y - first.Bottom));
            }
            drawList.FillRect(splitter, theme.Separator);
        }

        private void PaintDragSource(LayoutNode node, WidgetPaintState state, DrawList drawList) {
            drawList.StrokeRect(node.Rect, state.IsActive(node) ? theme.Accent : theme.Border, theme.BorderWidth);
        }

        private void PaintDropTarget(LayoutNode node, DropTarget dropTarget, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, state.IsHovered(node) ? theme.FieldHoverBg : theme.FieldBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            if (node.Children.Count == 0 && !string.IsNullOrEmpty(dropTarget.EmptyText)) {
                drawList.Text(new MuiRect(node.Rect.X + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 12f), node.Rect.Height), dropTarget.EmptyText, null, theme.TextDisabled);
            }
        }

        private void PaintTable(LayoutNode node, Table table, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.PanelBg);
            if (table.ShowOuterBorder) drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            PaintTableRowBackgrounds(node, table, drawList);
            if (!table.ShowHeader || !HasVisibleTableColumns(table) || HasExplicitHeaderRow(node)) return;

            float headerHeight = Math.Min(table.HeaderHeight > 0f ? table.HeaderHeight : 22f, node.Rect.Height);
            float[] widths = GetTableColumnWidths(table, GetTableColumnCount(table, node), node.Rect.Width);
            float x = node.Rect.X;
            int columnIndex = 0;
            for (int i = 0; i < table.Children.Count && columnIndex < widths.Length; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null || !column.Visible) continue;

                MuiRect rect = new MuiRect(x, node.Rect.Y, widths[columnIndex], headerHeight);
                drawList.FillRect(rect, theme.SelectedBg);
                drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
                drawList.Text(new MuiRect(rect.X + 4f, rect.Y, Math.Max(0f, rect.Width - 8f), rect.Height), FormatColumnTitle(column), null, TextColor(node));
                x += widths[columnIndex] + table.ColumnGap;
                columnIndex++;
            }
        }

        private void PaintTableRowBackgrounds(LayoutNode node, Table table, DrawList drawList) {
            int dataRow = 0;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode rowNode = node.Children[i];
                TableRow row = rowNode.Widget as TableRow;
                if (row == null) continue;

                if (row.HasBackground) {
                    drawList.FillRect(rowNode.Rect, row.Background);
                } else if (table.AlternatingRows && dataRow % 2 == 1) {
                    drawList.FillRect(rowNode.Rect, new MuiColor(theme.Text.R, theme.Text.G, theme.Text.B, 0.025f));
                }

                if (table.ShowInnerBorders) {
                    drawList.FillRect(new MuiRect(rowNode.Rect.X, rowNode.Rect.Bottom - 1f, rowNode.Rect.Width, 1f), theme.Separator);
                }

                dataRow++;
            }
        }

        private void PaintTableHeaderRow(LayoutNode node, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.SelectedBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
        }

        private void PaintTableCell(LayoutNode node, WidgetPaintState state, DrawList drawList) {
            TableCell cell = node.Widget as TableCell;
            if (cell != null && cell.HasBackground) drawList.FillRect(node.Rect, cell.Background);
            if (cell == null || cell.ShowBorder) drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
        }

        private void PaintLabel(LayoutNode node, Label label, WidgetPaintState state, DrawList drawList) {
            drawList.Text(node.Rect, label.Text, label.Style, label.Role == LabelRole.Muted && node.Widget.Enabled ? theme.TextMuted : TextColor(node));
        }

        private void PaintBulletText(LayoutNode node, BulletText bulletText, WidgetPaintState state, DrawList drawList) {
            float size = Math.Min(6f, Math.Max(3f, node.Rect.Height * 0.35f));
            MuiRect bullet = new MuiRect(node.Rect.X + 4f, node.Rect.Y + Math.Max(0f, (node.Rect.Height - size) * 0.5f), size, size);
            drawList.FillRect(bullet, TextColor(node));
            drawList.Text(new MuiRect(node.Rect.X + 16f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 16f), node.Rect.Height), bulletText.Text, null, TextColor(node));
        }

        private void PaintValueLabel(LayoutNode node, ValueLabel valueLabel, WidgetPaintState state, DrawList drawList) {
            float labelWidth = valueLabel.LabelWidth > 0f ? valueLabel.LabelWidth : Math.Min(96f, node.Rect.Width * 0.45f);
            drawList.Text(new MuiRect(node.Rect.X, node.Rect.Y, labelWidth, node.Rect.Height), valueLabel.Label, null, theme.TextMuted);
            drawList.Text(new MuiRect(node.Rect.X + labelWidth + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Width - labelWidth - 6f), node.Rect.Height), valueLabel.Value, null, TextColor(node));
        }

        private void PaintTextLink(LayoutNode node, TextLink link, WidgetPaintState state, DrawList drawList) {
            MuiColor color = state.IsHovered(node) ? theme.AccentHover : theme.Accent;
            drawList.Text(node.Rect, link.Text, null, color);
            drawList.FillRect(new MuiRect(node.Rect.X, node.Rect.Bottom - 2f, Math.Max(0f, node.Rect.Width), 1f), color);
        }

        private void PaintButton(LayoutNode node, Button button, WidgetPaintState state, DrawList drawList) {
            MuiColor background = ControlBg(node, state);
            MuiColor text = TextColor(node);
            if (button.Enabled && button.Variant == ButtonVariant.Primary) {
                background = state.IsHovered(node) || state.IsActive(node) ? theme.AccentHover : theme.Accent;
                text = theme.OnAccent;
            } else if (button.Enabled && button.Variant == ButtonVariant.Danger) {
                background = theme.ErrorBg;
            } else if (button.Enabled && button.Variant == ButtonVariant.Quiet && !state.IsHovered(node) && !state.IsActive(node)) {
                background = new MuiColor(0f, 0f, 0f, 0f);
            }
            if (background.A > 0f) drawList.FillRect(node.Rect, background);
            if (button.Variant == ButtonVariant.Default || button.Variant == ButtonVariant.Danger || state.IsFocused(node))
                drawList.StrokeRect(node.Rect, state.IsFocused(node) ? theme.FocusRing : theme.Border, theme.BorderWidth);
            drawList.Text(node.Rect, button.Text, null, text);
        }

        private void PaintSmallButton(LayoutNode node, SmallButton button, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, ControlBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            drawList.Text(node.Rect, button.Text, null, TextColor(node));
        }

        private void PaintArrowButton(LayoutNode node, ArrowButton button, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, ControlBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            drawList.Text(node.Rect, ArrowText(button.Direction), null, TextColor(node));
        }

        private void PaintToggle(LayoutNode node, Toggle toggle, WidgetPaintState state, DrawList drawList) {
            float boxSize = Math.Min(theme.ToggleBoxSize, Math.Max(10f, node.Rect.Height - 4f));
            MuiRect box = new MuiRect(node.Rect.X, node.Rect.Y + Math.Max(0f, (node.Rect.Height - boxSize) * 0.5f), boxSize, boxSize);
            drawList.FillRect(box, FieldBg(node, state));
            drawList.StrokeRect(box, theme.Border, theme.BorderWidth);
            if (toggle.Value) {
                drawList.FillRect(new MuiRect(box.X + 3f, box.Y + 3f, Math.Max(1f, box.Width - 6f), Math.Max(1f, box.Height - 6f)), theme.Accent);
            }

            drawList.Text(new MuiRect(box.Right + 5f, node.Rect.Y, Math.Max(0f, node.Rect.Right - box.Right - 5f), node.Rect.Height), toggle.Text, null, TextColor(node));
        }

        private void PaintCheckbox(LayoutNode node, Checkbox checkbox, WidgetPaintState state, DrawList drawList) {
            float boxSize = Math.Min(theme.ToggleBoxSize, Math.Max(10f, node.Rect.Height - 4f));
            MuiRect box = new MuiRect(node.Rect.X, node.Rect.Y + Math.Max(0f, (node.Rect.Height - boxSize) * 0.5f), boxSize, boxSize);
            drawList.FillRect(box, FieldBg(node, state));
            drawList.StrokeRect(box, theme.Border, theme.BorderWidth);
            if (checkbox.Value) {
                drawList.FillRect(new MuiRect(box.X + 3f, box.Y + 3f, Math.Max(1f, box.Width - 6f), Math.Max(1f, box.Height - 6f)), theme.Accent);
            }

            drawList.Text(new MuiRect(box.Right + 5f, node.Rect.Y, Math.Max(0f, node.Rect.Right - box.Right - 5f), node.Rect.Height), checkbox.Text, null, TextColor(node));
        }

        private void PaintRadioButton(LayoutNode node, RadioButton radio, WidgetPaintState state, DrawList drawList) {
            float boxSize = Math.Min(theme.ToggleBoxSize, Math.Max(10f, node.Rect.Height - 4f));
            MuiRect box = new MuiRect(node.Rect.X, node.Rect.Y + Math.Max(0f, (node.Rect.Height - boxSize) * 0.5f), boxSize, boxSize);
            drawList.FillRect(box, FieldBg(node, state));
            drawList.StrokeRect(box, theme.Border, theme.BorderWidth);
            if (radio.Selected) {
                drawList.FillRect(new MuiRect(box.X + 4f, box.Y + 4f, Math.Max(1f, box.Width - 8f), Math.Max(1f, box.Height - 8f)), theme.Accent);
            }

            drawList.Text(new MuiRect(box.Right + 5f, node.Rect.Y, Math.Max(0f, node.Rect.Right - box.Right - 5f), node.Rect.Height), radio.Text, null, TextColor(node));
        }

        private void PaintSegmentedControl(LayoutNode node, ISegmentedControlWidget segmented, WidgetPaintState state, DrawList drawList) {
            System.Collections.IEnumerable items = segmented.SegmentItems;
            if (items == null) return;

            int count = 0;
            foreach (object ignored in items) count++;
            if (count <= 0) return;

            float segmentWidth = node.Rect.Width / count;
            int index = 0;
            foreach (object item in items) {
                MuiRect rect = new MuiRect(node.Rect.X + segmentWidth * index, node.Rect.Y, segmentWidth, node.Rect.Height);
                bool selected = object.Equals(item, segmented.SegmentValue);
                drawList.FillRect(rect, selected ? theme.SelectedBg : ControlBg(node, state));
                drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
                drawList.Text(rect, segmented.GetSegmentText(item), null, TextColor(node));
                index++;
            }
        }

        private void PaintSlider(LayoutNode node, Slider slider, WidgetPaintState state, DrawList drawList) {
            float trackHeight = Math.Min(node.Rect.Height, theme.SliderTrackHeight);
            MuiRect track = new MuiRect(node.Rect.X, node.Rect.Y + Math.Max(0f, (node.Rect.Height - trackHeight) * 0.5f), node.Rect.Width, trackHeight);
            drawList.FillRect(track, FieldBg(node, state));
            drawList.StrokeRect(track, theme.Border, theme.BorderWidth);

            float t = slider.Max <= slider.Min ? 0f : (slider.Value - slider.Min) / (slider.Max - slider.Min);
            t = Clamp01(t);
            float thumbX = node.Rect.X + Math.Max(0f, node.Rect.Width - theme.SliderThumbWidth) * t;
            MuiRect thumb = new MuiRect(thumbX, node.Rect.Y + Math.Max(0f, (node.Rect.Height - theme.SliderThumbHeight) * 0.5f), theme.SliderThumbWidth, theme.SliderThumbHeight);
            drawList.FillRect(thumb, theme.Accent);
            drawList.StrokeRect(thumb, theme.Border, theme.BorderWidth);
        }

        private void PaintNumericInput(LayoutNode node, string label, string valueText, WidgetPaintState state, DrawList drawList) {
            MuiRect valueRect;
            SplitNumericRect(node.Rect, label, out valueRect, drawList, node);
            float buttonWidth = Math.Min(24f, Math.Max(18f, valueRect.Height));
            MuiRect minus = new MuiRect(valueRect.Right - buttonWidth * 2f, valueRect.Y, buttonWidth, valueRect.Height);
            MuiRect plus = new MuiRect(valueRect.Right - buttonWidth, valueRect.Y, buttonWidth, valueRect.Height);
            MuiRect field = new MuiRect(valueRect.X, valueRect.Y, Math.Max(1f, valueRect.Width - buttonWidth * 2f), valueRect.Height);
            drawList.FillRect(field, FieldBg(node, state));
            drawList.StrokeRect(field, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(field.X + 4f, field.Y, Math.Max(0f, field.Width - 8f), field.Height), valueText, null, TextColor(node));
            PaintMiniButton(minus, "-", node, state, drawList);
            PaintMiniButton(plus, "+", node, state, drawList);
        }

        private void PaintDragNumeric(LayoutNode node, string label, string valueText, WidgetPaintState state, DrawList drawList) {
            MuiRect valueRect;
            SplitNumericRect(node.Rect, label, out valueRect, drawList, node);
            drawList.FillRect(valueRect, FieldBg(node, state));
            drawList.StrokeRect(valueRect, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(valueRect.X + 4f, valueRect.Y, Math.Max(0f, valueRect.Width - 8f), valueRect.Height), valueText, null, TextColor(node));
        }

        private void PaintSliderWithLabel(LayoutNode node, string label, float value, float min, float max, string valueText, WidgetPaintState state, DrawList drawList) {
            MuiRect valueRect;
            SplitNumericRect(node.Rect, label, out valueRect, drawList, node);
            float valueWidth = Math.Min(58f, Math.Max(42f, valueRect.Width * 0.3f));
            MuiRect textRect = new MuiRect(valueRect.Right - valueWidth, valueRect.Y, valueWidth, valueRect.Height);
            MuiRect sliderRect = new MuiRect(valueRect.X, valueRect.Y, Math.Max(1f, valueRect.Width - valueWidth - 6f), valueRect.Height);
            float trackHeight = Math.Min(sliderRect.Height, theme.SliderTrackHeight);
            MuiRect track = new MuiRect(sliderRect.X, sliderRect.Y + Math.Max(0f, (sliderRect.Height - trackHeight) * 0.5f), sliderRect.Width, trackHeight);
            drawList.FillRect(track, FieldBg(node, state));
            drawList.StrokeRect(track, theme.Border, theme.BorderWidth);
            float t = max <= min ? 0f : Clamp01((value - min) / (max - min));
            MuiRect thumb = new MuiRect(track.X + Math.Max(0f, track.Width - theme.SliderThumbWidth) * t, sliderRect.Y + Math.Max(0f, (sliderRect.Height - theme.SliderThumbHeight) * 0.5f), theme.SliderThumbWidth, theme.SliderThumbHeight);
            drawList.FillRect(thumb, theme.Accent);
            drawList.StrokeRect(thumb, theme.Border, theme.BorderWidth);
            drawList.Text(textRect, valueText, null, TextColor(node));
        }

        private void SplitNumericRect(MuiRect rect, string label, out MuiRect valueRect, DrawList drawList, LayoutNode node) {
            float labelWidth = string.IsNullOrEmpty(label) ? 0f : Math.Min(82f, rect.Width * 0.36f);
            if (labelWidth > 0f) {
                drawList.Text(new MuiRect(rect.X, rect.Y, labelWidth, rect.Height), label, null, TextColor(node));
                valueRect = new MuiRect(rect.X + labelWidth + 6f, rect.Y, Math.Max(1f, rect.Width - labelWidth - 6f), rect.Height);
            } else {
                valueRect = rect;
            }
        }

        private void PaintMiniButton(MuiRect rect, string text, LayoutNode node, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(rect, ControlBg(node, state));
            drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
            drawList.Text(rect, text, null, TextColor(node));
        }

        private void PaintProgressBar(LayoutNode node, ProgressBar progressBar, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.FieldBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            float t = progressBar.Indeterminate ? 0.35f : Clamp01(progressBar.Value);
            drawList.FillRect(new MuiRect(node.Rect.X, node.Rect.Y, node.Rect.Width * t, node.Rect.Height), theme.SelectedBg);
            string text = progressBar.Text;
            if (string.IsNullOrEmpty(text)) text = ((int)(Clamp01(progressBar.Value) * 100f)).ToString() + "%";
            drawList.Text(node.Rect, text, null, TextColor(node));
        }

        private void PaintStatusBadge(LayoutNode node, StatusBadge badge, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, StatusColor(badge.Kind));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(node.Rect.X + 5f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 10f), node.Rect.Height), badge.Text, null, TextColor(node));
        }

        private void PaintLoadingIndicator(LayoutNode node, LoadingIndicator indicator, WidgetPaintState state, DrawList drawList) {
            float size = Math.Min(10f, Math.Max(4f, node.Rect.Height - 6f));
            MuiRect mark = new MuiRect(node.Rect.X + 3f, node.Rect.Y + Math.Max(0f, (node.Rect.Height - size) * 0.5f), size, size);
            drawList.FillRect(mark, theme.Accent);
            string text = string.IsNullOrEmpty(indicator.Text) ? "Loading" : indicator.Text;
            drawList.Text(new MuiRect(mark.Right + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Right - mark.Right - 6f), node.Rect.Height), text, null, TextColor(node));
        }

        private void PaintImage(LayoutNode node, Image image, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.FieldBg);
            drawList.Image(node.Rect, image.Texture, image.Tint);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
        }

        private void PaintImageButton(LayoutNode node, ImageButton imageButton, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, ControlBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            float imageSize = Math.Min(node.Rect.Height - 4f, 20f);
            if (imageSize > 0f) {
                drawList.Image(new MuiRect(node.Rect.X + 3f, node.Rect.Y + Math.Max(0f, (node.Rect.Height - imageSize) * 0.5f), imageSize, imageSize), imageButton.Texture, imageButton.Tint);
            }
            if (!string.IsNullOrEmpty(imageButton.Text)) {
                drawList.Text(new MuiRect(node.Rect.X + imageSize + 8f, node.Rect.Y, Math.Max(0f, node.Rect.Width - imageSize - 10f), node.Rect.Height), imageButton.Text, null, TextColor(node));
            }
        }

        private void PaintTextField(LayoutNode node, TextField textField, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            string text = string.IsNullOrEmpty(textField.Value) ? textField.Placeholder : textField.Value;
            drawList.Text(new MuiRect(node.Rect.X + 4f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 8f), node.Rect.Height), text, null, TextColor(node));
        }

        private void PaintTextArea(LayoutNode node, TextArea textArea, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            string text = string.IsNullOrEmpty(textArea.Value) ? textArea.Placeholder : textArea.Value;
            drawList.Text(new MuiRect(node.Rect.X + 4f, node.Rect.Y + 3f, Math.Max(0f, node.Rect.Width - 8f), Math.Max(0f, node.Rect.Height - 6f)), text, null, TextColor(node));
        }

        private void PaintPasswordField(LayoutNode node, PasswordField passwordField, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            string text = string.IsNullOrEmpty(passwordField.Value) ? passwordField.Placeholder : Mask(passwordField.Value, passwordField.MaskChar);
            drawList.Text(new MuiRect(node.Rect.X + 4f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 8f), node.Rect.Height), text, null, TextColor(node));
        }

        private void PaintSearchBox(LayoutNode node, SearchBox searchBox, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            string text = string.IsNullOrEmpty(searchBox.Value) ? searchBox.Placeholder : searchBox.Value;
            drawList.Text(new MuiRect(node.Rect.X + 18f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 22f), node.Rect.Height), text, null, TextColor(node));
        }

        private void PaintComboBox(LayoutNode node, IComboBoxWidget combo, WidgetPaintState state, DrawList drawList) {
            MuiRect rect = node.Rect;
            string label = combo.ComboLabel ?? string.Empty;
            if (!string.IsNullOrEmpty(label)) {
                float labelWidth = combo.ComboLabelWidth > 0f ? combo.ComboLabelWidth : Math.Min(72f, rect.Width * 0.4f);
                drawList.Text(new MuiRect(rect.X, rect.Y, labelWidth, rect.Height), label, null, TextColor(node));
                rect = new MuiRect(rect.X + labelWidth + 6f, rect.Y, Math.Max(1f, rect.Width - labelWidth - 6f), rect.Height);
            }

            drawList.FillRect(rect, ControlBg(node, state));
            drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(rect.X + 6f, rect.Y, Math.Max(0f, rect.Width - 20f), rect.Height), combo.GetComboText(combo.ComboValue), null, TextColor(node));
            drawList.Text(new MuiRect(rect.Right - 16f, rect.Y, 14f, rect.Height), "v", null, TextColor(node));
        }

        private void PaintListBox(LayoutNode node, IListBoxWidget listBox, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);

            ISearchableListBoxWidget searchable = node.Widget as ISearchableListBoxWidget;
            float top = node.Rect.Y;
            float rowHeight = Math.Max(18f, theme.FontSize + 8f);
            string filter = string.Empty;
            if (searchable != null) {
                filter = searchable.ListFilterText ?? string.Empty;
                MuiRect filterRect = new MuiRect(node.Rect.X + 2f, node.Rect.Y + 2f, Math.Max(1f, node.Rect.Width - 4f), rowHeight);
                drawList.FillRect(filterRect, theme.FieldBg);
                drawList.StrokeRect(filterRect, theme.Border, theme.BorderWidth);
                string filterText = string.IsNullOrEmpty(filter) ? searchable.ListFilterPlaceholder : filter;
                drawList.Text(new MuiRect(filterRect.X + 5f, filterRect.Y, Math.Max(0f, filterRect.Width - 10f), filterRect.Height), filterText, null, string.IsNullOrEmpty(filter) ? theme.TextDisabled : TextColor(node));
                top = filterRect.Bottom + 3f;
            }

            int index = 0;
            System.Collections.IEnumerable items = listBox.ListItems;
            if (items == null) return;

            foreach (object item in items) {
                string text = listBox.GetListText(item);
                if (!MatchesFilter(text, filter)) continue;

                float y = top + index * rowHeight;
                if (y >= node.Rect.Bottom) break;

                MuiRect row = new MuiRect(node.Rect.X + 2f, y + 1f, Math.Max(0f, node.Rect.Width - 4f), Math.Max(1f, rowHeight - 2f));
                bool selected = object.Equals(item, listBox.ListValue);
                if (selected) drawList.FillRect(row, theme.SelectedBg);
                drawList.Text(new MuiRect(row.X + 4f, row.Y, Math.Max(0f, row.Width - 8f), row.Height), text, null, TextColor(node));
                index++;
            }
        }

        private void PaintDebugLines(LayoutNode node, IDebugLinesWidget debug, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);

            float rowHeight = Math.Max(18f, theme.FontSize + 8f);
            MuiRect title = new MuiRect(node.Rect.X + 2f, node.Rect.Y + 2f, Math.Max(0f, node.Rect.Width - 4f), rowHeight);
            drawList.FillRect(title, theme.SelectedBg);
            drawList.Text(new MuiRect(title.X + 4f, title.Y, Math.Max(0f, title.Width - 8f), title.Height), debug.DebugTitle, null, TextColor(node));

            IList<string> lines = debug.GetDebugLines();
            int maxRows = Math.Max(1, debug.VisibleRows);
            for (int i = 0; i < lines.Count && i < maxRows; i++) {
                float y = title.Bottom + i * rowHeight;
                if (y >= node.Rect.Bottom) break;

                drawList.Text(new MuiRect(node.Rect.X + 6f, y, Math.Max(0f, node.Rect.Width - 12f), rowHeight), lines[i], null, TextColor(node));
            }
        }

        private void PaintThemeEditor(LayoutNode node, ThemeEditor editor, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);

            ThemeTokens tokens = editor.Tokens ?? new ThemeTokens();
            float rowHeight = Math.Max(18f, theme.FontSize + 8f);
            MuiRect title = new MuiRect(node.Rect.X + 2f, node.Rect.Y + 2f, Math.Max(0f, node.Rect.Width - 4f), rowHeight);
            drawList.FillRect(title, theme.SelectedBg);
            drawList.Text(new MuiRect(title.X + 4f, title.Y, Math.Max(0f, title.Width - 8f), title.Height), "Theme Editor", null, TextColor(node));

            int row = 0;
            int maxRows = Math.Max(1, ThemeEditorTokens.VisibleRowCount(editor));
            if (editor.ShowColors) {
                for (int i = 0; i < ThemeEditorTokens.GetColors(editor).Length && row < maxRows; i++) {
                    ThemeColorToken token = ThemeEditorTokens.GetColors(editor)[i];
                    MuiRect rect = ThemeRowRect(node, title.Bottom, rowHeight, row);
                    PaintThemeColorRow(rect, ThemeEditorTokens.ColorName(token), tokens.GetColor(token), drawList, node);
                    row++;
                }
            }

            if (editor.ShowScalars) {
                for (int i = 0; i < ThemeEditorTokens.GetScalars(editor).Length && row < maxRows; i++) {
                    ThemeScalarToken token = ThemeEditorTokens.GetScalars(editor)[i];
                    MuiRect rect = ThemeRowRect(node, title.Bottom, rowHeight, row);
                    PaintThemeScalarRow(rect, ThemeEditorTokens.ScalarName(token), tokens.GetScalar(token), drawList, node);
                    row++;
                }
            }
        }

        private void PaintThemeColorRow(MuiRect rect, string name, MuiColor color, DrawList drawList, LayoutNode node) {
            drawList.Text(new MuiRect(rect.X + 6f, rect.Y, Math.Max(0f, rect.Width - 82f), rect.Height), name, null, TextColor(node));
            MuiRect swatch = new MuiRect(rect.Right - 70f, rect.Y + 3f, 28f, Math.Max(1f, rect.Height - 6f));
            drawList.FillRect(swatch, color);
            drawList.StrokeRect(swatch, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(swatch.Right + 5f, rect.Y, 32f, rect.Height), ColorSummary(color), null, TextColor(node));
        }

        private void PaintThemeScalarRow(MuiRect rect, string name, float value, DrawList drawList, LayoutNode node) {
            drawList.Text(new MuiRect(rect.X + 6f, rect.Y, Math.Max(0f, rect.Width - 96f), rect.Height), name, null, TextColor(node));
            drawList.Text(new MuiRect(rect.Right - 88f, rect.Y, 48f, rect.Height), value.ToString("0.##"), null, TextColor(node));
            drawList.FillRect(new MuiRect(rect.Right - 36f, rect.Y + 3f, 14f, Math.Max(1f, rect.Height - 6f)), theme.ButtonBg);
            drawList.FillRect(new MuiRect(rect.Right - 18f, rect.Y + 3f, 14f, Math.Max(1f, rect.Height - 6f)), theme.ButtonBg);
            drawList.Text(new MuiRect(rect.Right - 36f, rect.Y, 14f, rect.Height), "-", null, TextColor(node));
            drawList.Text(new MuiRect(rect.Right - 18f, rect.Y, 14f, rect.Height), "+", null, TextColor(node));
        }

        private static MuiRect ThemeRowRect(LayoutNode node, float startY, float rowHeight, int row) {
            return new MuiRect(node.Rect.X + 2f, startY + row * rowHeight, Math.Max(0f, node.Rect.Width - 4f), rowHeight);
        }

        private void PaintHelpBox(LayoutNode node, HelpBox helpBox, WidgetPaintState state, DrawList drawList) {
            MuiColor bg = theme.PanelBg;
            if (helpBox.Type == HelpBoxType.Warning) bg = theme.WarningBg;
            else if (helpBox.Type == HelpBoxType.Error) bg = theme.ErrorBg;

            drawList.FillRect(node.Rect, bg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            drawList.Text(new MuiRect(node.Rect.X + 6f, node.Rect.Y + 2f, Math.Max(0f, node.Rect.Width - 12f), Math.Max(0f, node.Rect.Height - 4f)), helpBox.Message, null, TextColor(node));
        }

        private void PaintSeparator(LayoutNode node, DrawList drawList) {
            drawList.FillRect(new MuiRect(node.Rect.X, node.Rect.Y + Math.Max(0f, (node.Rect.Height - 1f) * 0.5f), node.Rect.Width, 1f), theme.Separator);
        }

        private void PaintSeparatorText(LayoutNode node, SeparatorText separatorText, DrawList drawList) {
            float y = node.Rect.Y + Math.Max(0f, (node.Rect.Height - 1f) * 0.5f);
            float textWidth = Math.Min(node.Rect.Width, Math.Max(20f, separatorText.Text.Length * theme.FontSize * 0.55f + 12f));
            float lineWidth = Math.Max(0f, (node.Rect.Width - textWidth) * 0.5f);
            drawList.FillRect(new MuiRect(node.Rect.X, y, lineWidth, 1f), theme.Separator);
            drawList.FillRect(new MuiRect(node.Rect.Right - lineWidth, y, lineWidth, 1f), theme.Separator);
            drawList.Text(new MuiRect(node.Rect.X + lineWidth, node.Rect.Y, textWidth, node.Rect.Height), separatorText.Text, null, TextColor(node));
        }

        private void PaintFoldout(LayoutNode node, Foldout foldout, WidgetPaintState state, DrawList drawList) {
            PaintCollapsingHeader(node, foldout.Label, foldout.Open, foldout.Framed, false, state, drawList);
        }

        private void PaintCollapsingHeader(LayoutNode node, string label, bool open, bool framed, bool closable, WidgetPaintState state, DrawList drawList) {
            MuiRect header = new MuiRect(node.Rect.X, node.Rect.Y, node.Rect.Width, Math.Min(22f, node.Rect.Height));
            if (framed) {
                drawList.FillRect(header, ControlBg(node, state));
                drawList.StrokeRect(header, theme.Border, theme.BorderWidth);
            }
            drawList.Text(new MuiRect(header.X + 6f, header.Y, Math.Max(0f, header.Width - (closable ? 32f : 12f)), header.Height), (open ? "v " : "> ") + label, null, TextColor(node));
            if (closable) {
                MuiRect close = new MuiRect(header.Right - 20f, header.Y + 2f, 18f, Math.Max(1f, header.Height - 4f));
                drawList.FillRect(close, ControlBg(node, state));
                drawList.StrokeRect(close, theme.Border, theme.BorderWidth);
                drawList.Text(close, "x", null, TextColor(node));
            }
        }

        private void PaintClosableCollapsingHeader(LayoutNode node, ClosableCollapsingHeader header, WidgetPaintState state, DrawList drawList) {
            if (!header.Visible) return;
            PaintCollapsingHeader(node, header.Label, header.Open, header.Framed, true, state, drawList);
        }

        private void PaintSelectable(LayoutNode node, Selectable selectable, WidgetPaintState state, DrawList drawList) {
            if (selectable.Selected || state.IsHovered(node) || state.IsActive(node))
                drawList.FillRect(node.Rect, selectable.Selected ? theme.SelectedBg : ControlBg(node, state));
            if (selectable.Selected) drawList.FillRect(new MuiRect(node.Rect.X, node.Rect.Y, 3f, node.Rect.Height), theme.Accent);
            if (state.IsFocused(node)) drawList.StrokeRect(node.Rect, theme.FocusRing, theme.BorderWidth);
            drawList.Text(new MuiRect(node.Rect.X + 14f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 24f), node.Rect.Height), selectable.Text, null, TextColor(node));
        }

        private void PaintTreeNode(LayoutNode node, TreeNode treeNode, WidgetPaintState state, DrawList drawList) {
            MuiRect header = new MuiRect(node.Rect.X, node.Rect.Y, node.Rect.Width, Math.Min(22f, node.Rect.Height));
            if (treeNode.Framed || treeNode.Selected) {
                drawList.FillRect(header, treeNode.Selected ? theme.SelectedBg : ControlBg(node, state));
                drawList.StrokeRect(header, theme.Border, theme.BorderWidth);
            }

            string prefix = treeNode.Leaf ? (treeNode.Bullet ? "* " : "- ") : (treeNode.Open ? "v " : "> ");
            drawList.Text(new MuiRect(header.X + 6f, header.Y, Math.Max(0f, header.Width - 12f), header.Height), prefix + treeNode.Label, null, TextColor(node));
            if (treeNode.DrawLines && treeNode.Open && node.Children.Count > 0) {
                float x = header.X + 9f;
                float startY = header.Bottom;
                float endY = node.Children[node.Children.Count - 1].Rect.Y + Math.Min(22f, node.Children[node.Children.Count - 1].Rect.Height) * 0.5f;
                drawList.Line(new MuiVec2(x, startY), new MuiVec2(x, endY), theme.Separator, 1f);
            }
        }

        private void PaintTreeLeaf(LayoutNode node, TreeLeaf treeLeaf, WidgetPaintState state, DrawList drawList) {
            if (treeLeaf.Framed || treeLeaf.Selected) {
                drawList.FillRect(node.Rect, treeLeaf.Selected ? theme.SelectedBg : ControlBg(node, state));
                drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            }
            string prefix = treeLeaf.Bullet ? "* " : "- ";
            drawList.Text(new MuiRect(node.Rect.X + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 12f), node.Rect.Height), prefix + treeLeaf.Label, null, TextColor(node));
        }

        private void PaintColorButton(LayoutNode node, ColorButton colorButton, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, ControlBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);

            float swatchSize = Math.Min(node.Rect.Height - 4f, 22f);
            MuiRect swatch = new MuiRect(node.Rect.X + 3f, node.Rect.Y + Math.Max(0f, (node.Rect.Height - swatchSize) * 0.5f), swatchSize, swatchSize);
            PaintColorSwatch(swatch, colorButton.Value, colorButton.ShowCheckerboard, drawList);

            string text = colorButton.Label;
            if (string.IsNullOrEmpty(text)) text = FormatColor(colorButton.Value, colorButton.ShowAlpha, ColorDisplayMode.Hex);
            drawList.Text(new MuiRect(swatch.Right + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Right - swatch.Right - 9f), node.Rect.Height), text, null, TextColor(node));
        }

        private void PaintColorEdit(LayoutNode node, ColorEdit colorEdit, WidgetPaintState state, DrawList drawList) {
            PaintColorEditCore(node, string.Empty, colorEdit.Value, colorEdit.ShowAlpha, colorEdit.DisplayMode, true, state, drawList);
        }

        private void PaintColorEditCore(LayoutNode node, string label, MuiColor value, bool alpha, ColorDisplayMode displayMode, bool showPreview, WidgetPaintState state, DrawList drawList) {
            MuiRect rect = node.Rect;
            float labelWidth = string.IsNullOrEmpty(label) ? 0f : Math.Min(72f, rect.Width * 0.3f);
            if (labelWidth > 0f) {
                drawList.Text(new MuiRect(rect.X, rect.Y, labelWidth, rect.Height), label, null, TextColor(node));
                rect = new MuiRect(rect.X + labelWidth + 6f, rect.Y, Math.Max(1f, rect.Width - labelWidth - 6f), rect.Height);
            }

            if (showPreview) {
                float swatchWidth = Math.Min(32f, rect.Width * 0.22f);
                MuiRect swatch = new MuiRect(rect.X, rect.Y, swatchWidth, rect.Height);
                PaintColorSwatch(swatch, value, true, drawList);
                rect = new MuiRect(swatch.Right + 6f, rect.Y, Math.Max(1f, rect.Right - swatch.Right - 6f), rect.Height);
            }

            int components = alpha ? 4 : 3;
            float gap = 3f;
            float itemWidth = Math.Max(1f, (rect.Width - gap * (components - 1)) / components);
            for (int i = 0; i < components; i++) {
                MuiRect item = new MuiRect(rect.X + (itemWidth + gap) * i, rect.Y, itemWidth, rect.Height);
                float valueComponent = GetColorComponent(value, i);
                PaintColorComponent(item, ComponentName(i), valueComponent, ComponentColor(i, value), FormatColorComponent(valueComponent, displayMode), state, node, drawList);
            }
        }

        private void PaintColorPickerCore(LayoutNode node, string label, MuiColor value, bool alpha, ColorDisplayMode displayMode, bool showPreview, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.PanelBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);

            float rowHeight = Math.Max(18f, Math.Min(24f, node.Rect.Height / (alpha ? 5f : 4f)));
            MuiRect header = new MuiRect(node.Rect.X + 4f, node.Rect.Y + 4f, Math.Max(1f, node.Rect.Width - 8f), rowHeight);
            if (showPreview) {
                MuiRect swatch = new MuiRect(header.X, header.Y, Math.Min(44f, header.Height * 1.8f), header.Height);
                PaintColorSwatch(swatch, value, true, drawList);
                drawList.Text(new MuiRect(swatch.Right + 6f, header.Y, Math.Max(1f, header.Right - swatch.Right - 6f), header.Height), string.IsNullOrEmpty(label) ? FormatColor(value, alpha, displayMode) : label, null, TextColor(node));
            } else {
                drawList.Text(header, string.IsNullOrEmpty(label) ? FormatColor(value, alpha, displayMode) : label, null, TextColor(node));
            }

            int components = alpha ? 4 : 3;
            for (int i = 0; i < components; i++) {
                MuiRect row = new MuiRect(node.Rect.X + 4f, header.Bottom + 4f + i * (rowHeight + 3f), Math.Max(1f, node.Rect.Width - 8f), rowHeight);
                float valueComponent = GetColorComponent(value, i);
                PaintColorComponent(row, ComponentName(i), valueComponent, ComponentColor(i, value), FormatColorComponent(valueComponent, displayMode), state, node, drawList);
            }
        }

        private void PaintColorComponent(MuiRect rect, string label, float value, MuiColor fillColor, string valueText, WidgetPaintState state, LayoutNode node, DrawList drawList) {
            drawList.FillRect(rect, FieldBg(node, state));
            drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
            float fillWidth = Math.Max(0f, rect.Width * Clamp01(value));
            drawList.FillRect(new MuiRect(rect.X + 1f, rect.Y + 1f, Math.Max(0f, fillWidth - 2f), Math.Max(0f, rect.Height - 2f)), fillColor);
            drawList.Text(new MuiRect(rect.X + 5f, rect.Y, Math.Max(0f, rect.Width - 10f), rect.Height), label + " " + valueText, null, TextColor(node));
        }

        private void PaintColorSwatch(MuiRect rect, MuiColor color, bool checkerboard, DrawList drawList) {
            if (checkerboard && color.A < 0.999f) {
                float half = Math.Max(1f, rect.Height * 0.5f);
                drawList.FillRect(rect, theme.FieldBg);
                drawList.FillRect(new MuiRect(rect.X, rect.Y, rect.Width * 0.5f, half), theme.PanelBg);
                drawList.FillRect(new MuiRect(rect.X + rect.Width * 0.5f, rect.Y + half, rect.Width * 0.5f, Math.Max(1f, rect.Height - half)), theme.PanelBg);
            }

            drawList.FillRect(rect, color);
            drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
        }

        private void PaintVectorField(LayoutNode node, VectorField vectorField, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, FieldBg(node, state));
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            string text = vectorField.Label + "  " + vectorField.X.ToString("0.##") + ", " + vectorField.Y.ToString("0.##");
            if (vectorField.Components >= 3) text += ", " + vectorField.Z.ToString("0.##");
            drawList.Text(new MuiRect(node.Rect.X + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 12f), node.Rect.Height), text, null, TextColor(node));
        }

        private void PaintPlotLines(LayoutNode node, PlotLines plot, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.FieldBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            if (plot.Values == null || plot.Values.Count == 0) return;

            float min;
            float max;
            GetPlotRange(plot.Values, plot.HasRange, plot.Min, plot.Max, out min, out max);
            float step = plot.Values.Count <= 1 ? node.Rect.Width : node.Rect.Width / (plot.Values.Count - 1);
            for (int i = 0; i < plot.Values.Count; i++) {
                float t = Normalize(plot.Values[i], min, max);
                float x = node.Rect.X + step * i;
                float y = node.Rect.Bottom - t * node.Rect.Height;
                drawList.FillRect(new MuiRect(x, y, 2f, 2f), theme.Accent);
            }
            if (!string.IsNullOrEmpty(plot.OverlayText)) drawList.Text(node.Rect, plot.OverlayText, null, TextColor(node));
        }

        private void PaintPlotHistogram(LayoutNode node, PlotHistogram plot, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.FieldBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            if (plot.Values == null || plot.Values.Count == 0) return;

            float min;
            float max;
            GetPlotRange(plot.Values, plot.HasRange, plot.Min, plot.Max, out min, out max);
            float barWidth = Math.Max(1f, node.Rect.Width / plot.Values.Count);
            for (int i = 0; i < plot.Values.Count; i++) {
                float t = Normalize(plot.Values[i], min, max);
                float height = node.Rect.Height * t;
                drawList.FillRect(new MuiRect(node.Rect.X + i * barWidth, node.Rect.Bottom - height, Math.Max(1f, barWidth - 1f), height), theme.SelectedBg);
            }
            if (!string.IsNullOrEmpty(plot.OverlayText)) drawList.Text(node.Rect, plot.OverlayText, null, TextColor(node));
        }

        private void PaintDrawCanvas(LayoutNode node, DrawCanvas canvas, WidgetPaintState state, DrawList drawList) {
            if (canvas.DrawBackground) {
                drawList.FillRect(node.Rect, theme.FieldBg);
                drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            }

            if (canvas.Clip) drawList.PushClip(node.Rect);
            if (canvas.OnDraw != null) canvas.OnDraw(drawList, node.Rect);
            if (canvas.Clip) drawList.PopClip();
        }

        private void PaintDrawListWidget(LayoutNode node, DrawListWidget widget, WidgetPaintState state, DrawList drawList) {
            if (widget.DrawBackground) {
                drawList.FillRect(node.Rect, theme.FieldBg);
                drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            }

            if (widget.Clip) drawList.PushClip(node.Rect);
            if (widget.Source != null) {
                drawList.Append(widget.Source, widget.LocalCoordinates ? new MuiVec2(node.Rect.X, node.Rect.Y) : new MuiVec2(0f, 0f));
            }
            if (widget.Clip) drawList.PopClip();
        }

        private void PaintVirtualList(LayoutNode node, VirtualList virtualList, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.FieldBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);
            string text = "VirtualList: " + virtualList.ItemCount.ToString() + " items";
            drawList.Text(new MuiRect(node.Rect.X + 6f, node.Rect.Y, Math.Max(0f, node.Rect.Width - 12f), Math.Min(20f, node.Rect.Height)), text, null, theme.TextDisabled);
        }

        private void PaintTabs(LayoutNode node, Tabs tabs, WidgetPaintState state, DrawList drawList) {
            if (tabs.Children.Count == 0) return;

            List<Tab> visibleTabs = new List<Tab>();
            List<TabItemButton> leadingButtons = new List<TabItemButton>();
            List<TabItemButton> trailingButtons = new List<TabItemButton>();
            for (int i = 0; i < tabs.Children.Count; i++) {
                Tab tab = tabs.Children[i] as Tab;
                if (tab != null) {
                    if (tab.Visible) visibleTabs.Add(tab);
                    continue;
                }

                TabItemButton button = tabs.Children[i] as TabItemButton;
                if (button == null) continue;
                if (button.Placement == TabButtonPlacement.Leading) leadingButtons.Add(button);
                else trailingButtons.Add(button);
            }

            float headerHeight = Math.Min(22f, node.Rect.Height);
            float x = node.Rect.X;
            for (int i = 0; i < leadingButtons.Count; i++) {
                float width = Math.Max(20f, leadingButtons[i].ButtonWidth);
                MuiRect rect = new MuiRect(x, node.Rect.Y, width, headerHeight);
                drawList.FillRect(rect, ControlBg(node, state));
                drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
                drawList.Text(rect, leadingButtons[i].Text, null, TextColor(node));
                x += width + 2f;
            }

            float trailingWidth = 0f;
            for (int i = 0; i < trailingButtons.Count; i++) trailingWidth += Math.Max(20f, trailingButtons[i].ButtonWidth) + 2f;
            float tabAreaWidth = Math.Max(1f, node.Rect.Right - x - trailingWidth);
            float tabWidth = visibleTabs.Count <= 0 ? tabAreaWidth : tabAreaWidth / visibleTabs.Count;
            int selectedIndex = Clamp(tabs.SelectedIndex, 0, Math.Max(0, visibleTabs.Count - 1));
            for (int i = 0; i < visibleTabs.Count; i++) {
                Tab tab = visibleTabs[i];
                MuiRect rect = new MuiRect(x + tabWidth * i, node.Rect.Y, tabWidth, headerHeight);
                bool selected = i == selectedIndex;
                if (selected) {
                    drawList.FillRect(rect, theme.SelectedBg);
                    drawList.FillRect(new MuiRect(rect.X, rect.Bottom - 3f, rect.Width, 3f), theme.Accent);
                }
                string title = tab.Title ?? string.Empty;
                if (tab.Unsaved) title += "*";
                if (tab.Closable) title += "  x";
                drawList.Text(rect, title, null, TextColor(node));
            }

            float trailingX = node.Rect.Right - trailingWidth;
            for (int i = 0; i < trailingButtons.Count; i++) {
                float width = Math.Max(20f, trailingButtons[i].ButtonWidth);
                MuiRect rect = new MuiRect(trailingX, node.Rect.Y, width, headerHeight);
                drawList.FillRect(rect, ControlBg(node, state));
                drawList.StrokeRect(rect, theme.Border, theme.BorderWidth);
                drawList.Text(rect, trailingButtons[i].Text, null, TextColor(node));
                trailingX += width + 2f;
            }
        }

        private void PaintMenuBar(LayoutNode node, MenuBar menuBar, WidgetPaintState state, DrawList drawList) {
            drawList.FillRect(node.Rect, theme.FieldBg);
            drawList.StrokeRect(node.Rect, theme.Border, theme.BorderWidth);

            float x = node.Rect.X + 2f;
            for (int i = 0; i < menuBar.Children.Count; i++) {
                Menu menu = menuBar.Children[i] as Menu;
                if (menu == null) continue;

                float width = Math.Max(44f, menu.Title.Length * theme.FontSize * 0.55f + 18f);
                MuiRect rect = new MuiRect(x, node.Rect.Y + 2f, width, Math.Max(1f, node.Rect.Height - 4f));
                drawList.Text(rect, menu.Title, null, TextColor(node));
                x += width + 2f;
            }
        }

        private MuiColor ControlBg(LayoutNode node, WidgetPaintState state) {
            if (node.Widget != null && !node.Widget.Enabled) return theme.ButtonDisabledBg;
            if (state.IsActive(node)) return theme.ButtonActiveBg;
            if (state.IsHovered(node)) return theme.ButtonHoverBg;
            return theme.ButtonBg;
        }

        private MuiColor FieldBg(LayoutNode node, WidgetPaintState state) {
            if (node.Widget != null && !node.Widget.Enabled) return theme.ButtonDisabledBg;
            if (state.IsActive(node) || state.IsFocused(node)) return theme.FieldActiveBg;
            if (state.IsHovered(node)) return theme.FieldHoverBg;
            return theme.FieldBg;
        }

        private MuiColor TextColor(LayoutNode node) {
            return node != null && node.Widget != null && !node.Widget.Enabled ? theme.TextDisabled : theme.Text;
        }

        private static float Clamp01(float value) {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static int Clamp(int value, int min, int max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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
                string textBytes = ToByte(color.R).ToString() + ", " + ToByte(color.G).ToString() + ", " + ToByte(color.B).ToString();
                if (alpha) textBytes += ", " + ToByte(color.A).ToString();
                return textBytes;
            }

            string text = color.R.ToString("0.##") + ", " + color.G.ToString("0.##") + ", " + color.B.ToString("0.##");
            if (alpha) text += ", " + color.A.ToString("0.##");
            return text;
        }

        private static string FormatColorComponent(float value, ColorDisplayMode displayMode) {
            if (displayMode == ColorDisplayMode.Hex) return ToByte(value).ToString("X2");
            if (displayMode == ColorDisplayMode.Byte) return ToByte(value).ToString();
            return Clamp01(value).ToString("0.##");
        }

        private static int ToByte(float value) {
            return (int)Math.Round(Clamp01(value) * 255f);
        }

        private static float GetColorComponent(MuiColor color, int index) {
            if (index == 0) return color.R;
            if (index == 1) return color.G;
            if (index == 2) return color.B;
            return color.A;
        }

        private static string ComponentName(int index) {
            if (index == 0) return "R";
            if (index == 1) return "G";
            if (index == 2) return "B";
            return "A";
        }

        private static MuiColor ComponentColor(int index, MuiColor source) {
            if (index == 0) return new MuiColor(Clamp01(source.R), 0.16f, 0.16f, 1f);
            if (index == 1) return new MuiColor(0.16f, Clamp01(source.G), 0.16f, 1f);
            if (index == 2) return new MuiColor(0.16f, 0.22f, Clamp01(source.B), 1f);
            return new MuiColor(0.22f, 0.22f, 0.22f, Clamp01(source.A));
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

            if (defined > 0) return Math.Max(1, visibleDefined);

            int count = Math.Max(1, table.Columns);
            for (int i = 0; i < node.Children.Count; i++) {
                count = Math.Max(count, node.Children[i].Children.Count);
            }

            return count;
        }

        private static float[] GetTableColumnWidths(Table table, int columns, float totalWidth) {
            float[] widths = new float[Math.Max(1, columns)];
            float fixedWidth = 0f;
            int stretchCount = 0;
            int columnIndex = 0;

            for (int i = 0; i < table.Children.Count && columnIndex < widths.Length; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null || !column.Visible) continue;

                if (!column.Stretch || column.Width > 0f) {
                    widths[columnIndex] = ClampColumnWidth(column, Math.Max(1f, column.Width > 0f ? column.Width : 80f));
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

            float gaps = table.ColumnGap * Math.Max(0, widths.Length - 1);
            float stretchWidth = stretchCount <= 0 ? 0f : Math.Max(1f, (totalWidth - fixedWidth - gaps) / stretchCount);
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
            float value = Math.Max(1f, width);
            if (column != null && column.MinWidth > 0f) value = Math.Max(column.MinWidth, value);
            if (column != null && column.MaxWidth > 0f) value = Math.Min(column.MaxWidth, value);
            return Math.Max(1f, value);
        }

        private static string FormatColumnTitle(TableColumn column) {
            if (column == null) return string.Empty;

            string title = column.Title ?? string.Empty;
            if (column.SortDirection == TableSortDirection.Ascending) return title + " ^";
            if (column.SortDirection == TableSortDirection.Descending) return title + " v";
            return title;
        }

        private MuiColor StatusColor(StatusBadgeKind kind) {
            if (kind == StatusBadgeKind.Success) return theme.SuccessBg;
            if (kind == StatusBadgeKind.Warning) return theme.WarningBg;
            if (kind == StatusBadgeKind.Error) return theme.ErrorBg;
            return theme.InfoBg;
        }

        private static string ArrowText(ArrowDirection direction) {
            if (direction == ArrowDirection.Left) return "<";
            if (direction == ArrowDirection.Right) return ">";
            if (direction == ArrowDirection.Up) return "^";
            return "v";
        }

        private static string Mask(string value, char mask) {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            char ch = mask == '\0' ? '*' : mask;
            return new string(ch, value.Length);
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
            return Clamp01((value - min) / (max - min));
        }

        private static string ColorSummary(MuiColor color) {
            return ((int)(Clamp01(color.A) * 100f)).ToString() + "%";
        }
    }
}
