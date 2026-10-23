using System;

namespace MaximumUI {
    public sealed class Label : Widget {
        public string Text;
        public bool WordWrap;
        public object Style;

        public Label(string text, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            WordWrap = false;
            Style = null;
        }
    }

    public sealed class BulletText : Widget {
        public string Text;

        public BulletText(string text, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
        }
    }

    public sealed class ValueLabel : Widget {
        public string Label;
        public string Value;
        public float LabelWidth;

        public ValueLabel(string label, string value, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            LabelWidth = 0f;
        }
    }

    public sealed class TextLink : Widget {
        public string Text;
        public Action OnClick;

        public TextLink(string text, Action onClick = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            OnClick = onClick;
        }
    }

    public sealed class Button : Widget {
        public string Text;
        public Action OnClick;

        public Button(string text, Action onClick = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            OnClick = onClick;
        }
    }

    public sealed class SmallButton : Widget {
        public string Text;
        public Action OnClick;

        public SmallButton(string text, Action onClick = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            OnClick = onClick;
        }
    }

    public enum ArrowDirection {
        Left,
        Right,
        Up,
        Down
    }

    public sealed class ArrowButton : Widget {
        public ArrowDirection Direction;
        public Action OnClick;

        public ArrowButton(ArrowDirection direction, Action onClick = null, object key = null)
            : base(key) {
            Direction = direction;
            OnClick = onClick;
        }
    }

    public sealed class Toggle : Widget {
        public string Text;
        public bool Value;
        public Action<bool> OnChange;

        public Toggle(string text, bool value, Action<bool> onChange = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Value = value;
            OnChange = onChange;
        }
    }

    public sealed class Checkbox : Widget {
        public string Text;
        public bool Value;
        public Action<bool> OnChange;

        public Checkbox(string text, bool value, Action<bool> onChange = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Value = value;
            OnChange = onChange;
        }
    }

    public sealed class RadioButton : Widget {
        public string Text;
        public bool Selected;
        public Action OnSelect;

        public RadioButton(string text, bool selected, Action onSelect = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Selected = selected;
            OnSelect = onSelect;
        }
    }

    public sealed class Slider : Widget {
        public float Value;
        public float Min;
        public float Max;
        public Action<float> OnChange;

        public Slider(float value, float min, float max, Action<float> onChange = null, object key = null)
            : base(key) {
            Value = value;
            Min = min;
            Max = max;
            OnChange = onChange;
        }
    }

    public sealed class InputInt : Widget {
        public string Label;
        public int Value;
        public int Step;
        public int StepFast;
        public int Min;
        public int Max;
        public bool HasMin;
        public bool HasMax;
        public string Format;
        public Action<int> OnChange;

        public InputInt(string label, int value, Action<int> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Step = 1;
            StepFast = 10;
            Min = 0;
            Max = 0;
            HasMin = false;
            HasMax = false;
            Format = "{0}";
            OnChange = onChange;
        }
    }

    public sealed class InputFloat : Widget {
        public string Label;
        public float Value;
        public float Step;
        public float StepFast;
        public float Min;
        public float Max;
        public bool HasMin;
        public bool HasMax;
        public string Format;
        public Action<float> OnChange;

        public InputFloat(string label, float value, Action<float> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Step = 0.1f;
            StepFast = 1f;
            Min = 0f;
            Max = 0f;
            HasMin = false;
            HasMax = false;
            Format = "0.###";
            OnChange = onChange;
        }
    }

    public sealed class InputDouble : Widget {
        public string Label;
        public double Value;
        public double Step;
        public double StepFast;
        public double Min;
        public double Max;
        public bool HasMin;
        public bool HasMax;
        public string Format;
        public Action<double> OnChange;

        public InputDouble(string label, double value, Action<double> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Step = 0.1;
            StepFast = 1.0;
            Min = 0.0;
            Max = 0.0;
            HasMin = false;
            HasMax = false;
            Format = "0.###";
            OnChange = onChange;
        }
    }

    public sealed class DragInt : Widget {
        public string Label;
        public int Value;
        public float Speed;
        public int Min;
        public int Max;
        public bool HasMin;
        public bool HasMax;
        public string Format;
        public Action<int> OnChange;

        public DragInt(string label, int value, Action<int> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Speed = 1f;
            Min = 0;
            Max = 0;
            HasMin = false;
            HasMax = false;
            Format = "{0}";
            OnChange = onChange;
        }
    }

