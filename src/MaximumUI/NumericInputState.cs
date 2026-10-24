using System;
using System.Collections.Generic;
using System.Globalization;

namespace MaximumUI {
    internal enum NumericInputKind { Int, Float, Double }
    internal enum NumericParseStatus { Invalid, Incomplete, Valid }

    internal static class NumericInputParser {
        // NumberStyles.Float deliberately excludes AllowThousands. A period
        // entered in a comma-decimal culture must never turn 1.5 into 15.
        public static NumericParseStatus Parse(string text, NumericInputKind kind, CultureInfo culture, bool final, out double value) {
            value = 0;
            string input = (text ?? string.Empty).Trim();
            CultureInfo current = culture ?? CultureInfo.CurrentCulture;
            NumericParseStatus local = ParseCulture(input, kind, current, final, out value);
            if (local == NumericParseStatus.Valid) return local;
            NumericParseStatus invariant = ParseCulture(input, kind, CultureInfo.InvariantCulture, final, out value);
            if (invariant == NumericParseStatus.Valid) return invariant;
            value = 0;
            return local == NumericParseStatus.Incomplete || invariant == NumericParseStatus.Incomplete
                ? NumericParseStatus.Incomplete : NumericParseStatus.Invalid;
        }

        public static string Format(double value, NumericInputKind kind, CultureInfo culture) {
            CultureInfo current = culture ?? CultureInfo.CurrentCulture;
            if (kind == NumericInputKind.Int) return ((int)value).ToString(current);
            if (kind == NumericInputKind.Float) return ((float)value).ToString("R", current);
            return value.ToString("R", current);
        }

        internal static bool IsFinite(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static double Normalize(double value, NumericInputKind kind) {
            if (!IsFinite(value)) value = 0;
            if (kind == NumericInputKind.Int) return Math.Truncate(Math.Max(int.MinValue, Math.Min(int.MaxValue, value)));
            if (kind == NumericInputKind.Float) return (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, value));
            return value;
        }

        internal static double Clamp(double value, NumericInputKind kind, bool hasMin, double min, bool hasMax, double max) {
            hasMin = hasMin && IsFinite(min);
            hasMax = hasMax && IsFinite(max);
            if (hasMin && hasMax && min > max) { double swap = min; min = max; max = swap; }
            if (hasMin && value < min) value = min;
            if (hasMax && value > max) value = max;
            return Normalize(value, kind);
        }

        private static NumericParseStatus ParseCulture(string text, NumericInputKind kind, CultureInfo culture, bool final, out double value) {
            value = 0;
            NumericParseStatus grammar = CheckGrammar(text, kind, culture.NumberFormat, final);
            if (grammar != NumericParseStatus.Valid) return grammar;
            if (kind == NumericInputKind.Int) {
                int parsed;
                if (!int.TryParse(text, NumberStyles.Integer, culture, out parsed)) return NumericParseStatus.Invalid;
                value = parsed;
            } else if (kind == NumericInputKind.Float) {
                float parsed;
                if (!float.TryParse(text, NumberStyles.Float, culture, out parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed)) return NumericParseStatus.Invalid;
                value = parsed;
            } else {
                if (!double.TryParse(text, NumberStyles.Float, culture, out value) || !IsFinite(value)) return NumericParseStatus.Invalid;
            }
            return NumericParseStatus.Valid;
        }

        private static NumericParseStatus CheckGrammar(string text, NumericInputKind kind, NumberFormatInfo format, bool final) {
            int index = 0;
            TakeSign(text, ref index, format);
            if (index == text.Length) return NumericParseStatus.Incomplete;
            int wholeDigits = TakeDigits(text, ref index);
            if (kind == NumericInputKind.Int) return wholeDigits > 0 && index == text.Length ? NumericParseStatus.Valid : NumericParseStatus.Invalid;

            bool decimalPoint = Take(text, ref index, format.NumberDecimalSeparator);
            int fractionDigits = decimalPoint ? TakeDigits(text, ref index) : 0;
            if (wholeDigits + fractionDigits == 0)
                return index == text.Length && decimalPoint ? NumericParseStatus.Incomplete : NumericParseStatus.Invalid;
            bool exponent = index < text.Length && (text[index] == 'e' || text[index] == 'E');
            if (exponent) {
                index++;
                TakeSign(text, ref index, format);
                if (index == text.Length) return NumericParseStatus.Incomplete;
                if (TakeDigits(text, ref index) == 0) return NumericParseStatus.Invalid;
            }
            if (index != text.Length) return NumericParseStatus.Invalid;
            if (!final && decimalPoint && fractionDigits == 0 && !exponent) return NumericParseStatus.Incomplete;
            return NumericParseStatus.Valid;
        }

        private static int TakeDigits(string text, ref int index) {
            int start = index;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            return index - start;
        }

        private static void TakeSign(string text, ref int index, NumberFormatInfo format) {
            if (!Take(text, ref index, format.NegativeSign)) Take(text, ref index, format.PositiveSign);
        }

