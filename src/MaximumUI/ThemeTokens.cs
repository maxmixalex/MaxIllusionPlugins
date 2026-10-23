namespace MaximumUI {
    public enum ThemeColorToken {
        WindowBg,
        PanelBg,
        DialogBg,
        DropdownBg,
        Backdrop,
        Text,
        TextDisabled,
        Border,
        Separator,
        Accent,
        ButtonBg,
        ButtonHoverBg,
        ButtonActiveBg,
        ButtonDisabledBg,
        FieldBg,
        FieldHoverBg,
        FieldActiveBg,
        SelectedBg,
        InfoBg,
        SuccessBg,
        WarningBg,
        ErrorBg,
        TitleBg,
        TextMuted,
        OnAccent,
        AccentHover,
        FocusRing,
        ControlBorder,
        ControlHoverBorder,
        AccentPressed
    }

    public enum ThemeScalarToken {
        BorderWidth,
        Radius,
        FontSize,
        ToggleBoxSize,
        SliderTrackHeight,
        SliderThumbWidth,
        SliderThumbHeight
    }

    public sealed class ThemeTokens {
        public MuiColor WindowBg = new MuiColor(0.80f, 0.80f, 0.80f, 0.80f);
        public MuiColor PanelBg = new MuiColor(0.78f, 0.78f, 0.78f, 0.80f);
        public MuiColor DialogBg = new MuiColor(0.80f, 0.80f, 0.80f, 0.80f);
        public MuiColor DropdownBg = new MuiColor(0.80f, 0.80f, 0.80f, 0.80f);
        public MuiColor Backdrop = new MuiColor(0f, 0f, 0f, 0.35f);

        public MuiColor Text = new MuiColor(0f, 0f, 0f, 1f);
        public MuiColor TextDisabled = new MuiColor(0f, 0f, 0f, 0.45f);
        public MuiColor Border = new MuiColor(0f, 0f, 0f, 1f);
        public MuiColor Separator = new MuiColor(0f, 0f, 0f, 1f);
        public MuiColor Accent = new MuiColor(0.12f, 0.12f, 0.12f, 1f);

        public MuiColor ButtonBg = new MuiColor(0.90f, 0.90f, 0.90f, 1f);
        public MuiColor ButtonHoverBg = new MuiColor(0.96f, 0.96f, 0.96f, 1f);
        public MuiColor ButtonActiveBg = new MuiColor(0.70f, 0.70f, 0.70f, 1f);
        public MuiColor ButtonDisabledBg = new MuiColor(0.72f, 0.72f, 0.72f, 0.65f);

        public MuiColor FieldBg = new MuiColor(0.88f, 0.88f, 0.88f, 1f);
        public MuiColor FieldHoverBg = new MuiColor(0.96f, 0.96f, 0.96f, 1f);
        public MuiColor FieldActiveBg = new MuiColor(1f, 1f, 1f, 1f);

        public MuiColor SelectedBg = new MuiColor(0.66f, 0.66f, 0.66f, 1f);
        public MuiColor InfoBg = new MuiColor(0.62f, 0.78f, 0.96f, 1f);
        public MuiColor SuccessBg = new MuiColor(0.58f, 0.82f, 0.62f, 1f);
        public MuiColor WarningBg = new MuiColor(0.95f, 0.88f, 0.50f, 1f);
        public MuiColor ErrorBg = new MuiColor(0.94f, 0.55f, 0.55f, 1f);

        public MuiColor TitleBg = new MuiColor(0.78f, 0.78f, 0.78f, 1f);
        public MuiColor TextMuted = new MuiColor(0.30f, 0.30f, 0.30f, 1f);
        public MuiColor OnAccent = new MuiColor(1f, 1f, 1f, 1f);
        public MuiColor AccentHover = new MuiColor(0.24f, 0.24f, 0.24f, 1f);
        public MuiColor FocusRing = new MuiColor(0.12f, 0.42f, 0.80f, 1f);
        public MuiColor ControlBorder = new MuiColor(0.48f, 0.48f, 0.48f, 1f);
        public MuiColor ControlHoverBorder = new MuiColor(0.1f, 0.1f, 0.1f, 1f);
        public MuiColor AccentPressed = new MuiColor(0f, 0.35f, 0.62f, 1f);

        public float BorderWidth = 1f;
        public float Radius = 4f;
        public float FontSize = 12f;
        public float ToggleBoxSize = 14f;
        public float SliderTrackHeight = 4f;
        public float SliderThumbWidth = 10f;
        public float SliderThumbHeight = 12f;

        public ThemeTokens Clone() {
            ThemeTokens clone = new ThemeTokens();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(ThemeTokens source) {
            if (source == null) return;

            WindowBg = source.WindowBg;
            PanelBg = source.PanelBg;
            DialogBg = source.DialogBg;
            DropdownBg = source.DropdownBg;
            Backdrop = source.Backdrop;
            Text = source.Text;
            TextDisabled = source.TextDisabled;
            Border = source.Border;
            Separator = source.Separator;
            Accent = source.Accent;
            ButtonBg = source.ButtonBg;
            ButtonHoverBg = source.ButtonHoverBg;
            ButtonActiveBg = source.ButtonActiveBg;
            ButtonDisabledBg = source.ButtonDisabledBg;
            FieldBg = source.FieldBg;
            FieldHoverBg = source.FieldHoverBg;
            FieldActiveBg = source.FieldActiveBg;
            SelectedBg = source.SelectedBg;
            InfoBg = source.InfoBg;
            SuccessBg = source.SuccessBg;
            WarningBg = source.WarningBg;
            ErrorBg = source.ErrorBg;
            TitleBg = source.TitleBg;
            TextMuted = source.TextMuted;
            OnAccent = source.OnAccent;
            AccentHover = source.AccentHover;
            FocusRing = source.FocusRing;
            ControlBorder = source.ControlBorder;
            ControlHoverBorder = source.ControlHoverBorder;
            AccentPressed = source.AccentPressed;
            BorderWidth = source.BorderWidth;
            Radius = source.Radius;
            FontSize = source.FontSize;
            ToggleBoxSize = source.ToggleBoxSize;
            SliderTrackHeight = source.SliderTrackHeight;
            SliderThumbWidth = source.SliderThumbWidth;
            SliderThumbHeight = source.SliderThumbHeight;
        }

        public MuiColor GetColor(ThemeColorToken token) {
            if (token == ThemeColorToken.WindowBg) return WindowBg;
            if (token == ThemeColorToken.PanelBg) return PanelBg;
            if (token == ThemeColorToken.DialogBg) return DialogBg;
            if (token == ThemeColorToken.DropdownBg) return DropdownBg;
            if (token == ThemeColorToken.Backdrop) return Backdrop;
            if (token == ThemeColorToken.Text) return Text;
            if (token == ThemeColorToken.TextDisabled) return TextDisabled;
            if (token == ThemeColorToken.Border) return Border;
            if (token == ThemeColorToken.Separator) return Separator;
            if (token == ThemeColorToken.Accent) return Accent;
            if (token == ThemeColorToken.ButtonBg) return ButtonBg;
            if (token == ThemeColorToken.ButtonHoverBg) return ButtonHoverBg;
            if (token == ThemeColorToken.ButtonActiveBg) return ButtonActiveBg;
            if (token == ThemeColorToken.ButtonDisabledBg) return ButtonDisabledBg;
            if (token == ThemeColorToken.FieldBg) return FieldBg;
            if (token == ThemeColorToken.FieldHoverBg) return FieldHoverBg;
            if (token == ThemeColorToken.FieldActiveBg) return FieldActiveBg;
            if (token == ThemeColorToken.SelectedBg) return SelectedBg;
            if (token == ThemeColorToken.InfoBg) return InfoBg;
            if (token == ThemeColorToken.SuccessBg) return SuccessBg;
            if (token == ThemeColorToken.WarningBg) return WarningBg;
            if (token == ThemeColorToken.TitleBg) return TitleBg;
            if (token == ThemeColorToken.TextMuted) return TextMuted;
            if (token == ThemeColorToken.OnAccent) return OnAccent;
            if (token == ThemeColorToken.AccentHover) return AccentHover;
            if (token == ThemeColorToken.FocusRing) return FocusRing;
            if (token == ThemeColorToken.ControlBorder) return ControlBorder;
            if (token == ThemeColorToken.ControlHoverBorder) return ControlHoverBorder;
            if (token == ThemeColorToken.AccentPressed) return AccentPressed;
            return ErrorBg;
        }

        public void SetColor(ThemeColorToken token, MuiColor color) {
            if (token == ThemeColorToken.WindowBg) WindowBg = color;
            else if (token == ThemeColorToken.PanelBg) PanelBg = color;
            else if (token == ThemeColorToken.DialogBg) DialogBg = color;
            else if (token == ThemeColorToken.DropdownBg) DropdownBg = color;
            else if (token == ThemeColorToken.Backdrop) Backdrop = color;
            else if (token == ThemeColorToken.Text) Text = color;
            else if (token == ThemeColorToken.TextDisabled) TextDisabled = color;
            else if (token == ThemeColorToken.Border) Border = color;
            else if (token == ThemeColorToken.Separator) Separator = color;
            else if (token == ThemeColorToken.Accent) Accent = color;
            else if (token == ThemeColorToken.ButtonBg) ButtonBg = color;
            else if (token == ThemeColorToken.ButtonHoverBg) ButtonHoverBg = color;
            else if (token == ThemeColorToken.ButtonActiveBg) ButtonActiveBg = color;
            else if (token == ThemeColorToken.ButtonDisabledBg) ButtonDisabledBg = color;
            else if (token == ThemeColorToken.FieldBg) FieldBg = color;
            else if (token == ThemeColorToken.FieldHoverBg) FieldHoverBg = color;
            else if (token == ThemeColorToken.FieldActiveBg) FieldActiveBg = color;
            else if (token == ThemeColorToken.SelectedBg) SelectedBg = color;
            else if (token == ThemeColorToken.InfoBg) InfoBg = color;
            else if (token == ThemeColorToken.SuccessBg) SuccessBg = color;
            else if (token == ThemeColorToken.WarningBg) WarningBg = color;
            else if (token == ThemeColorToken.TitleBg) TitleBg = color;
            else if (token == ThemeColorToken.TextMuted) TextMuted = color;
            else if (token == ThemeColorToken.OnAccent) OnAccent = color;
            else if (token == ThemeColorToken.AccentHover) AccentHover = color;
            else if (token == ThemeColorToken.FocusRing) FocusRing = color;
            else if (token == ThemeColorToken.ControlBorder) ControlBorder = color;
            else if (token == ThemeColorToken.ControlHoverBorder) ControlHoverBorder = color;
            else if (token == ThemeColorToken.AccentPressed) AccentPressed = color;
            else ErrorBg = color;
        }

        public float GetScalar(ThemeScalarToken token) {
            if (token == ThemeScalarToken.BorderWidth) return BorderWidth;
            if (token == ThemeScalarToken.Radius) return Radius;
            if (token == ThemeScalarToken.FontSize) return FontSize;
            if (token == ThemeScalarToken.ToggleBoxSize) return ToggleBoxSize;
            if (token == ThemeScalarToken.SliderTrackHeight) return SliderTrackHeight;
            if (token == ThemeScalarToken.SliderThumbWidth) return SliderThumbWidth;
            return SliderThumbHeight;
        }

        public void SetScalar(ThemeScalarToken token, float value) {
            if (token == ThemeScalarToken.BorderWidth) BorderWidth = value;
            else if (token == ThemeScalarToken.Radius) Radius = value;
            else if (token == ThemeScalarToken.FontSize) FontSize = value;
            else if (token == ThemeScalarToken.ToggleBoxSize) ToggleBoxSize = value;
            else if (token == ThemeScalarToken.SliderTrackHeight) SliderTrackHeight = value;
            else if (token == ThemeScalarToken.SliderThumbWidth) SliderThumbWidth = value;
            else SliderThumbHeight = value;
        }
    }

    public sealed class WidgetPaintState {
        /// <summary>Effective ancestor state when painting a subtree (for example inside a clipped viewport).</summary>
        public bool ParentEnabled = true;
        public string HoveredPath;
        public string ActivePath;
        public string FocusedPath;
        public string OpenPopupPath;
        public int OpenMenuIndex = -1;

        public bool IsHovered(LayoutNode node) {
            return node != null && string.Equals(node.Path, HoveredPath, System.StringComparison.Ordinal);
        }

        public bool IsActive(LayoutNode node) {
            return node != null && string.Equals(node.Path, ActivePath, System.StringComparison.Ordinal);
        }

        public bool IsFocused(LayoutNode node) {
            return node != null && string.Equals(node.Path, FocusedPath, System.StringComparison.Ordinal);
        }

        public bool IsPopupOpen(LayoutNode node) {
            return node != null && !string.IsNullOrEmpty(OpenPopupPath) && string.Equals(node.Path, OpenPopupPath, System.StringComparison.Ordinal);
        }
    }
}
