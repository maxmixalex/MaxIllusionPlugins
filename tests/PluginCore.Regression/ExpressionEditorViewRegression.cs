using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using MaximumUI;
using ExpressionControl;
using EC = ExpressionControl.ExpressionControl;

internal static class ExpressionEditorViewRegression {
    internal static void Run(Action<string, Action> run) {
        run("Expression actual editor builds four native pages at minimum size", Pages);
        run("Expression actual face callbacks keep role and lock targets separate", FaceCallbacks);
        run("Expression stale face callbacks cannot change a removed character", RemovedCharacter);
        run("Expression actual program picker selects saved identity and clones editing state", ProgramSelection);
        run("Expression actual unit toolbar copies, moves and deletes independently", UnitToolbar);
        run("Expression actual preset list keeps large libraries virtual and searchable", PresetList);
        run("Expression preset cache follows reorder, rename and XML reload", CacheInvalidation);
        run("Expression actual name dialog validates, preserves typing and saves real XML", Naming);
        run("Expression cancelling a naming dialog never writes XML", CancelNaming);
        run("Expression failed reload disables writes on every actual editor page", ReloadFailure);
        run("Expression deletion clears all units, live roles and saved XML references", DeleteReferences);
        run("Expression missing preset deletion preserves other role identities", MissingDeletion);
        run("Expression actual scene page restores role programs and timers", SceneRoles);
    }

    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Find<T>(Widget root, string key) where T : Widget {
        if (root is T && Equals(root.Key, key)) return (T)root;
        for (int i = 0; i < root.Children.Count; i++) {
            T result = Find<T>(root.Children[i], key);
            if (result != null) return result;
        }
        return null;
    }
    private static Button Button(Widget root, string text) {
        var button = root as Button;
        if (button != null && button.Text == text) return button;
        for (int i = 0; i < root.Children.Count; i++) {
            Button result = Button(root.Children[i], text);
            if (result != null) return result;
        }
        return null;
    }
    private static int Count(Widget root) {
        int count = 1;
        Assert(!(root is DrawCanvas), "legacy custom rendering entered the native view");
        for (int i = 0; i < root.Children.Count; i++) count += Count(root.Children[i]);
        return count;
    }
    private static EC Host(bool character = true) {
        var host = new EC { xml = new XmlMgr(), currentProgram = new EC.Program(), hotkey = new ShortCutKeyMgr() };
        if (character) host.femaleList.Add(Female("First"));
        return host;
    }
    private static EC.FemaleData Female(string name) {
        var character = new ChaControl(); character.chaFile.parameter.fullname = name;
        return new EC.FemaleData { female = character, expression = new EC.Expression(character), expressionRealtime = new EC.Expression(character) };
    }
    private static BuildContext Context() { return new BuildContext { Dialogs = new DialogService() }; }
    private static Widget Page(EC.ExpressionEditorView view, BuildContext context, int page) {
        Find<Button>(view.Build(context), "page-" + page).OnClick();
        return view.Build(context);
    }
    private static void WithFile(Action<string> test) {
        string previous = Paths.ConfigPath;
        string directory = Path.Combine(Path.GetTempPath(), "MaxIllusion-Expression-View-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); Paths.ConfigPath = directory;
        try { test(Path.Combine(directory, "MaxMixAlex.ExpressionControl.presets.xml")); }
        finally { Paths.ConfigPath = previous; Directory.Delete(directory, true); }
    }

    private static void Pages() {
        foreach (bool character in new[] { false, true }) {
            var host = Host(character); host.currentProgram.units.Add(new EC.Program.Unit());
            var view = new EC.ExpressionEditorView(host); BuildContext context = Context();
            for (int page = 0; page < 4; page++) {
                Widget tree = Page(view, context, page);
                var window = (Window)tree;
                Assert(window.Closable && window.Resizable && window.Foldable, "window controls missing");
                Assert(Count(tree) < 160, "inactive pages were built eagerly");
                foreach (MuiRect size in new[] { new MuiRect(0, 0, 900, 540), new MuiRect(0, 0, 1080, 760) }) {
                    LayoutNode layout = new LayoutEngine().Layout(tree, size);
                    VisibleBounds(layout, size, false);
                }
            }
        }
    }
    private static void VisibleBounds(LayoutNode node, MuiRect viewport, bool scrolled) {
        if (!scrolled) {
            Assert(node.Rect.Width >= 0f && node.Rect.Height >= 0f &&
                node.Rect.X >= -0.1f && node.Rect.Y >= -0.1f &&
                node.Rect.Right <= viewport.Right + 0.1f && node.Rect.Bottom <= viewport.Bottom + 0.1f,
                "control outside window: " + node.Widget.GetType().Name + "/" + node.Widget.Key +
                " at " + node.Rect.X + "," + node.Rect.Y + "," + node.Rect.Width + "," + node.Rect.Height);
        }
        for (int i = 0; i < node.Children.Count; i++)
            VisibleBounds(node.Children[i], viewport, scrolled || node.Widget is ScrollView);
    }
    private static void FaceCallbacks() {
        var host = Host(); var first = host.femaleList[0]; var second = Female("Second"); host.femaleList.Add(second);
        var view = new EC.ExpressionEditorView(host); Widget tree = view.Build(Context());
        Find<SearchableComboBox<int>>(tree, "pattern-eyes").OnChange(4);
        Assert(first.LastPart == EC.PartType.eyes && first.LastValue == 4 && second.AppliedParts == 0, "pattern hit wrong face/part");
        Find<Checkbox>(tree, "eye-open-lock").OnChange(true);
        Find<InputFloat>(tree, "mouth-open-value").OnChange(0.65f);
        Assert(first.lockEyeOpen && !first.lockMouthOpen && first.LastPart == EC.PartType.mouth && first.LastValue == 0.65f, "locks/values crossed targets");
        Find<SearchableComboBox<EC.FemaleData>>(tree, "character").OnChange(second);
        Find<Slider>(tree, "eye-y").OnChange(0.2f);
        Assert(host.currentFemaleNum == 1 && first.LastAction == "eye-y" && second.LastAction == null, "queued callback silently switched its captured character");
        Find<SliderInt>(view.Build(Context()), "tear").OnChange(2);
        Assert(second.LastAction == "tear" && second.LastValue == 2, "new tree did not target selected character");
    }
    private static void RemovedCharacter() {
        var host = Host(); var female = host.femaleList[0]; var view = new EC.ExpressionEditorView(host);
        Widget tree = view.Build(Context()); host.femaleList.Clear();
        Find<SearchableComboBox<int>>(tree, "pattern-mouth").OnChange(3);
        Assert(female.AppliedParts == 0, "removed character received deferred action");
        Assert(!Find<Button>(view.Build(Context()), "expression-save").Enabled, "empty scene still enabled saving a missing face");
    }
    private static void ProgramSelection() {
        var host = Host(); var saved = new EC.Program { guid = "saved", name = "Program" };
        saved.units.Add(new EC.Program.Unit { idle = true }); host.xml.programs.Add(saved);
        host.currentProgram = saved.DeepClone(); host.femaleList[0].program = saved.DeepClone();
        var view = new EC.ExpressionEditorView(host); var context = Context();
        Assert(ReferenceEquals(Find<SearchableComboBox<EC.Program>>(view.Build(context), "running-program-choice").Value, saved), "running picker cannot highlight current saved identity");
        var choice = Find<SearchableComboBox<EC.Program>>(Page(view, context, 1), "program-choice");
        Assert(ReferenceEquals(choice.Value, saved), "editor clone cannot highlight saved option");
        choice.OnChange(saved); host.currentProgram.units[0].idle = false;
        Assert(saved.units[0].idle && host.selectedProgramUnit == 0, "selecting a program edits stored units directly");
    }
    private static void UnitToolbar() {
        var host = Host(false); var view = new EC.ExpressionEditorView(host); var context = Context();
        Widget tree = Page(view, context, 1);
        Assert(!Find<Button>(tree, "unit-delete").Enabled, "empty unit delete enabled");
        Find<Button>(tree, "unit-add").OnClick();
        host.currentProgram.units[0].expressionGuids.Add("first");
        Find<Button>(view.Build(context), "unit-copy-below").OnClick();
        Assert(host.currentProgram.units.Count == 2 && host.selectedProgramUnit == 1, "copy-below lost insertion selection");
        host.currentProgram.units[1].expressionGuids.Add("second");
        Assert(host.currentProgram.units[0].expressionGuids.Count == 1, "copy shares a mutable GUID list");
        Find<ArrowButton>(view.Build(context), "unit-up").OnClick();
        Assert(host.selectedProgramUnit == 0 && host.currentProgram.units[0].expressionGuids.Count == 2, "move moved the wrong unit");
        Find<Button>(view.Build(context), "unit-delete").OnClick();
        Assert(host.currentProgram.units.Count == 1 && host.selectedProgramUnit == 0, "delete left an invalid selected unit");
    }
    private static void PresetList() {
        var host = Host();
        for (int i = 0; i < 20000; i++) host.xml.expressions.Add(new EC.Expression { guid = "e" + i, name = "Preset " + i });
        var view = new EC.ExpressionEditorView(host); var context = Context(); Widget tree = view.Build(context);
        Assert(Count(tree) < 130, "preset library materialized every row");
        var list = Find<VirtualList>(tree, "expression-list");
        Assert(list.ItemCount == 20000, "virtual list omitted presets");
        ((Selectable)list.BuildItem(19999)).OnChange(true);
        Assert(ReferenceEquals(host.femaleList[0].AppliedExpression, host.xml.expressions[19999]), "row lost saved expression identity");
        // A face-value edit must rebuild only the fixed-size editor, not copy
        // and filter 20,000 preset references again while dragging a slider.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Find<Slider>(tree, "eye-y").OnChange(0.1f);
        view.Build(context);
        Assert(GC.GetAllocatedBytesForCurrentThread() - before < 150000,
            "ordinary face edit refiltered/materialized a large unchanged preset library");
        Find<SearchBox>(tree, "expression-search").OnChange("Preset 19999");
        list = Find<VirtualList>(view.Build(context), "expression-list");
        Assert(list.ItemCount == 1, "real search callback did not invalidate choices");
    }
    private static void Naming() {
        WithFile(delegate(string path) {
            var host = Host(); host.femaleList[0].expression.eyeOpen = 0.4f;
            var view = new EC.ExpressionEditorView(host); var context = Context();
            Find<Button>(view.Build(context), "expression-save").OnClick();
            Dialog dialog = context.Dialogs.Dialogs[0]; TextField input = Find<TextField>(dialog, "preset-name");
            Button(dialog, "保存").OnClick();
            Assert(context.Dialogs.HasOpenDialogs && !File.Exists(path), "empty name saved or closed prompt");
            input.OnChange("测试 表情"); view.Build(context);
            Assert(input.Value == "测试 表情" && ReferenceEquals(dialog, context.Dialogs.Dialogs[0]), "retained rebuild discarded typing");
            input.OnSubmit("测试 表情");
            XmlMgr saved = new XmlMgr(); saved.Load();
            Assert(!context.Dialogs.HasOpenDialogs && saved.expressions.Count == 1 && saved.expressions[0].name == "测试 表情" && saved.expressions[0].eyeOpen == 0.4f,
                "real naming/save callback did not preserve face values in XML");
        });
    }
    private static void CacheInvalidation() {
        WithFile(delegate(string path) {
            var host = Host();
            host.xml.expressions.Add(new EC.Expression { guid = "a", name = "Alpha" });
            host.xml.expressions.Add(new EC.Expression { guid = "b", name = "Bravo" });
            host.xml.expressions.Add(new EC.Expression { guid = "c", name = "Charlie" });
            var view = new EC.ExpressionEditorView(host); var context = Context();
            view.Build(context);
            Widget tree = Page(view, context, 1);
            Find<Checkbox>(tree, "sort-expressions").OnChange(true);
            var rows = Find<VirtualList>(view.Build(context), "unit-expression-list");
            ((ArrowButton)rows.BuildItem(1).Children[0]).OnClick();
            rows = Find<VirtualList>(view.Build(context), "unit-expression-list");
            Assert(((Label)rows.BuildItem(0).Children[2]).Text == "Bravo", "same-query cache did not follow in-place ordering");

            host.femaleList[0].expression = new EC.Expression(host.xml.expressions[0]);
            tree = Page(view, context, 0);
            Find<SearchBox>(tree, "expression-search").OnChange("Bravo");
            tree = view.Build(context); Assert(Find<VirtualList>(tree, "expression-list").ItemCount == 1, "initial filtered name missing");
            Find<Button>(tree, "expression-overwrite").OnClick();
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Renamed");
            Button(context.Dialogs.Dialogs[0], "保存").OnClick();
            Assert(Find<VirtualList>(view.Build(context), "expression-list").ItemCount == 0, "rename left a nonmatching preset in same-query cache");

            Find<SearchBox>(view.Build(context), "expression-search").OnChange(string.Empty);
            Assert(Find<VirtualList>(view.Build(context), "expression-list").ItemCount == 3, "cleared search did not restore library");
            var replacement = new XmlMgr(); replacement.expressions.Add(new EC.Expression { guid = "external", name = "External" }); replacement.Save(false);
            host.TestReload();
            var reloaded = Find<VirtualList>(view.Build(context), "expression-list");
            Assert(reloaded.ItemCount == 1 && ((Selectable)reloaded.BuildItem(0)).Text == "External", "same-query cache retained pre-reload data");
        });
    }
    private static void CancelNaming() {
        WithFile(delegate(string path) {
            var host = Host(); var view = new EC.ExpressionEditorView(host); var context = Context();
            Find<Button>(view.Build(context), "expression-save").OnClick();
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Cancelled");
            context.Dialogs.CloseTop();
            Assert(!File.Exists(path) && host.xml.expressions.Count == 0, "cancel wrote a preset");
        });
    }
    private static void ReloadFailure() {
        WithFile(delegate(string path) {
            var host = Host(); host.currentProgram.units.Add(new EC.Program.Unit());
            host.xml.expressions.Add(new EC.Expression { guid = "face", name = "Face" });
            host.xml.programs.Add(new EC.Program { guid = "program", name = "Program" });
            host.xml.scenePresets.Add(new EC.ScenePreset { guid = "scene", name = "Scene" });
            host.femaleList[0].expression.guid = "face"; host.currentProgram.guid = "program"; host.selectedScenePreset = 0;
            host.xml.Save(false); string valid = File.ReadAllText(path); File.WriteAllText(path, "<broken>");
            try { host.TestReload(); throw new Exception("expected invalid XML"); } catch (InvalidOperationException) { }
            var view = new EC.ExpressionEditorView(host); var context = Context();
            string[][] keys = {
                new[] { "expression-save", "expression-overwrite", "expression-delete" },
                new[] { "program-save", "program-overwrite", "program-delete" },
                new[] { "scene-save", "scene-overwrite", "scene-delete" },
                new[] { "shortcut-save" }
            };
            for (int page = 0; page < keys.Length; page++) {
                Widget tree = Page(view, context, page);
                foreach (string key in keys[page]) Assert(!Find<Button>(tree, key).Enabled, "failed reload still enables " + key);
            }
            Assert(host.xml.expressions.Count == 1 && File.ReadAllText(path) == "<broken>", "failed reload discarded old library or rewrote bad source");
            File.WriteAllText(path, valid); host.TestReload();
            Assert(Find<Button>(Page(view, context, 0), "expression-save").Enabled, "repair+reload did not restore writes");
        });
    }
    private static void DeleteReferences() {
        WithFile(delegate(string path) {
            var host = Host(); host.femaleList.Add(Female("Second"));
            host.xml.expressions.Add(new EC.Expression { guid = "gone", name = "Gone" });
            host.xml.expressions.Add(new EC.Expression { guid = "keep", name = "Keep" });
            var program = new EC.Program { guid = "p" };
            for (int i = 0; i < 3; i++) program.units.Add(new EC.Program.Unit { expressionGuids = new List<string> { "gone", "keep", "gone" } });
            host.xml.programs.Add(program); host.currentProgram = program.DeepClone();
            foreach (var female in host.femaleList) { female.program = program.DeepClone(); female.expression.guid = female.expressionRealtime.guid = "gone"; }
            host.xml.Save(false); Assert(host.TestDeleteExpression("gone"), "existing expression was not deleted");
            XmlMgr saved = new XmlMgr(); saved.Load();
            foreach (var p in new[] { saved.programs[0], host.currentProgram, host.femaleList[0].program, host.femaleList[1].program })
                foreach (var unit in p.units) Assert(unit.expressionGuids.Count == 1 && unit.expressionGuids[0] == "keep", "stale/duplicate reference survived");
            foreach (var female in host.femaleList) Assert(female.expression.guid == string.Empty && female.expressionRealtime.guid == string.Empty, "live role kept deleted identity");
            Assert(saved.expressions.Count == 1 && saved.expressions[0].guid == "keep", "deletion changed unrelated preset");
        });
    }
    private static void MissingDeletion() {
        WithFile(delegate(string path) {
            var host = Host(); host.femaleList[0].expression.guid = "keep";
            Assert(!host.TestDeleteExpression("missing") && !host.TestDeleteExpression(string.Empty), "nonexistent ID reported as deleted");
            Assert(host.femaleList[0].expression.guid == "keep" && !File.Exists(path), "no-op deletion changed a role or file");
        });
    }
    private static void SceneRoles() {
        WithFile(delegate(string path) {
            var host = Host(); host.femaleList.Add(Female("Second"));
            var one = new EC.Program { guid = "one", name = "One" }; var two = new EC.Program { guid = "two", name = "Two" };
            host.xml.programs.AddRange(new[] { one, two }); host.femaleList[0].program = one; host.femaleList[1].program = two;
            host.femaleList[0].programTimer = 4; host.femaleList[1].programTimer = 8;
            var view = new EC.ExpressionEditorView(host); var context = Context();
            Find<Button>(Page(view, context, 2), "scene-save").OnClick();
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Both roles");
            Button(context.Dialogs.Dialogs[0], "保存").OnClick();
            host.femaleList[0].program = two; host.femaleList[1].program = one;
            host.femaleList[0].programTimer = host.femaleList[1].programTimer = 1;
            Find<Button>(view.Build(context), "scene-apply").OnClick();
            Assert(host.femaleList[0].program == one && host.femaleList[1].program == two &&
                host.femaleList[0].programTimer == 4 && host.femaleList[1].programTimer == 8 && host.TimerResets == 2,
                "scene application lost per-role mapping or interval");
            var saved = new XmlMgr(); saved.Load();
            Assert(saved.scenePresets[0].roles[0].role == "F1" && saved.scenePresets[0].roles[1].role == "F2", "scene save lost ordered role identities");
        });
    }
}
