using System;

namespace MaximumUI {
    public sealed class LayoutEngine {
        private readonly LayoutMetrics metrics;

        public LayoutEngine()
            : this(new LayoutMetrics()) {
        }

        public LayoutEngine(LayoutMetrics metrics) {
            this.metrics = metrics ?? new LayoutMetrics();
        }

        public LayoutNode Layout(Widget widget, MuiRect viewport) {
            return Layout(widget, viewport, "0");
        }

        public LayoutNode Layout(Widget widget, MuiRect viewport, string rootPath) {
            LayoutNode node = Measure(widget, new LayoutConstraints(0f, viewport.Width, 0f, viewport.Height), rootPath);
            Arrange(node, viewport);
            return node;
        }

        public LayoutNode Measure(Widget widget, LayoutConstraints constraints, string path) {
            LayoutNode node = new LayoutNode {
                Widget = widget,
                Path = path ?? string.Empty
            };

            if (widget == null || widget is EmptyWidget) {
                node.DesiredSize = new MuiVec2(0f, 0f);
                return node;
            }

            if (widget is Window) MeasureWindow(node, (Window)widget, constraints);
            else if (widget is Dialog) MeasureDialog(node, (Dialog)widget, constraints);
            else if (widget is VStack) MeasureVStack(node, widget, ((VStack)widget).Gap, constraints);
            else if (widget is HStack) MeasureHStack(node, widget, ((HStack)widget).Gap, constraints);
            else if (widget is Panel) MeasurePanel(node, widget, constraints);
            else if (widget is TwoPaneLayout) MeasureTwoPaneLayout(node, (TwoPaneLayout)widget, constraints);
            else if (widget is DragSource) MeasurePanel(node, widget, constraints);
            else if (widget is DropTarget) MeasurePanel(node, widget, constraints);
            else if (widget is Grid) MeasureGrid(node, (Grid)widget, constraints);
            else if (widget is Table) MeasureTable(node, (Table)widget, constraints);
            else if (widget is TableRow) MeasureHStack(node, widget, 1f, constraints);
            else if (widget is TableCell) MeasurePanel(node, widget, constraints);
            else if (widget is ScrollView) MeasureScrollView(node, (ScrollView)widget, constraints);
            else if (widget is Foldout) MeasureFoldout(node, (Foldout)widget, constraints);
            else if (widget is CollapsingHeader) MeasureCollapsingHeader(node, (CollapsingHeader)widget, constraints);
            else if (widget is ClosableCollapsingHeader) MeasureClosableCollapsingHeader(node, (ClosableCollapsingHeader)widget, constraints);
            else if (widget is TreeNode) MeasureTreeNode(node, (TreeNode)widget, constraints);
            else if (widget is TreeLeaf) MeasureTreeLeaf(node, (TreeLeaf)widget, constraints);
            else if (widget is Tabs) MeasureTabs(node, (Tabs)widget, constraints);
            else if (widget is Tab) MeasureVStack(node, widget, 0f, constraints);
            else if (widget is MenuBar) MeasureMenuBar(node, (MenuBar)widget, constraints);
            else MeasureLeaf(node, widget, constraints);

            node.DesiredSize = ApplyHints(widget.Layout, node.DesiredSize, constraints);
            return node;
        }

        public void Arrange(LayoutNode node, MuiRect rect) {
            if (node == null) return;

            node.Rect = rect;
            Widget widget = node.Widget;
            if (widget == null || widget is EmptyWidget) return;

            if (widget is Window) ArrangeWindow(node, (Window)widget, rect);
            else if (widget is Dialog) ArrangeWindowLike(node, rect, metrics.WindowPaddingLeft, metrics.WindowPaddingTop, metrics.WindowPaddingRight, metrics.WindowPaddingBottom);
            else if (widget is VStack) ArrangeVStack(node, ((VStack)widget).Gap, rect);
            else if (widget is HStack) ArrangeHStack(node, ((HStack)widget).Gap, rect);
            else if (widget is Panel) ArrangePanel(node, rect);
            else if (widget is TwoPaneLayout) ArrangeTwoPaneLayout(node, (TwoPaneLayout)widget, rect);
            else if (widget is DragSource) ArrangePanel(node, rect);
            else if (widget is DropTarget) ArrangePanel(node, rect);
            else if (widget is Grid) ArrangeGrid(node, (Grid)widget, rect);
            else if (widget is Table) ArrangeTable(node, (Table)widget, rect);
            else if (widget is TableRow) ArrangeHStack(node, 1f, rect);
            else if (widget is TableCell) ArrangePanel(node, rect);
            else if (widget is ScrollView) ArrangeStackChildren(node, rect, 0f);
            else if (widget is Foldout) ArrangeFoldout(node, (Foldout)widget, rect);
            else if (widget is CollapsingHeader) ArrangeCollapsingHeader(node, ((CollapsingHeader)widget).Open, rect);
            else if (widget is ClosableCollapsingHeader) ArrangeCollapsingHeader(node, ((ClosableCollapsingHeader)widget).Open && ((ClosableCollapsingHeader)widget).Visible, rect);
            else if (widget is TreeNode) ArrangeTreeNode(node, (TreeNode)widget, rect);
            else if (widget is Tabs) ArrangeTabs(node, (Tabs)widget, rect);
            else if (widget is Tab) ArrangeStackChildren(node, rect, 0f);
            else if (widget is MenuBar) {
            }
        }

