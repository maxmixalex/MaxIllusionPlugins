using System;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class DialogLayoutTests {
        internal const int TestCount = 4;

        public static void RunAll() {
            DialogPaddingControlsBothMeasurementAndArrangement();
            ShortServiceDialogsKeepTheirActionsInsideAtEachFontSize();
            LongServiceMessagesScrollWhileActionsStayVisible();
            ServiceUsesTheProductionLabelMeasurement();
        }

        private static void DialogPaddingControlsBothMeasurementAndArrangement() {
            LayoutMetrics metrics = new LayoutMetrics {
                WindowPaddingLeft = 3f, WindowPaddingTop = 4f, WindowPaddingRight = 5f, WindowPaddingBottom = 6f,
                DialogPaddingLeft = 20f, DialogPaddingTop = 48f, DialogPaddingRight = 24f, DialogPaddingBottom = 18f
            };
            float measuredWidth = 0f;
            metrics.MeasureLabelHeight = delegate(Label label, float width) { measuredWidth = width; return 30f; };
            Dialog dialog = new Dialog("Distinct padding");
            dialog.Add(new Label("Wrapped") { WordWrap = true });
            LayoutNode layout = new LayoutEngine(metrics).Layout(dialog, new MuiRect(10f, 20f, 300f, 200f));
            Equal(256f, measuredWidth, "dialog measure must subtract dialog left/right padding");
            Equal(30f, layout.Children[0].Rect.X, "dialog arrange uses dialog left padding");
            Equal(68f, layout.Children[0].Rect.Y, "dialog arrange uses dialog top padding");
            Equal(256f, layout.Children[0].Rect.Width, "dialog measure and arrange widths agree");

            LayoutMetrics legacy = new LayoutMetrics { WindowPaddingLeft = 7f, WindowPaddingTop = 31f, WindowPaddingRight = 9f };
            layout = new LayoutEngine(legacy).Layout(dialog, new MuiRect(0f, 0f, 300f, 200f));
            Equal(7f, layout.Children[0].Rect.X, "unspecified dialog padding preserves legacy window fallback");
            Equal(31f, layout.Children[0].Rect.Y, "legacy top padding remains compatible");
            Equal(284f, layout.Children[0].Rect.Width, "legacy horizontal fallback remains compatible");
        }

        private static void ShortServiceDialogsKeepTheirActionsInsideAtEachFontSize() {
            int[] sizes = { 13, 18, 32 };
            foreach (int size in sizes) {
                LayoutMetrics metrics = Metrics(size);
                DialogService service = Service(metrics, 1280f, 720f);
                service.Alert("Alert", "Short message");
                service.Confirm("Confirm", "Short message", delegate { });
                for (int i = 0; i < service.Dialogs.Count; i++) {
                    Dialog dialog = service.Dialogs[i];
                    LayoutNode root = new LayoutEngine(metrics).Layout(dialog, new MuiRect(0f, 0f, dialog.Width, dialog.Height));
                    LayoutNode content = root.Children[0];
                    LayoutNode buttons = content.Children[content.Children.Count - 1];
                    Assert(!(content.Children[0].Widget is ScrollView), "a short message does not need scrolling");
                    Equal(metrics.DialogPaddingTop, content.Rect.Y, "service dialog reserves the actual title padding");
                    Assert(buttons.Rect.Bottom <= dialog.Height - metrics.DialogPaddingBottom + 0.01f,
                        "actual service action row fits inside the themed dialog at font " + size);
                    Assert(buttons.Children.Count == i + 2, "Alert/Confirm preserve their spacer and action buttons");
                    for (int j = 1; j < buttons.Children.Count; j++) {
                        Assert(buttons.Children[j].Rect.Height >= metrics.RowHeight, "action controls retain their full row height");
                        Assert(buttons.Children[j].Rect.Right <= dialog.Width - metrics.DialogPaddingRight + 0.01f,
                            "action controls fit the dialog content width");
                    }
                }
            }
        }

        private static void LongServiceMessagesScrollWhileActionsStayVisible() {
            LayoutMetrics metrics = Metrics(18);
            DialogService service = Service(metrics, 640f, 360f);
            service.Confirm("Long message", new string('M', 2400), delegate { });
            Dialog dialog = service.Dialogs[0];
            Assert(dialog.Width <= 600f && dialog.Height <= 320f, "service respects the available screen area");
            LayoutNode root = new LayoutEngine(metrics).Layout(dialog, new MuiRect(0f, 0f, dialog.Width, dialog.Height));
            LayoutNode content = root.Children[0];
            LayoutNode scroll = content.Children[0];
            Assert(scroll.Widget is ScrollView, "an oversized message gets a real production ScrollView");
            Assert(scroll.Children.Count == 1 && scroll.Children[0].Rect.Height > scroll.Rect.Height,
                "scrolling preserves the complete message extent instead of clipping the label height");
            MuiRect extent = ScrollGeometry.ContentExtent(scroll, scroll.Rect);
            Assert(extent.Height >= scroll.Children[0].Rect.Height, "the final message lines remain reachable");
            LayoutNode buttons = content.Children[content.Children.Count - 1];
            Assert(buttons.Rect.Y >= scroll.Rect.Bottom, "buttons stay outside the message scroller");
            Assert(buttons.Rect.Bottom <= dialog.Height - metrics.DialogPaddingBottom + 0.01f,
                "scrolling does not push actions beyond the dialog edge");
            object firstScrollKey = scroll.Widget.Key;
            service.Confirm("Next message", new string('N', 2400), delegate { });
            Dialog next = service.Dialogs[1];
            LayoutNode nextRoot = new LayoutEngine(metrics).Layout(next, new MuiRect(0f, 0f, next.Width, next.Height));
            Assert(!object.Equals(firstScrollKey, nextRoot.Children[0].Children[0].Widget.Key),
                "simultaneously open service messages keep separate retained scroll positions");
        }

        private static void ServiceUsesTheProductionLabelMeasurement() {
            LayoutMetrics metrics = Metrics(13);
            metrics.MeasureLabelHeight = delegate(Label label, float width) { return 73f; };
            DialogService service = Service(metrics, 1280f, 720f);
            service.Alert("Measured", "The native font can be taller than an estimate.");
            Dialog dialog = service.Dialogs[0];
            LayoutNode root = new LayoutEngine(metrics).Layout(dialog, new MuiRect(0f, 0f, dialog.Width, dialog.Height));
            Equal(73f, root.Children[0].Children[0].Rect.Height, "service uses LayoutEngine's font measurement result");
            Equal(metrics.DialogPaddingTop + metrics.DialogPaddingBottom + 73f + 16f + 9f + metrics.RowHeight,
                dialog.Height, "dialog height includes actual message, spacing, separator, actions and both paddings");
        }

        private static DialogService Service(LayoutMetrics metrics, float width, float height) {
            return new DialogService(delegate { return metrics; }, delegate { return new MuiVec2(width, height); });
        }

        private static LayoutMetrics Metrics(int size) {
            LayoutMetrics metrics = new LayoutMetrics {
                RowHeight = Math.Max(26f, size + 12f), TextWidth = size * 0.6f,
                WindowPaddingLeft = 16f, WindowPaddingRight = 16f,
                WindowPaddingTop = Math.Max(32f, size + 18f) + 12f, WindowPaddingBottom = 14f,
                DialogPaddingLeft = 20f, DialogPaddingRight = 20f,
                DialogPaddingTop = Math.Max(32f, size + 18f) + 16f, DialogPaddingBottom = 18f
            };
            metrics.MeasureText = delegate(string text) { return (text ?? string.Empty).Length * metrics.TextWidth; };
            metrics.MeasureLabelHeight = delegate(Label label, float width) {
                return label.WordWrap ? Math.Max(metrics.RowHeight,
                    (float)Math.Ceiling(metrics.MeasureText(label.Text) / Math.Max(1f, width)) * (size + 6f)) : metrics.RowHeight;
            };
            return metrics;
        }

        private static void Equal(float expected, float actual, string message) {
            if (Math.Abs(expected - actual) > 0.01f)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
