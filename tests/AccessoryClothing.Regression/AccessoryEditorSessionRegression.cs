using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessoryBatchManager;
using ChaCustom;

internal static class AccessoryEditorSessionRegression {
    internal static void Run(Action<string, Action> run) {
        run("ABM display refresh preserves a valid queued production operation", DisplayRefresh);
        run("ABM queued commands reject changed sources, destinations and selection", ChangedCommandTargets);
        run("ABM confirmation rejects a replacement working coordinate at the same index", ChangedWorkingCoordinate);
        run("ABM closing and reopening ends old command and confirmation lifetimes", EndSession);
        run("ABM actual card adapter distinguishes same-name same-ID source cards", SameNameCards);
        run("ABM card reset releases cached data and invalidates same-frame availability", ResetCardSource);
    }

    private sealed class Fixture {
        internal readonly ChaControl Character = new ChaControl();
        internal readonly AccessoryEditorSession Session = new AccessoryEditorSession();
        internal readonly HashSet<int> Batch = new HashSet<int> { 0 };
        internal readonly HashSet<int> Sources = new HashSet<int> { 0 };
        internal readonly HashSet<int> Destinations = new HashSet<int> { 0 };
        internal int Page = 1, SourceIndex, DestinationIndex = 1, Options;
        internal bool SourceClothing, DestinationClothing, Reverse;
        internal ChaFileCoordinate Card;
        internal Fixture() {
            Character.nowCoordinate.accessory.parts[0].type = 121;
            Character.nowCoordinate.accessory.parts[0].id = 310;
            Character.nowCoordinate.accessory.parts[1].type = 121;
            Character.nowCoordinate.accessory.parts[1].id = 311;
            Character.chaFile.coordinate[2].accessory.parts[0].type = 121;
            Character.chaFile.coordinate[2].accessory.parts[0].id = 320;
            Refresh();
        }

        internal AccessoryEditorTarget Capture() {
            bool copy = Page == 1;
            return new AccessoryEditorTarget(Character, Page, copy ? SourceIndex : -1, copy ? DestinationIndex : -1,
                copy && Card != null, copy && SourceClothing, copy && DestinationClothing, copy && Reverse,
                copy ? Card ?? AccessoryOperations.GetCoordinate(Character, SourceIndex) : null,
                copy ? AccessoryOperations.GetCoordinate(Character, DestinationIndex) : null,
                Page == 0 ? Batch : null, copy ? Sources : null, copy ? Destinations : null, Options);
        }
        internal void Refresh() { Session.Refresh(Capture()); }
        internal void Copy() {
            if (Card != null)
                AccessoryOperations.CopyFromParts(Character, Card.accessory.parts, DestinationIndex, Sources.ToArray(), Destinations.ToArray(), Reverse);
            else AccessoryOperations.CopyOrMoveBetweenCoordinates(Character, SourceIndex, DestinationIndex, Sources.ToArray(), Destinations.ToArray(), Reverse, false);
        }
        internal void DestinationUntouched() {
            Check(Character.chaFile.coordinate[1].accessory.parts.All(part => part.type == 120), "destination 1 must remain unchanged");
        }
    }

    private static void DisplayRefresh() {
        Fixture f = new Fixture();
        AccessoryEditorTarget captured = f.Session.Target;
        int revision = f.Session.ModelRevision;
        Check(!f.Session.Refresh(f.Capture()), "a status-only refresh has the same command premises");
        Check(ReferenceEquals(captured, f.Session.Target) && f.Session.ModelRevision == revision,
            "equivalent models reuse their identity so Update cannot discard a valid queued click");
        f.Character.nowCoordinate.accessory.parts[0].addMove[0, 0].x = 2.5f;
        Check(!f.Session.Refresh(f.Capture()), "ordinary transform edits preserve the same item identity");
        Check(f.Session.TryInvoke(revision, captured, f.Capture(), f.Copy), "the old displayed model remains actionable");
        Check(f.Character.chaFile.coordinate[1].accessory.parts[0].id == 310, "the actual copy operation executes on its reviewed source");
        Check(f.Character.chaFile.coordinate[1].accessory.parts[0].addMove[0, 0].x == 2.5f, "accepted parameter edits are copied without a stale full-field clone");
    }

