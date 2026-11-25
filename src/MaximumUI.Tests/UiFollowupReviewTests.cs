using System;
using System.Collections.Generic;
using MaximumUI;
using UnityEngine;

namespace MaximumUI.Tests {
    internal static class UiFollowupReviewTests {
        internal const int TestCount = 5;

        internal static void Run() {
            FailedChoiceFormattingKeepsThePublishedSnapshotAndRetries();
            FailedVirtualRowBuildCanBeRetried();
            VirtualRowsArrangeResizeAndEvictWithoutRebuilding();
            DeferredActionsIsolateErrorsAndKeepNestedWorkForTheNextFlush();
            AnUnselectedListStartsAtItsFirstItemOnDown();
        }

        private static void FailedChoiceFormattingKeepsThePublishedSnapshotAndRetries() {
            ChoiceCache cache = new ChoiceCache();
            object owner = new object();
            string[] values = { "Alpha", "Beta", "Gamma" };
            bool fail = false;
            Func<object, string> formatter = delegate(object value) {
                if (fail && (string)value == "Beta") throw new InvalidOperationException("formatter failed");
                return (string)value;
            };
            cache.Refresh(owner, values, formatter, "", 1, true);
            string[] complete = cache.Labels;
            fail = true;
            bool rejected = false;
            try { cache.Refresh(owner, values, formatter, "", 2, true); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "Formatting failures must be visible to the caller.");
            Assert(ReferenceEquals(cache.Labels, complete) && cache.Values.Count == 3, "A failed refresh must not publish half an option list.");
            fail = false;
            cache.Refresh(owner, values, formatter, "mm", 2, true);
            Assert(cache.Labels.Length == 1 && cache.Labels[0] == "Gamma" && (string)cache.Values[0] == "Gamma", "A retry after failure must build and filter a complete aligned snapshot.");
        }

        private static void FailedVirtualRowBuildCanBeRetried() {
            bool fail = true;
            VirtualList list = new VirtualList(10, 24f, delegate(int index) {
                if (fail) throw new InvalidOperationException("row builder failed");
                return new Label("Recovered row " + index);
            });
            VirtualListCache cache = new VirtualListCache();
            cache.BeginRange(list, 0, 3, 1, new LayoutEngine());
            bool rejected = false;
            try { cache.GetRow(0, new MuiRect(0f, 0f, 200f, 24f), "rows.0"); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && cache.Count == 0, "A throwing builder must not leave a permanently empty cached row.");
            fail = false;
            Assert(cache.GetRow(0, new MuiRect(0f, 0f, 200f, 24f), "rows.0") != null && cache.Count == 1,
                "The same visible row must be retried on the next GUI event.");
        }

        private static void VirtualRowsArrangeResizeAndEvictWithoutRebuilding() {
            int builds = 0;
            VirtualList list = new VirtualList(100000, 24f, delegate(int index) {
                builds++;
                return new VStack { new Label("Row " + index) };
            });
            LayoutEngine engine = new LayoutEngine();
            VirtualListCache cache = new VirtualListCache();
            cache.BeginRange(list, 10, 13, 7, engine);
            LayoutNode original = cache.GetRow(10, new MuiRect(0f, 240f, 200f, 24f), "rows.10");
            float childX = original.Children[0].Rect.X;
            LayoutNode moved = cache.GetRow(10, new MuiRect(25f, 240f, 200f, 24f), "rows.10");
            Assert(ReferenceEquals(original, moved) && builds == 1 && cache.LayoutCount == 1 && moved.Children[0].Rect.X == childX + 25f,
                "Moving a row must arrange its existing subtree without rebuilding or measuring.");
            cache.GetRow(10, new MuiRect(25f, 240f, 240f, 24f), "rows.10");
            Assert(builds == 1 && cache.LayoutCount == 2, "Width changes require measurement but not another user builder call.");
            cache.GetRow(11, new MuiRect(0f, 264f, 240f, 24f), "rows.11");
            cache.GetRow(12, new MuiRect(0f, 288f, 240f, 24f), "rows.12");
            cache.BeginRange(list, 12, 15, 7, engine);
            Assert(cache.Count == 1, "Rows outside the newly visible range must be evicted.");
            cache.GetRow(12, new MuiRect(0f, 288f, 240f, 24f), "rows.12");
            Assert(builds == 3, "An overlapping visible row must retain its widget.");
            cache.BeginRange(list, 12, 15, 8, engine);
            Assert(cache.Count == 0, "Content invalidation must release every old row.");
        }

        private static void DeferredActionsIsolateErrorsAndKeepNestedWorkForTheNextFlush() {
            DeferredActions queue = new DeferredActions();
            List<string> calls = new List<string>();
            queue.Add(delegate {
                calls.Add("first");
                queue.Add(delegate { calls.Add("nested"); });
                queue.Flush(delegate { throw new Exception("A nested flush must not run."); });
            });
            queue.Add(delegate { throw new InvalidOperationException("expected callback failure"); });
            queue.Add(delegate { calls.Add("last"); });
            int errors = 0;
            queue.Flush(delegate { errors++; });
            Assert(string.Join(",", calls.ToArray()) == "first,last" && errors == 1 && queue.Count == 1,
                "A callback exception must not discard siblings or execute newly queued work in the same flush.");
            queue.Flush(delegate { throw new Exception("The nested callback should succeed."); });
            Assert(string.Join(",", calls.ToArray()) == "first,last,nested" && queue.Count == 0,
                "Nested work must survive until the next flush and execute exactly once.");
        }

        private static void AnUnselectedListStartsAtItsFirstItemOnDown() {
            int next;
            Assert(KeyboardNavigation.MoveSelection(-1, 5, KeyCode.DownArrow, 3, out next) && next == 0,
                "Down from an absent selection must enter the first item instead of skipping to the second.");
            Assert(KeyboardNavigation.MoveSelection(0, 5, KeyCode.DownArrow, 3, out next) && next == 1,
                "Down from an actual first-item selection must still move to the second item.");
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new Exception(message);
        }
    }
}