        private void MeasureWindow(LayoutNode node, Window window, LayoutConstraints constraints) {
            MeasureChildren(node, window, InnerConstraints(constraints), true);
            node.DesiredSize = new MuiVec2(window.Width, window.Height);
        }

        private void MeasureDialog(LayoutNode node, Dialog dialog, LayoutConstraints constraints) {
            MeasureChildren(node, dialog, InnerConstraints(constraints), true);
            node.DesiredSize = new MuiVec2(dialog.Width, dialog.Height);
        }

        private void MeasureVStack(LayoutNode node, Widget widget, float gap, LayoutConstraints constraints) {
            MeasureChildren(node, widget, constraints, true);
            float width = 0f;
            float height = 0f;
            for (int i = 0; i < node.Children.Count; i++) {
                if (i > 0) height += gap;
                width = Math.Max(width, node.Children[i].DesiredSize.X);
                height += node.Children[i].DesiredSize.Y;
            }

            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureHStack(LayoutNode node, Widget widget, float gap, LayoutConstraints constraints) {
            MeasureChildren(node, widget, constraints, true);
            float width = 0f;
            float height = 0f;
            for (int i = 0; i < node.Children.Count; i++) {
                if (i > 0) width += gap;
                width += node.Children[i].DesiredSize.X;
                height = Math.Max(height, node.Children[i].DesiredSize.Y);
            }

            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasurePanel(LayoutNode node, Widget widget, LayoutConstraints constraints) {
            LayoutConstraints childConstraints = ShrinkConstraints(constraints, metrics.PanelPadding * 2f, metrics.PanelPadding * 2f);
            MeasureChildren(node, widget, childConstraints, true);
            MuiVec2 content = MeasureStackSize(node, 0f);
            node.DesiredSize = new MuiVec2(content.X + metrics.PanelPadding * 2f, content.Y + metrics.PanelPadding * 2f);
        }

        private void MeasureTwoPaneLayout(LayoutNode node, TwoPaneLayout layout, LayoutConstraints constraints) {
            MeasureChildren(node, layout, constraints, true);
            float width = constraints.MaxWidth > 0f && !float.IsInfinity(constraints.MaxWidth) ? constraints.MaxWidth : 240f;
            float height = constraints.MaxHeight > 0f && !float.IsInfinity(constraints.MaxHeight) ? constraints.MaxHeight : 120f;

            if (node.Children.Count > 0) {
                width = Math.Max(width, node.Children[0].DesiredSize.X);
                height = Math.Max(height, node.Children[0].DesiredSize.Y);
            }
            if (node.Children.Count > 1) {
                if (layout.Orientation == TwoPaneOrientation.Horizontal) {
                    width = Math.Max(width, node.Children[0].DesiredSize.X + layout.SplitterSize + node.Children[1].DesiredSize.X);
                    height = Math.Max(height, Math.Max(node.Children[0].DesiredSize.Y, node.Children[1].DesiredSize.Y));
                } else {
                    width = Math.Max(width, Math.Max(node.Children[0].DesiredSize.X, node.Children[1].DesiredSize.X));
                    height = Math.Max(height, node.Children[0].DesiredSize.Y + layout.SplitterSize + node.Children[1].DesiredSize.Y);
                }
            }

            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureGrid(LayoutNode node, Grid grid, LayoutConstraints constraints) {
            MeasureChildren(node, grid, constraints, true);
            int columns = Math.Max(1, grid.Columns);
            float cellWidth = Math.Max(1f, (constraints.MaxWidth - grid.ColumnGap * (columns - 1)) / columns);
            float width = constraints.MaxWidth;
            float height = 0f;
            int rows = node.Children.Count == 0 ? 0 : (node.Children.Count + columns - 1) / columns;
            for (int row = 0; row < rows; row++) {
                float rowHeight = 0f;
                for (int column = 0; column < columns; column++) {
                    int index = row * columns + column;
                    if (index >= node.Children.Count) break;
                    rowHeight = Math.Max(rowHeight, node.Children[index].DesiredSize.Y);
                }
                if (row > 0) height += grid.RowGap;
                height += rowHeight;
            }
            if (width <= 0f || float.IsInfinity(width)) width = cellWidth * columns + grid.ColumnGap * (columns - 1);
            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureTable(LayoutNode node, Table table, LayoutConstraints constraints) {
            for (int i = 0; i < table.Children.Count; i++) {
                if (table.Children[i] is TableColumn) continue;
                node.Children.Add(Measure(table.Children[i], constraints, JoinPath(node.Path, i)));
            }

            float width = constraints.MaxWidth;
            float height = 0f;
            if (table.ShowHeader && HasVisibleTableColumns(table) && !HasExplicitHeaderRow(node)) height += GetTableHeaderHeight(table);
            for (int i = 0; i < node.Children.Count; i++) {
                if (height > 0f || i > 0) height += table.RowGap;
                height += Math.Max(metrics.RowHeight, node.Children[i].DesiredSize.Y);
            }
            if (width <= 0f || float.IsInfinity(width)) width = Math.Max(120f, GetTableColumnCount(table, node) * 80f);
            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureScrollView(LayoutNode node, ScrollView scrollView, LayoutConstraints constraints) {
            MeasureChildren(node, scrollView, constraints, true);
            MuiVec2 content = MeasureStackSize(node, 0f);
            float height = scrollView.Height > 0f ? scrollView.Height : content.Y;
            node.DesiredSize = new MuiVec2(content.X, height);
        }

        private void MeasureFoldout(LayoutNode node, Foldout foldout, LayoutConstraints constraints) {
            if (foldout.Open) {
                LayoutConstraints childConstraints = ShrinkConstraints(constraints, metrics.FoldoutIndent, metrics.RowHeight);
                MeasureChildren(node, foldout, childConstraints, true);
            }

            MuiVec2 content = MeasureStackSize(node, 0f);
            float width = Math.Max(MeasureText(foldout.Label) + 24f, content.X + metrics.FoldoutIndent);
            float height = metrics.RowHeight + (foldout.Open ? content.Y : 0f);
            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureCollapsingHeader(LayoutNode node, CollapsingHeader header, LayoutConstraints constraints) {
            if (header.Open) {
                LayoutConstraints childConstraints = ShrinkConstraints(constraints, metrics.FoldoutIndent, metrics.RowHeight);
                MeasureChildren(node, header, childConstraints, true);
            }

            MuiVec2 content = MeasureStackSize(node, 0f);
            float width = Math.Max(MeasureText(header.Label) + 28f, content.X + metrics.FoldoutIndent);
            float height = metrics.RowHeight + (header.Open ? content.Y : 0f);
            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureClosableCollapsingHeader(LayoutNode node, ClosableCollapsingHeader header, LayoutConstraints constraints) {
            if (!header.Visible) {
                node.DesiredSize = new MuiVec2(0f, 0f);
                return;
            }

            if (header.Open) {
                LayoutConstraints childConstraints = ShrinkConstraints(constraints, metrics.FoldoutIndent, metrics.RowHeight);
                MeasureChildren(node, header, childConstraints, true);
            }

            MuiVec2 content = MeasureStackSize(node, 0f);
            float width = Math.Max(MeasureText(header.Label) + 48f, content.X + metrics.FoldoutIndent);
            float height = metrics.RowHeight + (header.Open ? content.Y : 0f);
            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureTreeNode(LayoutNode node, TreeNode treeNode, LayoutConstraints constraints) {
            if (treeNode.Open && !treeNode.Leaf) {
                LayoutConstraints childConstraints = ShrinkConstraints(constraints, metrics.FoldoutIndent, metrics.RowHeight);
                MeasureChildren(node, treeNode, childConstraints, true);
            }

            MuiVec2 content = MeasureStackSize(node, 0f);
            float width = Math.Max(MeasureText(treeNode.Label) + 28f, content.X + metrics.FoldoutIndent);
            float height = metrics.RowHeight + ((treeNode.Open && !treeNode.Leaf) ? content.Y : 0f);
            node.DesiredSize = new MuiVec2(width, height);
        }

        private void MeasureTreeLeaf(LayoutNode node, TreeLeaf treeLeaf, LayoutConstraints constraints) {
            float width = MeasureText(treeLeaf.Label) + 28f;
            node.DesiredSize = new MuiVec2(width, metrics.RowHeight);
        }

        private void MeasureTabs(LayoutNode node, Tabs tabs, LayoutConstraints constraints) {
            int visibleCount = CountVisibleTabs(tabs);
            int selected = Clamp(tabs.SelectedIndex, 0, Math.Max(0, visibleCount - 1));
            int tabOrdinal = 0;
            for (int i = 0; i < tabs.Children.Count; i++) {
                Tab tab = tabs.Children[i] as Tab;
                if (tab == null || !tab.Visible) continue;
                if (tabOrdinal != selected) {
                    tabOrdinal++;
                    continue;
                }

                node.Children.Add(Measure(tabs.Children[i], constraints, JoinPath(node.Path, i)));
                break;
            }

            MuiVec2 content = MeasureStackSize(node, 0f);
            node.DesiredSize = new MuiVec2(Math.Max(content.X, 80f), metrics.RowHeight + content.Y);
        }

        private void MeasureMenuBar(LayoutNode node, MenuBar menuBar, LayoutConstraints constraints) {
            float width = constraints.MaxWidth > 0f && !float.IsInfinity(constraints.MaxWidth) ? constraints.MaxWidth : 160f;
            node.DesiredSize = new MuiVec2(width, metrics.RowHeight);
        }

        private void MeasureLeaf(LayoutNode node, Widget widget, LayoutConstraints constraints) {
            float width = 80f;
            float height = metrics.RowHeight;

            Label label = widget as Label;
            if (label != null) {
                width = MeasureText(label.Text);
            } else if (widget is BulletText) {
                BulletText bulletText = (BulletText)widget;
                width = MeasureText(bulletText.Text) + 22f;
            } else if (widget is ValueLabel) {
                ValueLabel valueLabel = (ValueLabel)widget;
                float labelWidth = valueLabel.LabelWidth > 0f ? valueLabel.LabelWidth : MeasureText(valueLabel.Label) + 8f;
                width = labelWidth + MeasureText(valueLabel.Value) + 8f;
            } else if (widget is TextLink) {
                TextLink link = (TextLink)widget;
                width = MeasureText(link.Text) + 8f;
            } else if (widget is Button) {
                Button button = (Button)widget;
                width = MeasureText(button.Text) + 24f;
            } else if (widget is SmallButton) {
                SmallButton button = (SmallButton)widget;
                width = MeasureText(button.Text) + 16f;
                height = metrics.SmallRowHeight;
            } else if (widget is ArrowButton) {
                width = metrics.RowHeight;
                height = metrics.RowHeight;
            } else if (widget is Toggle) {
                Toggle toggle = (Toggle)widget;
                width = MeasureText(toggle.Text) + 24f;
            } else if (widget is Checkbox) {
                Checkbox checkbox = (Checkbox)widget;
                width = MeasureText(checkbox.Text) + 24f;
            } else if (widget is RadioButton) {
                RadioButton radio = (RadioButton)widget;
                width = MeasureText(radio.Text) + 24f;
            } else if (widget is ISegmentedControlWidget) {
                ISegmentedControlWidget segmented = (ISegmentedControlWidget)widget;
                width = 0f;
                int count = 0;
                System.Collections.IEnumerable items = segmented.SegmentItems;
                if (items != null) {
                    foreach (object item in items) {
                        width += Math.Max(32f, MeasureText(segmented.GetSegmentText(item)) + 18f);
                        count++;
                    }
                }
                if (count > 1) width += count - 1;
                if (width <= 0f) width = 80f;
            } else if (widget is Slider) {
                width = 120f;
                height = metrics.SmallRowHeight;
            } else if (widget is InputInt || widget is InputFloat || widget is InputDouble) {
                width = 180f;
                height = metrics.RowHeight;
            } else if (widget is DragInt || widget is DragFloat) {
                width = 180f;
                height = metrics.RowHeight;
            } else if (widget is SliderInt || widget is SliderFloat || widget is SliderAngle) {
                width = 180f;
                height = metrics.RowHeight;
            } else if (widget is ProgressBar) {
                ProgressBar progressBar = (ProgressBar)widget;
                width = Math.Max(120f, MeasureText(progressBar.Text) + 24f);
                height = metrics.SmallRowHeight;
            } else if (widget is StatusBadge) {
                StatusBadge badge = (StatusBadge)widget;
                width = MeasureText(badge.Text) + 16f;
                height = metrics.SmallRowHeight;
            } else if (widget is LoadingIndicator) {
                LoadingIndicator indicator = (LoadingIndicator)widget;
                width = MeasureText(indicator.Text) + 24f;
                height = metrics.SmallRowHeight;
            } else if (widget is Image) {
                Image image = (Image)widget;
                height = 64f;
                width = Math.Max(1f, height * Math.Max(0.1f, image.AspectRatio));
            } else if (widget is ImageButton) {
                ImageButton imageButton = (ImageButton)widget;
                width = Math.Max(28f, MeasureText(imageButton.Text) + 34f);
                height = metrics.RowHeight;
            } else if (widget is TextField) {
                width = 120f;
            } else if (widget is TextArea) {
                width = 180f;
                height = metrics.RowHeight * 3f;
            } else if (widget is PasswordField) {
                width = 120f;
            } else if (widget is SearchBox) {
                width = 160f;
            } else if (widget is IListBoxWidget) {
                IListBoxWidget listBox = (IListBoxWidget)widget;
                width = 160f;
                height = metrics.RowHeight * Math.Max(1, listBox.ListVisibleItems);
                if (widget is ISearchableListBoxWidget) height += metrics.RowHeight + 4f;
            } else if (widget is IDebugLinesWidget) {
                IDebugLinesWidget debug = (IDebugLinesWidget)widget;
                int rows = Math.Max(1, debug.VisibleRows);
                width = 240f;
                height = metrics.RowHeight * (rows + 1) + 6f;
            } else if (widget is ThemeEditor) {
                ThemeEditor editor = (ThemeEditor)widget;
                width = 280f;
                height = metrics.RowHeight * (Math.Max(1, ThemeEditorTokens.VisibleRowCount(editor)) + 1) + 6f;
            } else if (widget is IComboBoxWidget) {
                IComboBoxWidget combo = (IComboBoxWidget)widget;
                width = MeasureText(combo.GetComboText(combo.ComboValue)) + 32f;
                if (!string.IsNullOrEmpty(combo.ComboLabel)) {
                    width += (combo.ComboLabelWidth > 0f ? combo.ComboLabelWidth : 72f) + 6f;
                }
            } else if (widget is HelpBox) {
                HelpBox helpBox = (HelpBox)widget;
                width = MeasureText(helpBox.Message) + 16f;
                height = metrics.RowHeight + 10f;
            } else if (widget is Selectable) {
                Selectable selectable = (Selectable)widget;
                width = MeasureText(selectable.Text) + 16f;
            } else if (widget is ColorButton) {
                ColorButton colorButton = (ColorButton)widget;
                width = Math.Max(72f, MeasureText(colorButton.Label) + 48f);
            } else if (widget is ColorEdit || widget is ColorEdit3 || widget is ColorEdit4) {
                width = 160f;
            } else if (widget is ColorPicker3) {
                width = 220f;
                height = metrics.RowHeight * 4f + 10f;
            } else if (widget is ColorPicker4) {
                width = 220f;
                height = metrics.RowHeight * 5f + 12f;
            } else if (widget is VectorField) {
                VectorField vector = (VectorField)widget;
                width = Math.Max(180f, MeasureText(vector.Label) + 120f);
            } else if (widget is PlotLines || widget is PlotHistogram) {
                width = 180f;
                height = 64f;
            } else if (widget is DrawCanvas) {
                DrawCanvas canvas = (DrawCanvas)widget;
                width = Math.Max(1f, canvas.CanvasWidth);
                height = Math.Max(1f, canvas.CanvasHeight);
            } else if (widget is DrawListWidget) {
                DrawListWidget drawListWidget = (DrawListWidget)widget;
                width = Math.Max(1f, drawListWidget.CanvasWidth);
                height = Math.Max(1f, drawListWidget.CanvasHeight);
            } else if (widget is VirtualList) {
                VirtualList virtualList = (VirtualList)widget;
                width = 180f;
                height = Math.Max(virtualList.RowHeight, virtualList.Height);
            } else if (widget is Separator) {
                height = 9f;
            } else if (widget is SeparatorText) {
                SeparatorText separatorText = (SeparatorText)widget;
                width = MeasureText(separatorText.Text) + 48f;
                height = metrics.RowHeight;
            } else if (widget is Space) {
                Space space = (Space)widget;
                width = 0f;
                height = Math.Max(0f, space.Pixels);
            } else if (widget is Spacer) {
                width = 0f;
                height = 0f;
            }

            node.DesiredSize = new MuiVec2(width, height);
        }

        private void ArrangeWindow(LayoutNode node, Window window, MuiRect rect) {
            float top = window.Foldable ? metrics.WindowPaddingTop : metrics.TitleHeight;
            ArrangeWindowLike(node, rect, metrics.WindowPaddingLeft, top, metrics.WindowPaddingRight, metrics.WindowPaddingBottom);
        }

        private void ArrangeWindowLike(LayoutNode node, MuiRect rect, float left, float top, float right, float bottom) {
            MuiRect content = new MuiRect(
                rect.X + left,
                rect.Y + top,
                Math.Max(0f, rect.Width - left - right),
                Math.Max(0f, rect.Height - top - bottom));
            ArrangeStackChildren(node, content, 0f);
        }

        private void ArrangePanel(LayoutNode node, MuiRect rect) {
            MuiRect content = new MuiRect(
                rect.X + metrics.PanelPadding,
                rect.Y + metrics.PanelPadding,
                Math.Max(0f, rect.Width - metrics.PanelPadding * 2f),
                Math.Max(0f, rect.Height - metrics.PanelPadding * 2f));
            ArrangeStackChildren(node, content, 0f);
        }

        private void ArrangeTwoPaneLayout(LayoutNode node, TwoPaneLayout layout, MuiRect rect) {
            if (node.Children.Count == 0) return;

            float splitter = Math.Max(1f, layout.SplitterSize);
            float ratio = Clamp01(layout.Ratio);
            if (layout.Orientation == TwoPaneOrientation.Horizontal) {
                float available = Math.Max(0f, rect.Width - splitter);
                float firstWidth = available * ratio;
                firstWidth = Math.Max(layout.MinFirst, Math.Min(firstWidth, Math.Max(layout.MinFirst, available - layout.MinSecond)));
                Arrange(node.Children[0], new MuiRect(rect.X, rect.Y, firstWidth, rect.Height));
                if (node.Children.Count > 1) {
                    Arrange(node.Children[1], new MuiRect(rect.X + firstWidth + splitter, rect.Y, Math.Max(0f, rect.Width - firstWidth - splitter), rect.Height));
                }
            } else {
                float available = Math.Max(0f, rect.Height - splitter);
                float firstHeight = available * ratio;
                firstHeight = Math.Max(layout.MinFirst, Math.Min(firstHeight, Math.Max(layout.MinFirst, available - layout.MinSecond)));
                Arrange(node.Children[0], new MuiRect(rect.X, rect.Y, rect.Width, firstHeight));
                if (node.Children.Count > 1) {
                    Arrange(node.Children[1], new MuiRect(rect.X, rect.Y + firstHeight + splitter, rect.Width, Math.Max(0f, rect.Height - firstHeight - splitter)));
                }
            }
        }

        private void ArrangeGrid(LayoutNode node, Grid grid, MuiRect rect) {
            int columns = Math.Max(1, grid.Columns);
            float cellWidth = Math.Max(1f, (rect.Width - grid.ColumnGap * (columns - 1)) / columns);
            float y = rect.Y;
            int rows = node.Children.Count == 0 ? 0 : (node.Children.Count + columns - 1) / columns;
            for (int row = 0; row < rows; row++) {
                float rowHeight = 0f;
                for (int column = 0; column < columns; column++) {
                    int index = row * columns + column;
                    if (index >= node.Children.Count) break;
                    rowHeight = Math.Max(rowHeight, node.Children[index].DesiredSize.Y);
                }
                if (row > 0) y += grid.RowGap;
                for (int column = 0; column < columns; column++) {
                    int index = row * columns + column;
                    if (index >= node.Children.Count) break;
                    LayoutNode child = node.Children[index];
                    float x = rect.X + column * (cellWidth + grid.ColumnGap);
                    Arrange(child, new MuiRect(x, y, cellWidth, rowHeight));
                }
                y += rowHeight;
            }
        }

        private void ArrangeTable(LayoutNode node, Table table, MuiRect rect) {
            float y = rect.Y;
            int columns = GetTableColumnCount(table, node);
            float[] widths = GetTableColumnWidths(table, columns, rect.Width);
            if (table.ShowHeader && HasVisibleTableColumns(table) && !HasExplicitHeaderRow(node)) {
                y += GetTableHeaderHeight(table) + table.RowGap;
            }

            for (int i = 0; i < node.Children.Count; i++) {
                if (i > 0) y += table.RowGap;
                LayoutNode row = node.Children[i];
                float rowHeight = Math.Max(metrics.RowHeight, row.DesiredSize.Y);
                ArrangeTableRow(row, table, new MuiRect(rect.X, y, rect.Width, rowHeight), widths);
                y += rowHeight;
            }
        }

        private void ArrangeTableRow(LayoutNode row, Table table, MuiRect rect, float[] widths) {
            row.Rect = rect;
            float x = rect.X;
            int visibleColumn = 0;
            for (int column = 0; column < row.Children.Count; column++) {
                if (!IsTableColumnVisible(table, column)) {
                    Arrange(row.Children[column], new MuiRect(x, rect.Y, 0f, rect.Height));
                    continue;
                }

                float width = visibleColumn < widths.Length ? widths[visibleColumn] : widths[widths.Length - 1];
                Arrange(row.Children[column], new MuiRect(x, rect.Y, width, rect.Height));
                x += width + table.ColumnGap;
                visibleColumn++;
            }
        }

        private int GetTableColumnCount(Table table, LayoutNode node) {
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

            return Math.Max(1, count);
        }

        private float[] GetTableColumnWidths(Table table, int columns, float totalWidth) {
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
            columnIndex = 0;
            for (int i = 0; i < widths.Length; i++) {
                TableColumn column = GetVisibleTableColumn(table, i);
                if (widths[i] <= 0f) widths[i] = ClampColumnWidth(column, stretchWidth);
            }

            return widths;
        }

        private static bool HasVisibleTableColumns(Table table) {
            for (int i = 0; i < table.Children.Count; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column != null && column.Visible) return true;
            }

            return false;
        }

        private static bool IsTableColumnVisible(Table table, int index) {
            int columnIndex = 0;
            for (int i = 0; i < table.Children.Count; i++) {
                TableColumn column = table.Children[i] as TableColumn;
                if (column == null) continue;
                if (columnIndex == index) return column.Visible;
                columnIndex++;
            }

            return true;
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

        private float GetTableHeaderHeight(Table table) {
            return table.HeaderHeight > 0f ? table.HeaderHeight : metrics.RowHeight;
        }

        private static bool HasExplicitHeaderRow(LayoutNode node) {
            for (int i = 0; i < node.Children.Count; i++) {
                if (node.Children[i].Widget is TableHeaderRow) return true;
            }

            return false;
        }

        private void ArrangeVStack(LayoutNode node, float gap, MuiRect rect) {
            ArrangeStackChildren(node, rect, gap);
        }

        private void ArrangeHStack(LayoutNode node, float gap, MuiRect rect) {
            int expandCount = CountHorizontalExpanders(node);
            float fixedWidth = 0f;
            for (int i = 0; i < node.Children.Count; i++) {
                if (i > 0) fixedWidth += gap;
                if (!ExpandsWidth(node.Children[i])) fixedWidth += node.Children[i].DesiredSize.X;
            }

            float extraWidth = Math.Max(0f, rect.Width - fixedWidth);
            float expandWidth = expandCount > 0 ? extraWidth / expandCount : 0f;
            float x = rect.X;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode child = node.Children[i];
                if (i > 0) x += gap;

                float width = ExpandsWidth(child) ? expandWidth : child.DesiredSize.X;
                float height = child.Widget != null && child.Widget.Layout.ExpandHeight ? rect.Height : child.DesiredSize.Y;
                Arrange(child, new MuiRect(x, rect.Y, width, Math.Min(rect.Height, height)));
                x += width;
            }
        }

        private void ArrangeStackChildren(LayoutNode node, MuiRect rect, float gap) {
            int expandCount = CountVerticalExpanders(node);
            float fixedHeight = 0f;
            for (int i = 0; i < node.Children.Count; i++) {
                if (i > 0) fixedHeight += gap;
                if (!ExpandsHeight(node.Children[i])) fixedHeight += node.Children[i].DesiredSize.Y;
            }

            float extraHeight = Math.Max(0f, rect.Height - fixedHeight);
            float expandHeight = expandCount > 0 ? extraHeight / expandCount : 0f;
            float y = rect.Y;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode child = node.Children[i];
                if (i > 0) y += gap;

                float width = child.Widget != null && child.Widget.Layout.HasWidth ? child.DesiredSize.X : rect.Width;
                if (child.Widget != null && child.Widget.Layout.ExpandWidth) width = rect.Width;
                float height = ExpandsHeight(child) ? expandHeight : child.DesiredSize.Y;
                Arrange(child, new MuiRect(rect.X, y, Math.Min(rect.Width, width), height));
                y += height;
            }
        }

        private void ArrangeFoldout(LayoutNode node, Foldout foldout, MuiRect rect) {
            if (!foldout.Open) return;
            MuiRect content = new MuiRect(
                rect.X + metrics.FoldoutIndent,
                rect.Y + metrics.RowHeight,
                Math.Max(0f, rect.Width - metrics.FoldoutIndent),
                Math.Max(0f, rect.Height - metrics.RowHeight));
            ArrangeStackChildren(node, content, 0f);
        }

        private void ArrangeCollapsingHeader(LayoutNode node, bool open, MuiRect rect) {
            if (!open) return;
            MuiRect content = new MuiRect(
                rect.X + metrics.FoldoutIndent,
                rect.Y + metrics.RowHeight,
                Math.Max(0f, rect.Width - metrics.FoldoutIndent),
                Math.Max(0f, rect.Height - metrics.RowHeight));
            ArrangeStackChildren(node, content, 0f);
        }

        private void ArrangeTreeNode(LayoutNode node, TreeNode treeNode, MuiRect rect) {
            if (!treeNode.Open || treeNode.Leaf) return;
            MuiRect content = new MuiRect(
                rect.X + metrics.FoldoutIndent,
                rect.Y + metrics.RowHeight,
                Math.Max(0f, rect.Width - metrics.FoldoutIndent),
                Math.Max(0f, rect.Height - metrics.RowHeight));
            ArrangeStackChildren(node, content, 0f);
        }

        private void ArrangeTabs(LayoutNode node, Tabs tabs, MuiRect rect) {
            MuiRect content = new MuiRect(rect.X, rect.Y + metrics.RowHeight, rect.Width, Math.Max(0f, rect.Height - metrics.RowHeight));
            ArrangeStackChildren(node, content, 0f);
        }

        private void MeasureChildren(LayoutNode node, Widget widget, LayoutConstraints constraints, bool includeAll) {
            for (int i = 0; i < widget.Children.Count; i++) {
                Widget child = widget.Children[i];
                if (child == null || child is EmptyWidget) continue;
                node.Children.Add(Measure(child, constraints, JoinPath(node.Path, i)));
            }
        }

        private MuiVec2 MeasureStackSize(LayoutNode node, float gap) {
            float width = 0f;
            float height = 0f;
            for (int i = 0; i < node.Children.Count; i++) {
                if (i > 0) height += gap;
                width = Math.Max(width, node.Children[i].DesiredSize.X);
                height += node.Children[i].DesiredSize.Y;
            }

            return new MuiVec2(width, height);
        }

        private LayoutConstraints InnerConstraints(LayoutConstraints constraints) {
            return ShrinkConstraints(
                constraints,
                metrics.WindowPaddingLeft + metrics.WindowPaddingRight,
                metrics.WindowPaddingTop + metrics.WindowPaddingBottom);
        }

        private static LayoutConstraints ShrinkConstraints(LayoutConstraints constraints, float width, float height) {
            return new LayoutConstraints(
                0f,
                Math.Max(0f, constraints.MaxWidth - width),
                0f,
                Math.Max(0f, constraints.MaxHeight - height));
        }

        private MuiVec2 ApplyHints(LayoutHints hints, MuiVec2 size, LayoutConstraints constraints) {
            if (hints == null) return size;

            float width = size.X;
            float height = size.Y;
            if (hints.HasWidth) width = hints.Width;
            if (hints.HasHeight) height = hints.Height;
            if (hints.HasMinWidth) width = Math.Max(width, hints.MinWidth);
            if (hints.HasMaxWidth) width = Math.Min(width, hints.MaxWidth);
            if (hints.HasMinHeight) height = Math.Max(height, hints.MinHeight);
            if (hints.HasMaxHeight) height = Math.Min(height, hints.MaxHeight);

            width = Clamp(width, constraints.MinWidth, constraints.MaxWidth);
            height = Clamp(height, constraints.MinHeight, constraints.MaxHeight);
            return new MuiVec2(width, height);
        }

        private float MeasureText(string text) {
            if (string.IsNullOrEmpty(text)) return 12f;
            return Math.Max(12f, text.Length * metrics.TextWidth);
        }

        private static bool ExpandsWidth(LayoutNode node) {
            return node != null && node.Widget != null && (node.Widget is Spacer || node.Widget.Layout.ExpandWidth);
        }

        private static bool ExpandsHeight(LayoutNode node) {
            return node != null && node.Widget != null && (node.Widget is Spacer || node.Widget.Layout.ExpandHeight);
        }

        private static int CountHorizontalExpanders(LayoutNode node) {
            int count = 0;
            for (int i = 0; i < node.Children.Count; i++) {
                if (ExpandsWidth(node.Children[i])) count++;
            }

            return count;
        }

        private static int CountVerticalExpanders(LayoutNode node) {
            int count = 0;
            for (int i = 0; i < node.Children.Count; i++) {
                if (ExpandsHeight(node.Children[i])) count++;
            }

            return count;
        }

        private static string JoinPath(string path, int index) {
            return string.IsNullOrEmpty(path) ? index.ToString() : path + "." + index;
        }

        private static int CountVisibleTabs(Tabs tabs) {
            if (tabs == null) return 0;

            int count = 0;
            for (int i = 0; i < tabs.Children.Count; i++) {
                Tab tab = tabs.Children[i] as Tab;
                if (tab != null && tab.Visible) count++;
            }

            return count;
        }

        private static int Clamp(int value, int min, int max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Clamp(float value, float min, float max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Clamp01(float value) {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
