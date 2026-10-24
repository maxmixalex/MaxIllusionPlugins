using System;
using System.Reflection;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class CoordinateTests {
        internal const int TestCount = 9;

        internal static void Run() {
            ResetRestoresRootAndScreenBounds();
            ScrollIncludesTheContentOrigin();
            NestedGroupsAndScrollsIntersectEveryClip();
            EmptyAndOffscreenClipsNeverRevive();
            LegacyScrollOffsetsRoundToEven();
            ContentViewportMatchesLegacyScrollbarPasses();
            ClippedScreenQueriesContainOnlyVisiblePixels();
            LocalHitTestsRespectRectAndEveryAncestor();
            ReusedCoordinateFramesAllocateNothing();
        }

        private static void ResetRestoresRootAndScreenBounds() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(800f, 600f);
            RectEqual(coordinates.ToScreen(new MuiRect(10f, 20f, 30f, 40f)), 10f, 20f, 30f, 40f, "Root coordinates are already screen coordinates.");
            Assert(coordinates.ContainsScreenPoint(new MuiVec2(0f, 0f)) && !coordinates.ContainsScreenPoint(new MuiVec2(800f, 600f)), "Screen bounds use exclusive right/bottom edges.");
            coordinates.PushGroup(new MuiRect(100f, 80f, 300f, 200f));
            coordinates.Reset(320f, 180f);
            Assert(coordinates.Depth == 0 && coordinates.Origin.X == 0f && coordinates.Origin.Y == 0f, "Reset must discard unbalanced previous-event frames.");
            RectEqual(coordinates.Clip, 0f, 0f, 320f, 180f, "Resolution changes must update the root clip.");
            bool rejected = false;
            try { coordinates.Pop(); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "A mismatched pop must be reported instead of corrupting the next group.");
        }

        private static void ScrollIncludesTheContentOrigin() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(1000f, 800f);
            coordinates.PushScroll(new MuiRect(100f, 80f, 200f, 120f), new MuiVec2(15f, 30f), new MuiRect(7f, 9f, 800f, 900f));
            RectEqual(coordinates.ToScreen(new MuiRect(27f, 49f, 60f, 20f)), 105f, 90f, 60f, 20f, "Popup anchors must include viewport translation and both scroll/content offsets.");
            RectEqual(coordinates.Clip, 100f, 80f, 200f, 120f, "Content clipping stays at the viewport, not the translated content origin.");
            MuiVec2 contentStart = coordinates.ToScreen(new MuiVec2(7f, 9f));
            Assert(contentStart.X == 85f && contentStart.Y == 50f, "The content start moves above/left of the viewport by its scroll distance.");
            Assert(coordinates.IsVisible(new MuiRect(27f, 49f, 60f, 20f)) && !coordinates.IsVisible(new MuiRect(0f, 0f, 10f, 10f)), "Offscreen controls must not contribute popup/input hit regions.");
        }

        private static void NestedGroupsAndScrollsIntersectEveryClip() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(640f, 480f);
            coordinates.PushGroup(new MuiRect(100f, 50f, 180f, 100f));
            coordinates.PushScroll(new MuiRect(-20f, 10f, 160f, 90f), new MuiVec2(40f, 25f), new MuiRect(-10f, 5f, 1000f, 1000f));
            RectEqual(coordinates.Clip, 100f, 60f, 140f, 90f, "A negative viewport offset must still intersect its parent group.");
            Assert(coordinates.Origin.X == 50f && coordinates.Origin.Y == 30f, "Nested translation must use the original group origin before clipping.");
            coordinates.PushGroup(new MuiRect(60f, 45f, 200f, 200f));
            RectEqual(coordinates.Clip, 110f, 75f, 130f, 75f, "A descendant cannot escape any ancestor clip.");
            RectEqual(coordinates.ToScreen(new MuiRect(-15f, -10f, 30f, 30f)), 95f, 65f, 30f, 30f, "Negative local geometry must retain its true unclipped coordinates.");
            Assert(coordinates.IsVisible(new MuiRect(-15f, -10f, 30f, 30f)), "Partially clipped controls remain visible.");
            coordinates.Pop();
            RectEqual(coordinates.Clip, 100f, 60f, 140f, 90f, "Pop restores the exact scroll clip.");
            coordinates.Pop();
            RectEqual(coordinates.Clip, 100f, 50f, 180f, 100f, "Pop restores the exact parent group.");
            coordinates.Pop();
            Assert(coordinates.Depth == 0 && coordinates.Origin.X == 0f && coordinates.Origin.Y == 0f, "Balanced groups return to screen coordinates.");
        }

        private static void EmptyAndOffscreenClipsNeverRevive() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(100f, 100f);
            coordinates.PushGroup(new MuiRect(200f, 200f, 20f, 20f));
            coordinates.PushGroup(new MuiRect(-200f, -200f, 100f, 100f));
            Assert(!coordinates.IsVisible(new MuiRect(0f, 0f, 100f, 100f)) && !coordinates.ContainsScreenPoint(new MuiVec2(1f, 1f)), "A child returning to screen cannot revive a fully clipped parent.");
            coordinates.Reset(100f, 100f);
            coordinates.PushGroup(new MuiRect(10f, 10f, 0f, 20f));
            Assert(!coordinates.IsVisible(new MuiRect(-100f, -100f, 1000f, 1000f)), "Zero-width viewports contain no pixels.");
            coordinates.Reset(100f, 100f);
            coordinates.PushGroup(new MuiRect(20f, 20f, 30f, 30f));
            Assert(!coordinates.IsVisible(new MuiRect(-10f, 0f, 10f, 10f)), "Touching a clip edge without overlap is not visible.");
            Assert(!coordinates.IsVisible(new MuiRect(0f, 0f, -10f, 10f)), "Negative dimensions are empty, not mirrored hit regions.");
        }

        private static void LegacyScrollOffsetsRoundToEven() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(800f, 600f);
            coordinates.PushScroll(new MuiRect(10.25f, 20.75f, 100f, 100f), new MuiVec2(0.25f, 0.75f), new MuiRect(0.25f, 0.75f, 400f, 400f));
            Assert(coordinates.Origin.X == 10.25f && coordinates.Origin.Y == 18.75f, "Legacy scroll offsets round the combined sum to even, leaving the viewport's fractional origin intact.");
            coordinates.Pop();
            coordinates.PushScroll(new MuiRect(0f, 0f, 100f, 100f), new MuiVec2(-1.25f, -1.75f), new MuiRect(-1.25f, -1.75f, 400f, 400f));
            Assert(coordinates.Origin.X == 2f && coordinates.Origin.Y == 4f, "Positive half-way offsets also use banker's rounding.");
        }

        private static void ContentViewportMatchesLegacyScrollbarPasses() {
            MuiRect viewport = new MuiRect(10f, 20f, 100f, 100f);
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 100f, 100f), false, false, 16f),
                10f, 20f, 100f, 100f, "Exactly fitting content must not create scrollbars.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 100f, 101f), false, false, 16f),
                10f, 20f, 84f, 84f, "A vertical scrollbar can make formerly fitting content need a horizontal scrollbar.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 101f, 100f), false, false, 16f),
                10f, 20f, 84f, 84f, "A horizontal scrollbar can make formerly fitting content need a vertical scrollbar.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 84f, 101f), false, false, 16f),
                10f, 20f, 84f, 100f, "Content equal to the narrowed width needs only the vertical scrollbar.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 101f, 84f), false, false, 16f),
                10f, 20f, 100f, 84f, "Content equal to the shortened height needs only the horizontal scrollbar.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 84f, 84f), true, false, 16f),
                10f, 20f, 100f, 84f, "Always-show horizontal applies even when both axes fit.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 84f, 84f), false, true, 16f),
                10f, 20f, 84f, 100f, "Always-show vertical applies even when both axes fit.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 1f, 1f), true, true, 16f),
                10f, 20f, 84f, 84f, "Both forced scrollbars reserve both strips.");
            RectEqual(ScrollGeometry.ContentViewport(new MuiRect(10f, 20f, 8f, 6f), new MuiRect(0f, 0f, 9f, 7f), false, false, 16f),
                10f, 20f, 0f, 0f, "A viewport smaller than its scrollbars has an empty content clip.");
            RectEqual(ScrollGeometry.ContentViewport(viewport, new MuiRect(0f, 0f, 200f, 200f), true, true, -1f),
                10f, 20f, 100f, 100f, "Invalid negative scrollbar sizes must not enlarge the viewport.");

            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(800f, 600f);
            MuiRect content = new MuiRect(0f, 0f, 100f, 101f);
            coordinates.PushScroll(ScrollGeometry.ContentViewport(viewport, content, false, false, 16f), new MuiVec2(0f, 0f), content);
            Assert(coordinates.ContainsScreenPoint(new MuiVec2(93f, 103f)) &&
                !coordinates.ContainsScreenPoint(new MuiVec2(94f, 103f)) &&
                !coordinates.ContainsScreenPoint(new MuiVec2(93f, 104f)), "Scrollbars must be excluded from descendant input regions.");
        }

        private static void ClippedScreenQueriesContainOnlyVisiblePixels() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(640f, 480f);
            RectEqual(coordinates.ClipToScreen(new MuiRect(-10f, -20f, 50f, 60f)), 0f, 0f, 40f, 40f,
                "Root query regions must be intersected with the screen.");
            coordinates.PushGroup(new MuiRect(100f, 50f, 180f, 100f));
            coordinates.PushScroll(new MuiRect(-20f, 10f, 160f, 90f), new MuiVec2(40f, 25f), new MuiRect(-10f, 5f, 1000f, 1000f));
            coordinates.PushGroup(new MuiRect(60f, 45f, 200f, 200f));
            RectEqual(coordinates.ClipToScreen(new MuiRect(-15f, -10f, 30f, 30f)), 110f, 75f, 15f, 20f,
                "Partially visible descendants must register only their screen-space intersection with every ancestor.");
            RectEqual(coordinates.ClipToScreen(new MuiRect(5f, 5f, 10f, 10f)), 115f, 80f, 10f, 10f,
                "A fully visible control retains its dimensions after translation.");
            RectEqual(coordinates.ClipToScreen(new MuiRect(-15f, 0f, 15f, 10f)), 0f, 0f, 0f, 0f,
                "A rectangle merely touching the clip edge has no input region.");

            MuiRect[] invalid = {
                new MuiRect(0f, 0f, 0f, 10f), new MuiRect(0f, 0f, 10f, -1f),
                new MuiRect(float.NaN, 0f, 10f, 10f), new MuiRect(0f, 0f, float.PositiveInfinity, 10f),
                new MuiRect(float.MaxValue, 0f, float.MaxValue, 10f)
            };
            for (int i = 0; i < invalid.Length; i++) {
                RectEqual(coordinates.ClipToScreen(invalid[i]), 0f, 0f, 0f, 0f,
                    "Empty, negative or non-finite regions must return a finite canonical empty rectangle.");
                Assert(!coordinates.IsVisible(invalid[i]), "Visibility and clipped query regions must agree for invalid geometry.");
            }
            coordinates.Reset(100f, 100f);
            coordinates.PushGroup(new MuiRect(200f, 200f, 20f, 20f));
            coordinates.PushGroup(new MuiRect(-200f, -200f, 100f, 100f));
            RectEqual(coordinates.ClipToScreen(new MuiRect(0f, 0f, 100f, 100f)), 0f, 0f, 0f, 0f,
                "A fully clipped ancestor must never produce a descendant query region.");
        }

        private static void LocalHitTestsRespectRectAndEveryAncestor() {
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(640f, 480f);
            coordinates.PushGroup(new MuiRect(100f, 50f, 180f, 100f));
            coordinates.PushScroll(new MuiRect(-20f, 10f, 160f, 90f), new MuiVec2(40f, 25f), new MuiRect(-10f, 5f, 1000f, 1000f));
            coordinates.PushGroup(new MuiRect(60f, 45f, 200f, 200f));
            MuiRect local = new MuiRect(-15f, -10f, 30f, 30f);
            Assert(local.Contains(new MuiVec2(-1f, -1f)) && !coordinates.ContainsLocalPoint(local, new MuiVec2(-1f, -1f)),
                "A point inside the local control but outside its ancestor clips must not capture input.");
            Assert(coordinates.ContainsLocalPoint(local, new MuiVec2(0f, 0f)) &&
                coordinates.ContainsLocalPoint(local, new MuiVec2(14f, 19f)), "Visible left/top edges and interior points remain interactive.");
            Assert(!coordinates.ContainsLocalPoint(local, new MuiVec2(15f, 0f)) &&
                !coordinates.ContainsLocalPoint(local, new MuiVec2(0f, 20f)), "The control's right and bottom edges are exclusive even inside the clip.");
            MuiRect large = new MuiRect(0f, 0f, 200f, 200f);
            Assert(!coordinates.ContainsLocalPoint(large, new MuiVec2(130f, 10f)) &&
                !coordinates.ContainsLocalPoint(large, new MuiVec2(10f, 75f)), "The ancestor clip's right and bottom edges are also exclusive.");
            Assert(!coordinates.ContainsLocalPoint(new MuiRect(0f, 0f, -1f, 10f), new MuiVec2(0f, 0f)) &&
                !coordinates.ContainsLocalPoint(new MuiRect(0f, 0f, float.PositiveInfinity, 10f), new MuiVec2(1f, 1f)) &&
                !coordinates.ContainsLocalPoint(local, new MuiVec2(float.NaN, 1f)), "Invalid regions and non-finite pointers cannot become input owners.");
            coordinates.Reset(100f, 100f);
            coordinates.PushGroup(new MuiRect(100f, 0f, 20f, 20f));
            Assert(!coordinates.ContainsLocalPoint(new MuiRect(0f, 0f, 20f, 20f), new MuiVec2(1f, 1f)),
                "A fully clipped control cannot acquire a pointer lease.");
        }

        private static void ReusedCoordinateFramesAllocateNothing() {
            // The test DLL targets net461; obtain the .NET 9 runner's allocation
            // counter without adding a modern API dependency to that assembly.
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method == null) throw new InvalidOperationException("Coordinate allocation regression requires the documented .NET 9 test runner.");
            Func<long> allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            GuiCoordinates coordinates = new GuiCoordinates();
            for (int i = 0; i < 100; i++) Exercise(coordinates, 32);
            allocated();
            long before = allocated();
            float checksum = 0f;
            for (int i = 0; i < 10000; i++) checksum += Exercise(coordinates, 32);
            long bytes = allocated() - before;
            Assert(checksum > 0f && bytes == 0, "Repeated warmed-up coordinate pushes, queries, pops and resets must allocate zero managed bytes; actual " + bytes + ".");
        }

        private static float Exercise(GuiCoordinates coordinates, int depth) {
            coordinates.Reset(1920f, 1080f);
            for (int i = 0; i < depth; i++) {
                coordinates.PushGroup(new MuiRect(2f, 2f, 1500f, 900f));
                MuiRect content = new MuiRect(0.25f, 0.25f, 2000f, 2000f);
                MuiRect viewport = ScrollGeometry.ContentViewport(new MuiRect(3f, 3f, 1400f, 800f), content, false, false, 16f);
                coordinates.PushScroll(viewport, new MuiVec2(1.25f, 2.75f), content);
            }
            MuiRect result = coordinates.ToScreen(new MuiRect(20f, 20f, 40f, 30f));
            MuiRect clipped = coordinates.ClipToScreen(new MuiRect(20f, 20f, 40f, 30f));
            bool visible = coordinates.IsVisible(new MuiRect(20f, 20f, 40f, 30f));
            bool contains = coordinates.ContainsScreenPoint(new MuiVec2(result.X + 1f, result.Y + 1f));
            bool localContains = coordinates.ContainsLocalPoint(new MuiRect(20f, 20f, 40f, 30f), new MuiVec2(21f, 21f));
            for (int i = 0; i < depth; i++) { coordinates.Pop(); coordinates.Pop(); }
            return result.X + result.Y + clipped.Width + (visible ? 1f : 0f) + (contains ? 1f : 0f) + (localContains ? 1f : 0f);
        }

        private static void RectEqual(MuiRect actual, float x, float y, float width, float height, string message) {
            Assert(actual.X == x && actual.Y == y && actual.Width == width && actual.Height == height, message);
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new Exception(message);
        }
    }
}