    public sealed class DragFloat : Widget {
        public string Label;
        public float Value;
        public float Speed;
        public float Min;
        public float Max;
        public bool HasMin;
        public bool HasMax;
        public string Format;
        public Action<float> OnChange;

        public DragFloat(string label, float value, Action<float> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Speed = 0.01f;
            Min = 0f;
            Max = 0f;
            HasMin = false;
            HasMax = false;
            Format = "0.###";
            OnChange = onChange;
        }
    }

    public sealed class SliderInt : Widget {
        public string Label;
        public int Value;
        public int Min;
        public int Max;
        public string Format;
        public Action<int> OnChange;

        public SliderInt(string label, int value, int min, int max, Action<int> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Min = min;
            Max = max;
            Format = "{0}";
            OnChange = onChange;
        }
    }

    public sealed class SliderFloat : Widget {
        public string Label;
        public float Value;
        public float Min;
        public float Max;
        public string Format;
        public Action<float> OnChange;

        public SliderFloat(string label, float value, float min, float max, Action<float> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Min = min;
            Max = max;
            Format = "0.###";
            OnChange = onChange;
        }
    }

    public sealed class SliderAngle : Widget {
        public string Label;
        public float ValueDegrees;
        public float MinDegrees;
        public float MaxDegrees;
        public Action<float> OnChange;

        public SliderAngle(string label, float valueDegrees, Action<float> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            ValueDegrees = valueDegrees;
            MinDegrees = -180f;
            MaxDegrees = 180f;
            OnChange = onChange;
        }
    }

    public sealed class ProgressBar : Widget {
        public float Value;
        public string Text;
        public bool Indeterminate;

        public ProgressBar(float value, string text = null, object key = null)
            : base(key) {
            Value = value;
            Text = text;
            Indeterminate = false;
        }
    }

    public enum StatusBadgeKind {
        Info,
        Success,
        Warning,
        Error
    }

    public sealed class StatusBadge : Widget {
        public string Text;
        public StatusBadgeKind Kind;

        public StatusBadge(string text, StatusBadgeKind kind = StatusBadgeKind.Info, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Kind = kind;
        }
    }

    public sealed class LoadingIndicator : Widget {
        public string Text;
        public bool Indeterminate;

        public LoadingIndicator(string text = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Indeterminate = true;
        }
    }

    public sealed class Image : Widget {
        public object Texture;
        public MuiColor Tint;
        public float AspectRatio;

        public Image(object texture, object key = null)
            : base(key) {
            Texture = texture;
            Tint = new MuiColor(1f, 1f, 1f, 1f);
            AspectRatio = 1f;
        }
    }

    public sealed class ImageButton : Widget {
        public object Texture;
        public string Text;
        public Action OnClick;
        public MuiColor Tint;

        public ImageButton(object texture, Action onClick = null, object key = null)
            : base(key) {
            Texture = texture;
            Text = string.Empty;
            OnClick = onClick;
            Tint = new MuiColor(1f, 1f, 1f, 1f);
        }
    }

    [Flags]
    public enum TextInputFlags {
        None = 0,
        ReadOnly = 1,
        Decimal = 2,
        Hexadecimal = 4,
        Scientific = 8,
        Uppercase = 16,
        NoBlank = 32,
        EnterReturnsTrue = 64,
        EscapeClearsAll = 128,
        EscapeReverts = 256
    }

    public static class TextInputFilter {
        public static string Apply(string value, TextInputFlags flags, Func<char, bool> charFilter = null, int maxLength = 0) {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            string source = Has(flags, TextInputFlags.Uppercase) ? value.ToUpperInvariant() : value;
            System.Text.StringBuilder builder = new System.Text.StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++) {
                char ch = source[i];
                if (Has(flags, TextInputFlags.NoBlank) && char.IsWhiteSpace(ch)) continue;
                if (!PassesBuiltInFilter(ch, flags)) continue;
                if (charFilter != null && !charFilter(ch)) continue;
                builder.Append(ch);
                if (maxLength > 0 && builder.Length >= maxLength) break;
            }

            return builder.ToString();
        }

        public static bool Has(TextInputFlags flags, TextInputFlags flag) {
            return (flags & flag) == flag;
        }

