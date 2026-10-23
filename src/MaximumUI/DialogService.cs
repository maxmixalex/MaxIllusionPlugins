using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class DialogService {
        private const float MinDialogWidth = 220f;
        private const float MaxDialogWidth = 520f;
        private const float ContentGap = 8f;
        private const float ButtonGap = 6f;

        private readonly List<Dialog> dialogs = new List<Dialog>();
        private readonly Func<LayoutMetrics> metricsProvider;
        private readonly Func<MuiVec2> viewportProvider;
        public MaximumTheme Theme;

        public DialogService() { }

        // Tests exercise the actual service and layout engine without invoking
        // native GUIStyle/Screen. Runtime still obtains both from the current theme.
        internal DialogService(Func<LayoutMetrics> metricsProvider, Func<MuiVec2> viewportProvider) {
            this.metricsProvider = metricsProvider;
            this.viewportProvider = viewportProvider;
        }

        public IList<Dialog> Dialogs {
            get { return dialogs; }
        }

        public bool HasOpenDialogs {
            get { return dialogs.Count > 0; }
        }

        public void Alert(string title, string message, Action onClose = null) {
            VStack content;
            float buttonWidth;
            float rowHeight;
            Dialog dialog = CreateDialog(title, message, "alert:", 1, out content, out buttonWidth, out rowHeight);
            content.Add(new HStack(gap: ButtonGap) {
                new Spacer(),
                new Button("确定", delegate {
                    Close(dialog);
                }).Width(buttonWidth)
            }.Height(rowHeight));
            dialog.Add(content);
            dialog.OnClose = onClose;
            Show(dialog);
        }

        public void Confirm(string title, string message, Action onConfirm, Action onCancel = null) {
            VStack content;
            float buttonWidth;
            float rowHeight;
            Dialog dialog = CreateDialog(title, message, "confirm:", 2, out content, out buttonWidth, out rowHeight);
            content.Add(new HStack(gap: ButtonGap) {
                new Spacer(),
                new Button("取消", delegate {
                    Close(dialog);
                }).Width(buttonWidth),
                new Button("确定", delegate {
                    Complete(dialog, onConfirm);
                }).Width(buttonWidth)
            }.Height(rowHeight));
            dialog.Add(content);
            dialog.OnClose = onCancel;
            Show(dialog);
        }

        public void Show(Dialog dialog) {
            if (dialog != null) dialogs.Add(dialog);
        }

        public void CloseTop() {
            if (dialogs.Count == 0) return;

            Dialog dialog = dialogs[dialogs.Count - 1];
            dialogs.RemoveAt(dialogs.Count - 1);
            if (dialog.OnClose != null) dialog.OnClose();
        }

        public void Close(Dialog dialog) {
            if (dialog == null) return;

            for (int i = dialogs.Count - 1; i >= 0; i--) {
                if (dialogs[i] != dialog) continue;

                dialogs.RemoveAt(i);
                if (dialog.OnClose != null) dialog.OnClose();
                return;
            }
        }

        public void CloseAll() {
            Dialog[] closing = dialogs.ToArray();
            dialogs.Clear();
            Exception firstError = null;
            for (int i = closing.Length - 1; i >= 0; i--) {
                try {
                    if (closing[i].OnClose != null) closing[i].OnClose();
                } catch (Exception error) {
                    if (firstError == null) firstError = error;
                }
            }
            if (firstError != null) throw firstError;
        }

        // Mount teardown/hiding is not a dialog result. Never run a stale
        // confirmation or open another dialog from a hidden owner.
        internal void DismissAll() {
            dialogs.Clear();
        }

        internal void Complete(Dialog dialog, Action result) {
            if (dialog == null || !dialogs.Remove(dialog)) return;
            // Remove the originating dialog first; the callback may open another one.
            if (result != null) result();
        }

        private Dialog CreateDialog(string title, string message, string key, int buttonCount,
            out VStack content, out float buttonWidth, out float rowHeight) {
            string dialogKey = key + dialogs.Count;
            LayoutMetrics metrics = metricsProvider != null ? metricsProvider() : (Theme ?? MaximumTheme.MetroLight).CreateLayoutMetrics();
            MuiVec2 viewport = viewportProvider != null ? viewportProvider() : NativeViewport();
            LayoutEngine engine = new LayoutEngine(metrics);
            float horizontalPadding = metrics.EffectiveDialogPaddingLeft + metrics.EffectiveDialogPaddingRight;
            float verticalPadding = metrics.EffectiveDialogPaddingTop + metrics.EffectiveDialogPaddingBottom;
            rowHeight = metrics.RowHeight;
            buttonWidth = Math.Max(72f, Math.Max(TextWidth("确定", metrics), TextWidth("取消", metrics)) + 24f);
            float naturalWidth = Math.Max(MinDialogWidth, Math.Max(TextWidth(message, metrics),
                buttonCount * buttonWidth + Math.Max(0, buttonCount - 1) * ButtonGap) + horizontalPadding);
            float width = Math.Min(MaxDialogWidth, naturalWidth);
            if (viewport.X > 0f) width = Math.Min(width, Math.Max(120f, viewport.X - 40f));
            float contentWidth = Math.Max(1f, width - horizontalPadding);
            Label label = new Label(message ?? string.Empty) { WordWrap = true };
            float messageHeight = MeasureMessage(engine, label, contentWidth);
            float separatorHeight = engine.Measure(new Separator(),
                new LayoutConstraints(0f, contentWidth, 0f, float.PositiveInfinity), "separator").DesiredSize.Y;
            float frameHeight = verticalPadding + rowHeight + separatorHeight + ContentGap * 2f;
            float availableMessageHeight = viewport.Y > 0f ? Math.Max(rowHeight, viewport.Y - 40f - frameHeight) : messageHeight;
            Widget messageWidget;
            if (messageHeight > availableMessageHeight) {
                // Keep action buttons in view; measure again for the scrollbar gutter.
                messageHeight = MeasureMessage(engine, label, Math.Max(1f, contentWidth - 16f));
                messageWidget = new ScrollView(key: dialogKey + ":message") {
                    Height = availableMessageHeight, AlwaysShowVertical = true
                }.Content(label.Height(messageHeight).ExpandWidth()).Height(availableMessageHeight).ExpandWidth();
            } else {
                availableMessageHeight = messageHeight;
                messageWidget = label.Height(messageHeight).ExpandWidth();
            }
            content = new VStack(gap: ContentGap) { messageWidget, new Separator() };
            return new Dialog(title, dialogKey) {
                Width = width, Height = Math.Max(108f, frameHeight + availableMessageHeight)
            };
        }

        private static float MeasureMessage(LayoutEngine engine, Label label, float width) {
            return engine.Measure(label, new LayoutConstraints(0f, width, 0f, float.PositiveInfinity), "message").DesiredSize.Y;
        }

        private static float TextWidth(string text, LayoutMetrics metrics) {
            string[] lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            float width = 0f;
            for (int i = 0; i < lines.Length; i++) {
                width = Math.Max(width, metrics.MeasureText != null ? metrics.MeasureText(lines[i]) : lines[i].Length * metrics.TextWidth);
            }
            return width;
        }

        private static MuiVec2 NativeViewport() {
            return new MuiVec2(Screen.width, Screen.height);
        }
    }
}