        private static bool Take(string text, ref int index, string token) {
            if (string.IsNullOrEmpty(token) || index + token.Length > text.Length) return false;
            for (int i = 0; i < token.Length; i++) if (text[index + i] != token[i]) return false;
            index += token.Length;
            return true;
        }
    }

    /// <summary>
    /// A controlled numeric field with an optimistic edit buffer. Call
    /// Synchronize before drawing, then BeginEdit/Edit/Commit/Cancel as focus
    /// and text change. A true result means the caller should publish next.
    /// </summary>
    internal sealed class NumericInputState {
        private readonly List<double> pending = new List<double>();
        private bool initialized;
        private double observedModel;
        private double editStartValue;
        private NumericInputKind kind;
        private CultureInfo culture;
        private bool hasMin;
        private bool hasMax;
        private double min;
        private double max;

        public string Text { get; private set; }
        public double Value { get; private set; }
        public bool IsEditing { get; private set; }

        public NumericInputState() { Text = string.Empty; }

        public void Synchronize(double modelValue, NumericInputKind inputKind, bool minimum, double minimumValue,
            bool maximum, double maximumValue, CultureInfo inputCulture = null) {
            hasMin = minimum;
            min = minimumValue;
            hasMax = maximum;
            max = maximumValue;
            CultureInfo nextCulture = inputCulture ?? CultureInfo.CurrentCulture;
            if (!initialized || kind != inputKind) {
                initialized = true;
                kind = inputKind;
                culture = nextCulture;
                IsEditing = false;
                pending.Clear();
                AdoptModel(modelValue);
                return;
            }

            bool cultureChanged = !ReferenceEquals(culture, nextCulture);
            if (!IsEditing) culture = nextCulture;
            if (observedModel.Equals(modelValue)) {
                // Several callbacks may flush together and return to the
                // original model (for example Edit then Escape). They are no
                // longer pending once the live value already matches it.
                if (Value.Equals(modelValue)) pending.Clear();
                // Deferred OnGUI callbacks have not reached the model yet.
                // Keep both the live numeric value and incomplete edit text.
                if (cultureChanged && !IsEditing) Text = NumericInputParser.Format(Value, kind, culture);
                return;
            }

            double incoming = NumericInputParser.Normalize(modelValue, kind);
            for (int i = pending.Count - 1; i >= 0; i--) {
                if (!pending[i].Equals(incoming)) continue;
                observedModel = modelValue;
                pending.RemoveRange(0, i + 1);
                if (!IsEditing) Text = NumericInputParser.Format(Value, kind, culture);
                return;
            }

            // A real external change takes ownership, including Escape's
            // baseline, so cancellation cannot overwrite a newer model.
            pending.Clear();
            culture = nextCulture;
            AdoptModel(modelValue);
        }

        public void BeginEdit() {
            if (IsEditing) return;
            editStartValue = Value;
            IsEditing = true;
        }

        public bool Edit(string text, out double next) {
            BeginEdit();
            Text = text ?? string.Empty;
            double parsed;
            if (NumericInputParser.Parse(Text, kind, culture, false, out parsed) != NumericParseStatus.Valid) {
                next = Value;
                return false;
            }
            return Publish(NumericInputParser.Clamp(parsed, kind, hasMin, min, hasMax, max), out next);
        }

        public bool Commit(out double next) {
            double parsed;
            bool changed = false;
            next = Value;
            if (NumericInputParser.Parse(Text, kind, culture, true, out parsed) == NumericParseStatus.Valid)
                changed = Publish(NumericInputParser.Clamp(parsed, kind, hasMin, min, hasMax, max), out next);
            IsEditing = false;
            Text = NumericInputParser.Format(Value, kind, culture);
            return changed;
        }

        public bool Cancel(out double next) {
            next = Value;
            if (!IsEditing) return false;
            bool changed = Publish(editStartValue, out next);
            IsEditing = false;
            Text = NumericInputParser.Format(Value, kind, culture);
            return changed;
        }

        // Used when editing becomes unavailable. Keep the actual model, not
        // the unfinished buffer or a predicted value awaiting its callback.
        public void Abandon(double modelValue) {
            pending.Clear();
            IsEditing = false;
            AdoptModel(modelValue);
        }

        private bool Publish(double value, out double next) {
            next = value;
            if (Value.Equals(value)) return false;
            Value = value;
            // Reuse the list between edits. Keeping recent acknowledgements
            // allows earlier queued callbacks to arrive before the latest one.
            if (pending.Count == 64) pending.RemoveAt(0);
            pending.Add(value);
            return true;
        }

        private void AdoptModel(double modelValue) {
            observedModel = modelValue;
            Value = NumericInputParser.Normalize(modelValue, kind);
            editStartValue = Value;
            Text = NumericInputParser.Format(Value, kind, culture);
        }
    }
}