    private static void ChangedCommandTargets() {
        Action<Fixture>[] changes = {
            f => f.SourceIndex = 2,
            f => f.DestinationIndex = 2,
            f => { f.Sources.Clear(); f.Sources.Add(1); },
            f => { f.Destinations.Clear(); f.Destinations.Add(1); },
            f => f.SourceClothing = true,
            f => f.DestinationClothing = true,
            f => f.Reverse = true,
            f => f.Options = 1,
            f => f.Character.nowCoordinate.accessory.parts[0] = new ChaFileAccessory.PartsInfo { type = 121, id = 310 },
            f => f.Character.nowCoordinate.accessory.parts[0].id = 999,
            f => f.Character.nowCoordinate.accessory.parts[0].type = 122
        };
        foreach (Action<Fixture> change in changes) {
            Fixture f = new Fixture();
            AccessoryEditorTarget captured = f.Session.Target;
            int revision = f.Session.ModelRevision;
            int lifetime = f.Session.LifetimeRevision;
            change(f);
            Check(!f.Session.TryInvoke(revision, captured, f.Capture(), f.Copy), "changed live premises are rejected before the next UI poll");
            Check(!f.Session.TryConfirm(lifetime, captured, f.Capture(), f.Copy), "confirmation retains the reviewed mapping and options");
            Check(f.Session.Refresh(f.Capture()), "actual target change advances the model revision");
            Check(!f.Session.TryInvoke(revision, captured, f.Capture(), f.Copy), "refresh cannot retarget the old queued command");
            f.DestinationUntouched();
        }
        Fixture clothing = new Fixture { SourceClothing = true };
        clothing.Character.nowCoordinate.clothes.parts[0].id = 410;
        clothing.Refresh();
        AccessoryEditorTarget clothingTarget = clothing.Session.Target;
        int clothingRevision = clothing.Session.ModelRevision;
        clothing.Character.nowCoordinate.clothes.parts[0].id = 411;
        Check(!clothing.Session.TryInvoke(clothingRevision, clothingTarget, clothing.Capture(), () => {
            AccessoryOperations.CopyOrMoveClothingToAccessories(clothing.Character, clothing.Character.nowCoordinate.clothes.parts,
                0, 1, clothing.Sources.ToArray(), clothing.Destinations.ToArray(), false, false);
        }), "changing a selected clothing ID in place also invalidates the command");
        clothing.DestinationUntouched();
    }

    private static void ChangedWorkingCoordinate() {
        Fixture f = new Fixture { Page = 0 };
        f.Refresh();
        AccessoryEditorTarget captured = f.Session.Target;
        int lifetime = f.Session.LifetimeRevision;
        Check(!f.Session.Refresh(f.Capture()), "opening a confirmation changes display only");
        ChaFileCoordinate previous = f.Character.nowCoordinate;
        f.Character.nowCoordinate = new ChaFileCoordinate();
        f.Character.nowCoordinate.accessory.parts[0].type = 121;
        f.Character.nowCoordinate.accessory.parts[0].id = 999;
        Check(!f.Session.TryConfirm(lifetime, captured, f.Capture(), () => AccessoryOperations.RemoveSelected(f.Character, f.Batch)),
            "reloading a card into the same coordinate index invalidates a queued removal");
        Check(f.Character.nowCoordinate.accessory.parts[0].id == 999 && previous.accessory.parts[0].id == 310,
            "neither replacement nor original working data is removed");
        f.Refresh();
        captured = f.Session.Target;
        Check(f.Session.TryConfirm(lifetime, captured, f.Capture(), () => AccessoryOperations.RemoveSelected(f.Character, f.Batch)),
            "a newly reviewed confirmation still invokes the real removal");
        Check(f.Character.nowCoordinate.accessory.parts[0].type == 120, "new confirmation removes the explicitly reviewed slot");
    }

    private static void EndSession() {
        Fixture f = new Fixture();
        AccessoryEditorTarget captured = f.Session.Target;
        int revision = f.Session.ModelRevision, lifetime = f.Session.LifetimeRevision;
        f.Session.Clear();
        Check(f.Session.Target == null, "end of Maker session releases the target graph");
        Check(!f.Session.TryInvoke(revision, captured, f.Capture(), f.Copy) &&
            !f.Session.TryConfirm(lifetime, captured, f.Capture(), f.Copy), "closed editor rejects pending work");
        f.Refresh();
        Check(!f.Session.TryInvoke(revision, captured, f.Capture(), f.Copy) &&
            !f.Session.TryConfirm(lifetime, captured, f.Capture(), f.Copy), "reopening the same character cannot reactivate old work");
        f.DestinationUntouched();
    }

