using System;
using System.Collections.Generic;

namespace MaximumUI {
    internal interface IComboBoxWidget {
        string ComboLabel { get; }
        object ComboValue { get; }
        System.Collections.IEnumerable ComboOptions { get; }
        Delegate ComboOnChange { get; }
        float ComboLabelWidth { get; }
        int ComboMaxVisibleItems { get; }
        string GetComboText(object value);
    }

    internal interface ISearchableComboBoxWidget {
        string ComboFilterPlaceholder { get; }
    }

    public sealed class ComboBox<T> : Widget, IComboBoxWidget {
        public string Label;
        public T Value;
        public IEnumerable<T> Items;
        public IList<T> Options;
        public Func<T, string> Format;
        public Func<T, string> GetText;
        public Action<T> OnChange;
        public float LabelWidth;
        public int MaxVisibleItems;

        public ComboBox(string label, T value, IEnumerable<T> items, Func<T, string> format = null, Action<T> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Options = new List<T>();
            if (items != null) {
                foreach (T item in items) Options.Add(item);
            }
            Items = Options;
            GetText = format ?? DefaultGetText;
            Format = GetText;
            OnChange = onChange;
            LabelWidth = 0f;
            MaxVisibleItems = 8;
        }

        private static string DefaultGetText(T value) {
            return value == null ? string.Empty : value.ToString();
        }

        string IComboBoxWidget.ComboLabel {
            get { return Label; }
        }

        object IComboBoxWidget.ComboValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IComboBoxWidget.ComboOptions {
            get { return Items; }
        }

        Delegate IComboBoxWidget.ComboOnChange {
            get { return OnChange; }
        }

        float IComboBoxWidget.ComboLabelWidth {
            get { return LabelWidth; }
        }

        int IComboBoxWidget.ComboMaxVisibleItems {
            get { return MaxVisibleItems; }
        }

        string IComboBoxWidget.GetComboText(object value) {
            string text;
            if (value is T) {
                text = GetText((T)value);
                return text ?? string.Empty;
            }

            if (value == null) {
                text = GetText(default(T));
                return text ?? string.Empty;
            }

            return value.ToString();
        }
    }

    public sealed class SearchableComboBox<T> : Widget, IComboBoxWidget, ISearchableComboBoxWidget {
        public string Label;
        public T Value;
        public IEnumerable<T> Items;
        public IList<T> Options;
        public Func<T, string> Format;
        public Func<T, string> GetText;
        public Action<T> OnChange;
        public float LabelWidth;
        public int MaxVisibleItems;
        public string FilterPlaceholder;

        public SearchableComboBox(string label, T value, IEnumerable<T> items, Func<T, string> format = null, Action<T> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Options = new List<T>();
            if (items != null) {
                foreach (T item in items) Options.Add(item);
            }
            Items = Options;
            GetText = format ?? DefaultGetText;
            Format = GetText;
            OnChange = onChange;
            LabelWidth = 0f;
            MaxVisibleItems = 8;
            FilterPlaceholder = "Filter";
        }

        private static string DefaultGetText(T value) {
            return value == null ? string.Empty : value.ToString();
        }

        string IComboBoxWidget.ComboLabel {
            get { return Label; }
        }

        object IComboBoxWidget.ComboValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IComboBoxWidget.ComboOptions {
            get { return Items; }
        }

        Delegate IComboBoxWidget.ComboOnChange {
            get { return OnChange; }
        }

        float IComboBoxWidget.ComboLabelWidth {
            get { return LabelWidth; }
        }

        int IComboBoxWidget.ComboMaxVisibleItems {
            get { return MaxVisibleItems; }
        }

        string IComboBoxWidget.GetComboText(object value) {
            string text;
            if (value is T) {
                text = GetText((T)value);
                return text ?? string.Empty;
            }

            if (value == null) {
                text = GetText(default(T));
                return text ?? string.Empty;
            }

            return value.ToString();
        }

        string ISearchableComboBoxWidget.ComboFilterPlaceholder {
            get { return FilterPlaceholder; }
        }
    }
}
