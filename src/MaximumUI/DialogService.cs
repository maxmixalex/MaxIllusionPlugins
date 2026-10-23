using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class DialogService {
        private const float MinDialogWidth = 220f;
        private const float MaxDialogWidth = 520f;
        private const float DialogPadding = 28f;
        private const float DialogButtonHeight = 24f;

        private readonly List<Dialog> dialogs = new List<Dialog>();

        public IList<Dialog> Dialogs {
            get { return dialogs; }
        }

        public bool HasOpenDialogs {
            get { return dialogs.Count > 0; }
        }

        public void Alert(string title, string message, Action onClose = null) {
            float width = MeasureDialogWidth(message, 1);
            float messageHeight = MeasureMessageHeight(message, width);
            Dialog dialog = new Dialog(title, "alert:" + dialogs.Count) {
                Width = width,
                Height = MeasureDialogHeight(messageHeight)
            };
            dialog.Add(new VStack(gap: 8f) {
                new Label(message ?? string.Empty) { WordWrap = true }.Height(messageHeight),
                new Separator(),
                new HStack(gap: 6f) {
                    new Spacer(),
                    new Button("确定", delegate {
                        CloseTop();
                    }).Width(72f)
                }
            });
            dialog.OnClose = onClose;
            Show(dialog);
        }

        public void Confirm(string title, string message, Action onConfirm, Action onCancel = null) {
            float width = MeasureDialogWidth(message, 2);
            float messageHeight = MeasureMessageHeight(message, width);
            Dialog dialog = new Dialog(title, "confirm:" + dialogs.Count) {
                Width = width,
                Height = MeasureDialogHeight(messageHeight)
            };
            dialog.Add(new VStack(gap: 8f) {
                new Label(message ?? string.Empty) { WordWrap = true }.Height(messageHeight),
                new Separator(),
                new HStack(gap: 6f) {
                    new Spacer(),
                    new Button("取消", delegate {
                        if (onCancel != null) onCancel();
                        CloseTop();
                    }).Width(72f),
                    new Button("确定", delegate {
                        if (onConfirm != null) onConfirm();
                        CloseTop();
                    }).Width(72f)
                }
            });
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
            while (dialogs.Count > 0) CloseTop();
        }

        private static float MeasureDialogWidth(string message, int buttonCount) {
            int fontSize = MaximumTheme.ExpressionLight.FontSize;
            float contentWidth = EstimateMessageWidth(message, fontSize);
            float buttonWidth = Mathf.Max(0, buttonCount) * 78f + Mathf.Max(0, buttonCount - 1) * 6f;
            float width = Mathf.Max(MinDialogWidth, Mathf.Max(contentWidth + DialogPadding, buttonWidth + DialogPadding));
            if (Screen.width > 0) width = Mathf.Min(width, Mathf.Max(MinDialogWidth, Screen.width - 40f));
            return Mathf.Min(MaxDialogWidth, width);
        }

        private static float MeasureDialogHeight(float messageHeight) {
            float height = 62f + messageHeight + DialogButtonHeight;
            if (Screen.height > 0) height = Mathf.Min(height, Mathf.Max(96f, Screen.height - 40f));
            return Mathf.Max(108f, height);
        }

        private static float MeasureMessageHeight(string message, float dialogWidth) {
            int fontSize = MaximumTheme.ExpressionLight.FontSize;
            int lines = CountWrappedLines(message, Mathf.Max(1f, dialogWidth - DialogPadding), fontSize);
            return Mathf.Max(22f, lines * (fontSize + 6f));
        }

        private static float EstimateMessageWidth(string message, int fontSize) {
            string text = message ?? string.Empty;
            int longest = 0;
            int current = 0;
            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                if (ch == '\r') continue;
                if (ch == '\n') {
                    if (current > longest) longest = current;
                    current = 0;
                } else {
                    current++;
                }
            }
            if (current > longest) longest = current;
            return Mathf.Max(120f, longest * fontSize * 0.62f);
        }

        private static int CountWrappedLines(string message, float contentWidth, int fontSize) {
            if (string.IsNullOrEmpty(message)) return 1;

            int lines = 1;
            int current = 0;
            int maxCharsPerLine = Mathf.Max(1, Mathf.FloorToInt(contentWidth / Mathf.Max(1f, fontSize * 0.62f)));
            for (int i = 0; i < message.Length; i++) {
                char ch = message[i];
                if (ch == '\r') continue;
                if (ch == '\n') {
                    lines++;
                    current = 0;
                } else {
                    current++;
                    if (current > maxCharsPerLine) {
                        lines++;
                        current = 1;
                    }
                }
            }
            return lines;
        }
    }
}
