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
        run("Expression restores independent main and program windows and original controls", Pages);
        run("Expression actual face callbacks keep role and lock targets separate", FaceCallbacks);
        run("Expression stale face callbacks cannot change a removed character", RemovedCharacter);
        run("Expression actual program picker selects saved identity and clones editing state", ProgramSelection);
        run("Expression actual unit toolbar copies, moves and deletes independently", UnitToolbar);
        run("Expression original preset picker retains large libraries without eager copies", PresetList);
        run("Expression preset cache follows reorder, rename and XML reload", CacheInvalidation);
        run("Expression actual name dialog validates, preserves typing and saves real XML", Naming);
        run("Expression cancelling a naming dialog never writes XML", CancelNaming);
        run("Expression failed reload disables writes in both original windows", ReloadFailure);
        run("Expression deletion clears all units, live roles and saved XML references", DeleteReferences);
        run("Expression missing preset deletion preserves other role identities", MissingDeletion);
        run("Expression main-window scene controls restore role programs and timers", SceneRoles);
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
            BuildContext context = Context();
            foreach (bool programWindow in new[] { false, true }) {
                Widget tree = new EC.ExpressionEditorView(host, programWindow).Build(context);
                var window = (Window)tree;
                Assert(window.Closable && window.Resizable && window.Foldable, "window controls missing");
                Assert(Count(tree) < 200 && Find<Button>(tree, "page-0") == null, "restoration retained migrated pages or built virtual rows eagerly");
                foreach (MuiRect size in new[] { new MuiRect(0, 0, window.MinWidth, window.MinHeight), new MuiRect(0, 0, window.Width, window.Height) }) {
                    LayoutNode layout = new LayoutEngine().Layout(tree, size);
                    VisibleBounds(layout, size, false);
                }
                if (programWindow) {
                    Assert(Find<Button>(tree, "program-save").Text == "新建保存" &&
                        Find<Button>(tree, "unit-add").Text == "添加" && Find<Button>(tree, "unit-copy-below").Text == "复制到下方" &&
                        Find<Button>(tree, "sort-expressions").Text == "表情排序", "program toolbar lost original labels");
                    Assert(Find<Checkbox>(tree, "apply-mouth").Text == "应用嘴巴开合" && Find<Checkbox>(tree, "ignore-kiss").Text == "接吻时禁用程序",
                        "original program options missing");
                    Assert(Find<Button>(tree, "scene-save") == null, "main-window controls were merged into the program editor");
                    Widget unit = Find<VirtualList>(tree, "unit-list").BuildItem(0);
                    Assert(Find<Checkbox>(unit, "unit:1:aibu") != null && Find<TextField>(unit, "unit:1:feeling-min") != null,
                        "unit list replaced editable condition panels with a master/detail selection step");
                } else {
                    Assert(Find<VirtualList>(tree, "unit-list") == null && Find<Button>(tree, "edit-program").Text == "编辑表情程序",
                        "main window no longer opens an independent program editor");
                    Find<Button>(tree, "edit-program").OnClick();
                    Assert(host.ProgramWindowToggleCount == 1, "edit button did not toggle the independent window");
                    if (character) {
                        Assert(Find<Button>(tree, "read-face").Text == "获取当前表情" && Find<Checkbox>(tree, "blink").Text == "不眨眼" &&
                            Find<Button>(tree, "expression-save").Text == "新建保存" && Find<Button>(tree, "scene-delete").Text == "删除选中",
                            "main editor changed original wording");
                        Assert(Find<ComboBox<EC.Expression>>(tree, "expression-choice") != null && Find<Button>(tree, "scene-save") != null &&
                            Find<Slider>(tree, "program-timer") != null, "original single-column main window lost a control group");
                    }
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
        Find<ComboBox<int>>(tree, "pattern-eyes").OnChange(4);
        Assert(first.LastPart == EC.PartType.eyes && first.LastValue == 4 && second.AppliedParts == 0, "pattern hit wrong face/part");
        Find<Checkbox>(tree, "eye-open-lock").OnChange(true);
        Find<Slider>(tree, "mouth-open").OnChange(0.65f);
        Assert(first.lockEyeOpen && !first.lockMouthOpen && first.LastPart == EC.PartType.mouth && first.LastValue == 0.65f, "locks/values crossed targets");
        Find<Checkbox>(tree, "blink").OnChange(true);
        Assert(first.LastAction == "blink" && first.LastValue == 0f, "original 不眨眼 toggle enabled blinking instead of disabling it");
        Find<ArrowButton>(tree, "character-next").OnClick();
        Find<Slider>(tree, "eye-y").OnChange(0.2f);
        Assert(host.currentFemaleNum == 1 && first.LastAction == "eye-y" && second.LastAction == null, "queued callback silently switched its captured character");
        Find<Slider>(view.Build(Context()), "tear").OnChange(2.8f);
        Assert(second.LastAction == "tear" && second.LastValue == 2, "new tree did not target selected character");
    }
    private static void RemovedCharacter() {
        var host = Host(); var female = host.femaleList[0]; var view = new EC.ExpressionEditorView(host);
        Widget tree = view.Build(Context()); host.femaleList.Clear();
        Find<ComboBox<int>>(tree, "pattern-mouth").OnChange(3);
        Assert(female.AppliedParts == 0, "removed character received deferred action");
        Assert(Find<Button>(view.Build(Context()), "expression-save") == null, "empty scene still presents saving a missing face");
    }
    private static void ProgramSelection() {
        var host = Host(); var saved = new EC.Program { guid = "saved", name = "Program" };
        saved.units.Add(new EC.Program.Unit { idle = true }); host.xml.programs.Add(saved);
        host.currentProgram = saved.DeepClone(); host.femaleList[0].program = saved.DeepClone();
        var view = new EC.ExpressionEditorView(host); var context = Context();
        Assert(ReferenceEquals(Find<ComboBox<EC.Program>>(view.Build(context), "running-program-choice").Value, saved), "running picker cannot highlight current saved identity");
        var choice = Find<ComboBox<EC.Program>>(new EC.ExpressionEditorView(host, true).Build(context), "program-choice");
        Assert(ReferenceEquals(choice.Value, saved), "editor clone cannot highlight saved option");
        choice.OnChange(saved); host.currentProgram.units[0].idle = false;
        Assert(saved.units[0].idle && host.selectedProgramUnit == 0, "selecting a program edits stored units directly");
    }
    private static void UnitToolbar() {
        var host = Host(false); var view = new EC.ExpressionEditorView(host, true); var context = Context();
        Widget tree = view.Build(context);
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
        Find<Button>(view.Build(context), "unit-add").OnClick();
        tree = view.Build(context);
        var units = Find<VirtualList>(tree, "unit-list");
        Widget firstRow = units.BuildItem(0), secondRow = units.BuildItem(1);
        Assert(!Find<Panel>(firstRow, "unit:1:panel").Selected && Find<Panel>(secondRow, "unit:2:panel").Selected,
            "selected program unit lost the original whole-panel highlight");
        LayoutNode rowLayout = new LayoutEngine().Layout(firstRow, new MuiRect(0, 0, 780, 204));
        Assert(rowLayout.Children[0].Rect.Height == 198f && rowLayout.Children[1].Rect.Height == 6f,
            "virtual row must retain a visible gap between independently editable unit panels");
        Find<Checkbox>(firstRow, "unit:1:morph").OnChange(true);
        Assert(host.currentProgram.units[0].morph && !host.currentProgram.units[1].morph,
            "editing an unselected visible unit changed the selected unit");
        host.selectedProgramUnit = 0;
        Find<Button>(tree, "unit-delete").OnClick();
        Assert(host.currentProgram.units.Count == 2, "queued toolbar action crossed a unit selection change");
        EC.Program oldProgram = host.currentProgram;
        host.currentProgram = new EC.Program();
        Find<Checkbox>(firstRow, "unit:1:morph").OnChange(false);
        Find<Button>(tree, "unit-delete").OnClick();
        Assert(oldProgram.units[0].morph && host.currentProgram.units.Count == 0, "stale unit controls modified a replaced program");
        host.currentProgram.units.Add(new EC.Program.Unit());
        Widget newProgramRow = Find<VirtualList>(view.Build(context), "unit-list").BuildItem(0);
        Assert(!Equals(firstRow.Key, newProgramRow.Key), "switching programs reused a previous numeric field state identity");
    }
    private static void PresetList() {
        var host = Host();
        for (int i = 0; i < 20000; i++) host.xml.expressions.Add(new EC.Expression { guid = "e" + i, name = "Preset " + i });
        var view = new EC.ExpressionEditorView(host); var context = Context(); Widget tree = view.Build(context);
        Assert(Count(tree) < 200, "preset library materialized every row");
        var choice = Find<ComboBox<EC.Expression>>(tree, "expression-choice");
        Assert(choice.Options.Count == 20000 && ReferenceEquals(choice.Options, host.xml.expressions), "preset picker copied or omitted the source library");
        choice.OnChange(choice.Options[19999]);
        Assert(ReferenceEquals(host.femaleList[0].AppliedExpression, host.xml.expressions[19999]), "row lost saved expression identity");
        // A face-value edit must rebuild only the fixed-size editor, not copy
        // and filter 20,000 preset references again while dragging a slider.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Find<Slider>(tree, "eye-y").OnChange(0.1f);
        view.Build(context);
        Assert(GC.GetAllocatedBytesForCurrentThread() - before < 150000,
            "ordinary face edit refiltered/materialized a large unchanged preset library");
        Assert(Find<SearchBox>(tree, "expression-search") == null, "restoration introduced a search-first flow absent from the original editor");
        host.femaleList[0].expression.guid = "e19999";
        Find<ArrowButton>(view.Build(context), "expression-next").OnClick();
        Assert(ReferenceEquals(host.femaleList[0].AppliedExpression, host.xml.expressions[0]), "original next arrow did not wrap to the first saved expression");
    }
    private static void Naming() {
        WithFile(delegate(string path) {
            var host = Host(); host.femaleList[0].expression.eyeOpen = 0.4f;
            var view = new EC.ExpressionEditorView(host); var context = Context();
            Find<Button>(view.Build(context), "expression-save").OnClick();
            Dialog dialog = context.Dialogs.Dialogs[0]; TextField input = Find<TextField>(dialog, "preset-name");
            Button(dialog, "确定").OnClick();
            Assert(context.Dialogs.HasOpenDialogs && !File.Exists(path), "empty name saved or closed prompt");
            input.OnChange("测试 表情"); view.Build(context);
            Assert(input.Value == "测试 表情" && ReferenceEquals(dialog, context.Dialogs.Dialogs[0]), "retained rebuild discarded typing");
            input.OnSubmit("测试 表情");
            XmlMgr saved = new XmlMgr(); saved.Load();
            Assert(!context.Dialogs.HasOpenDialogs && saved.expressions.Count == 1 && saved.expressions[0].name == "测试 表情" && saved.expressions[0].eyeOpen == 0.4f,
                "real naming/save callback did not preserve face values in XML");
            string savedDocument = File.ReadAllText(path);
            Find<Button>(view.Build(context), "expression-overwrite").OnClick();
            host.femaleList[0].expression.guid = "different-expression";
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Wrong target");
            Button(context.Dialogs.Dialogs[0], "确定").OnClick();
            Assert(context.Dialogs.HasOpenDialogs && File.ReadAllText(path) == savedDocument && host.xml.expressions.Count == 1,
                "overwrite dialog followed an expression identity changed after opening");
        });
    }
    private static void CacheInvalidation() {
        WithFile(delegate(string path) {
            var host = Host();
            host.xml.expressions.Add(new EC.Expression { guid = "a", name = "Alpha" });
            host.xml.expressions.Add(new EC.Expression { guid = "b", name = "Bravo" });
            host.xml.expressions.Add(new EC.Expression { guid = "c", name = "Charlie" });
            var view = new EC.ExpressionEditorView(host); var programView = new EC.ExpressionEditorView(host, true);
            var context = Context(); view.Build(context);
            Find<Button>(programView.Build(context), "sort-expressions").OnClick();
            var rows = Find<VirtualList>(programView.Build(context), "unit-expression-list");
            ((ArrowButton)rows.BuildItem(1).Children[0]).OnClick();
            rows = Find<VirtualList>(programView.Build(context), "unit-expression-list");
            Assert(((Label)rows.BuildItem(0).Children[2]).Text == "Bravo", "expression ordering did not follow in-place changes");

            host.femaleList[0].expression = new EC.Expression(host.xml.expressions[0]);
            Widget tree = view.Build(context);
            Find<Button>(tree, "expression-overwrite").OnClick();
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Renamed");
            Button(context.Dialogs.Dialogs[0], "确定").OnClick();
            var choice = Find<ComboBox<EC.Expression>>(view.Build(context), "expression-choice");
            Assert(choice.Options.Count == 3 && choice.Options[0].name == "Renamed" && choice.Value.name == "Renamed",
                "rename did not update the current preset and original picker together");

            var replacement = new XmlMgr(); replacement.expressions.Add(new EC.Expression { guid = "external", name = "External" }); replacement.Save(false);
            host.TestReload();
            choice = Find<ComboBox<EC.Expression>>(view.Build(context), "expression-choice");
            Assert(choice.Options.Count == 1 && choice.Options[0].name == "External", "reload retained pre-reload preset data");
            Assert(Find<VirtualList>(programView.Build(context), "unit-expression-list").ItemCount == 1,
                "independent program window retained the old expression inventory");
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
            var view = new EC.ExpressionEditorView(host); var context = Context();
            Find<Button>(view.Build(context), "expression-save").OnClick();
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Queued before failed reload");
            host.xml.Save(false); string valid = File.ReadAllText(path); File.WriteAllText(path, "<broken>");
            try { host.TestReload(); throw new Exception("expected invalid XML"); } catch (InvalidOperationException) { }
            Button(context.Dialogs.Dialogs[0], "确定").OnClick();
            Assert(host.xml.expressions.Count == 1 && context.Dialogs.HasOpenDialogs,
                "a previously opened save dialog mutated the library after XML reload failed");
            context.Dialogs.CloseTop();
            string[] mainKeys = { "expression-save", "expression-overwrite", "expression-delete", "scene-save", "scene-overwrite", "scene-delete" };
            Widget mainTree = view.Build(context);
            foreach (string key in mainKeys) Assert(!Find<Button>(mainTree, key).Enabled, "failed reload still enables " + key);
            Widget programTree = new EC.ExpressionEditorView(host, true).Build(context);
            foreach (string key in new[] { "program-save", "program-overwrite", "program-delete", "sort-expressions" })
                Assert(!Find<Button>(programTree, key).Enabled, "failed reload still enables " + key);
            Assert(host.xml.expressions.Count == 1 && File.ReadAllText(path) == "<broken>", "failed reload discarded old library or rewrote bad source");
            File.WriteAllText(path, valid); host.TestReload();
            Assert(Find<Button>(view.Build(context), "expression-save").Enabled, "repair+reload did not restore writes");
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
            Find<Button>(view.Build(context), "scene-save").OnClick();
            Find<TextField>(context.Dialogs.Dialogs[0], "preset-name").OnChange("Both roles");
            Button(context.Dialogs.Dialogs[0], "确定").OnClick();
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
