using System;
using System.Collections.Generic;

namespace MaximumUI {
    public static class MUI {
        public static readonly Widget Empty = EmptyWidget.Instance;

        public static Widget If(bool condition, Widget widget) {
            return condition ? widget : Empty;
        }

        public static Widget If(bool condition, Func<Widget> build) {
            if (!condition || build == null) return Empty;
            return build();
        }

        public static Widget If(bool condition, Func<Widget> thenBuild, Func<Widget> elseBuild) {
            if (condition) return thenBuild != null ? thenBuild() : Empty;
            return elseBuild != null ? elseBuild() : Empty;
        }

        public static Widget ForEach<T>(IEnumerable<T> items, Func<T, Widget> build) {
            Fragment fragment = new Fragment();
            if (items == null || build == null) return fragment;

            foreach (T item in items) {
                fragment.Add(build(item));
            }

            return fragment;
        }

        public static Widget ForEach<T>(IEnumerable<T> items, Func<T, int, Widget> build) {
            Fragment fragment = new Fragment();
            if (items == null || build == null) return fragment;

            int index = 0;
            foreach (T item in items) {
                fragment.Add(build(item, index));
                index++;
            }

            return fragment;
        }
    }
}