        private static bool PassesBuiltInFilter(char ch, TextInputFlags flags) {
            if (Has(flags, TextInputFlags.Hexadecimal)) {
                return (ch >= '0' && ch <= '9') ||
                    (ch >= 'a' && ch <= 'f') ||
                    (ch >= 'A' && ch <= 'F');
            }

            if (Has(flags, TextInputFlags.Scientific)) {
                return (ch >= '0' && ch <= '9') || ch == '-' || ch == '+' || ch == '.' || ch == 'e' || ch == 'E';
            }

            if (Has(flags, TextInputFlags.Decimal)) {
                return (ch >= '0' && ch <= '9') || ch == '-' || ch == '+' || ch == '.';
            }

            return true;
        }
    }

    public sealed class TextField : Widget {
        public string Value;
        public Action<string> OnChange;
        public string Placeholder;
        public int MaxLength;
        public object Style;
        public Action<string> OnSubmit;
        public Action<string> OnBlur;
        public Action<string> OnEdit;
        public Func<char, bool> CharFilter;
        public TextInputFlags Flags;
        public bool AutoFocus;

        public TextField(string value, Action<string> onChange = null, object key = null)
            : base(key) {
            Value = value ?? string.Empty;
            OnChange = onChange;
            Placeholder = string.Empty;
            MaxLength = 0;
            OnSubmit = null;
            OnBlur = null;
            OnEdit = null;
            CharFilter = null;
            Flags = TextInputFlags.None;
            AutoFocus = false;
        }
    }

    public sealed class TextArea : Widget {
        public string Value;
        public Action<string> OnChange;
        public string Placeholder;
        public int MaxLength;
        public object Style;
        public Action<string> OnSubmit;
        public Action<string> OnBlur;
        public Action<string> OnEdit;
        public Func<char, bool> CharFilter;
        public TextInputFlags Flags;

        public TextArea(string value, Action<string> onChange = null, object key = null)
            : base(key) {
            Value = value ?? string.Empty;
            OnChange = onChange;
            Placeholder = string.Empty;
            MaxLength = 0;
            Style = null;
            OnSubmit = null;
            OnBlur = null;
            OnEdit = null;
            CharFilter = null;
            Flags = TextInputFlags.None;
        }
    }

    public sealed class PasswordField : Widget {
        public string Value;
        public Action<string> OnChange;
        public string Placeholder;
        public int MaxLength;
        public char MaskChar;
        public object Style;
        public Action<string> OnSubmit;
        public Action<string> OnBlur;
        public Action<string> OnEdit;
        public Func<char, bool> CharFilter;
        public TextInputFlags Flags;

        public PasswordField(string value, Action<string> onChange = null, object key = null)
            : base(key) {
            Value = value ?? string.Empty;
            OnChange = onChange;
            Placeholder = string.Empty;
            MaxLength = 0;
            MaskChar = '*';
            Style = null;
            OnSubmit = null;
            OnBlur = null;
            OnEdit = null;
            CharFilter = null;
            Flags = TextInputFlags.None;
        }
    }

    public sealed class SearchBox : Widget {
        public string Value;
        public Action<string> OnChange;
        public string Placeholder;
        public int MaxLength;
        public Action<string> OnSubmit;
        public Action<string> OnBlur;
        public Action<string> OnEdit;
        public Func<char, bool> CharFilter;
        public TextInputFlags Flags;

        public SearchBox(string value, Action<string> onChange = null, object key = null)
            : base(key) {
            Value = value ?? string.Empty;
            OnChange = onChange;
            Placeholder = "Search";
            MaxLength = 0;
            OnSubmit = null;
            OnBlur = null;
            OnEdit = null;
            CharFilter = null;
            Flags = TextInputFlags.None;
        }
    }

    public sealed class TextFieldState {
        public string EditingValue = string.Empty;
        public bool HasEditingValue;
        public bool Focused;
        public bool WasFocused;
        public string ControlName;
        public bool AutoFocusApplied;
        public string FocusStartValue = string.Empty;
        public bool HasFocusStartValue;
    }

    public enum HelpBoxType {
        Info,
        Warning,
        Error
    }

    public sealed class HelpBox : Widget {
        public string Message;
        public HelpBoxType Type;

        public HelpBox(string message, HelpBoxType type = HelpBoxType.Info, object key = null)
            : base(key) {
            Message = message ?? string.Empty;
            Type = type;
        }
    }

    public sealed class Separator : Widget {
        public Separator(object key = null)
            : base(key) {
        }
    }

    public sealed class SeparatorText : Widget {
        public string Text;

        public SeparatorText(string text, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
        }
    }

    public sealed class Space : Widget {
        public float Pixels;

        public Space(float pixels, object key = null)
            : base(key) {
            Pixels = pixels;
        }
    }

    public sealed class Spacer : Widget {
        public Spacer(object key = null)
            : base(key) {
        }
    }

}