    private sealed class CardFixture : IDisposable {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "abm-cards-" + Guid.NewGuid().ToString("N"));
        private readonly object[] previousScene = UnityEngine.Object.SceneObjects.ToArray();
        private readonly Func<ChaFileCoordinate, string, bool> previousLoader = ChaFileCoordinate.FileLoader;
        private readonly int previousFrame = UnityEngine.Time.frameCount;
        internal readonly CoordinateCardSource Source = new CoordinateCardSource();
        internal readonly CustomFileListCtrl List = new CustomFileListCtrl();
        internal int Loads;

        internal CardFixture() {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "one.png"), "anonymous external game-loader fixture");
            File.WriteAllText(Path.Combine(directory, "two.png"), "anonymous external game-loader fixture");
            ChaFileCoordinate.FileLoader = (coordinate, path) => {
                Loads++;
                coordinate.coordinateName = "Same display name";
                coordinate.accessory.parts[0].type = 121;
                coordinate.accessory.parts[0].id = 42;
                coordinate.accessory.parts[0].addMove[0, 0].x = Path.GetFileName(path) == "one.png" ? 1f : 2f;
                return true;
            };
            UnityEngine.Object.SceneObjects.Clear();
            UnityEngine.Object.SceneObjects.Add(new CustomCoordinateFile(List, new CustomFileWindow()));
            Select("one.png");
        }
        internal void Select(string name) {
            List.Selected = new CustomFileInfoComponent { info = new CustomFileInfo {
                FullPath = Path.Combine(directory, name), FileName = name, name = "Same display name"
            } };
            UnityEngine.Time.frameCount++;
            Check(Source.TryRefresh(), "production card adapter loads the external game fixture");
        }
        public void Dispose() {
            Source.Reset();
            ChaFileCoordinate.FileLoader = previousLoader;
            UnityEngine.Object.SceneObjects.Clear();
            UnityEngine.Object.SceneObjects.AddRange(previousScene);
            UnityEngine.Time.frameCount = previousFrame;
            Directory.Delete(directory, true);
        }
    }

    private static void SameNameCards() {
        using (CardFixture cards = new CardFixture()) {
            Fixture f = new Fixture { Card = cards.Source.Coordinate };
            f.Refresh();
            AccessoryEditorTarget captured = f.Session.Target;
            int revision = f.Session.ModelRevision, lifetime = f.Session.LifetimeRevision;
            string name = cards.Source.DisplayName;
            ChaFileCoordinate first = cards.Source.Coordinate;
            cards.Select("two.png");
            Check(name == cards.Source.DisplayName && first.accessory.parts[0].id == cards.Source.Parts[0].id,
                "the replacement deliberately keeps the same display name and item IDs");
            f.Card = cards.Source.Coordinate;
            Check(!f.Session.TryInvoke(revision, captured, f.Capture(), f.Copy), "old rendered command cannot use the newly selected card");
            Check(!f.Session.TryConfirm(lifetime, captured, f.Capture(), f.Copy), "old confirmation cannot cross a source-card change");
            Check(f.Session.Refresh(f.Capture()), "card reference changes invalidate a same-name/same-ID model");
            f.DestinationUntouched();
            Check(f.Session.TryInvoke(f.Session.ModelRevision, f.Session.Target, f.Capture(), f.Copy), "explicit command from the new card remains valid");
            Check(f.Character.chaFile.coordinate[1].accessory.parts[0].addMove[0, 0].x == 2f,
                "the new approved command copies actual parameters from the correct card");
        }
    }

    private static void ResetCardSource() {
        using (CardFixture cards = new CardFixture()) {
            Check(cards.Source.TryRefresh() && cards.Loads == 1, "same-frame lookup reuses the normal card cache");
            cards.Source.Reset();
            Check(cards.Source.Coordinate == null && cards.Source.Parts == null && cards.Source.DisplayName == null,
                "Maker exit releases coordinate, array and filename state");
            Check(cards.Source.TryRefresh() && cards.Loads == 2, "reset cannot reuse stale same-frame availability on reentry");
            UnityEngine.Object.SceneObjects.Clear();
            UnityEngine.Time.frameCount++;
            Check(!cards.Source.TryRefresh() && cards.Source.Coordinate == null, "leaving the external Maker file window clears its cached card");
        }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
