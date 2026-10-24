using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using ExpressionControl;
using JuiceControl;
using MaxIllusionPlugins.Common;
using UnityEngine;
using EC = ExpressionControl.ExpressionControl;

internal static class Program {
    private static int passed, failed;
    private static string temporary;

    private static int Main(string[] args) {
        if (args.Length != 0) return PresetFileValidation.Run(args);
        temporary = Path.Combine(Path.GetTempPath(), "MaxIllusionPlugins-core-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try {
            Run("Atomic XML round trip and backup", AtomicRoundTrip);
            Run("Failed write preserves old bytes and cleans temporary", FailedWritePreservesFile);
            Run("Failed serialization preserves old document", FailedSerializationPreservesFile);
            Run("Expression XML constructor has no filesystem side effects", ConstructorHasNoSideEffects);
            Run("Legacy expression XML retains fields and IDs", ExpressionLegacyRoundTrip);
            Run("Missing expression IDs and null entries normalize safely", ExpressionNormalization);
            Run("Malformed reload retains current expression data", FailedExpressionReload);
            Run("XML DTD is rejected without changing the source", RejectDtd);
            Run("Every program state matches only its selected flag", AllProgramStateFlags);
            Run("Empty and stale expression units do not throw", EmptyExpressionUnits);
            Run("Expression selection preserves unit weighting and part filter", ExpressionSelection);
            Run("Deleting a program resets the editor to a valid unit and program", DeleteProgramResetsSelection);
            Run("Deleting the last program leaves a clean editor placeholder", DeleteLastProgramResetsSelection);
            Run("Mouth pattern lock allows independent mouth openness changes", MouthPatternLockAllowsOpenChange);
            Run("Mouth openness lock restores game, blend and Studio state", MouthOpenLockRestoresState);
            Run("Mouth openness lock preserves ignore and active-slide exclusions", MouthOpenLockExclusions);
            Run("Empty shortcut disables an existing binding", EmptyShortcutDisables);
            Run("Shortcut parsing is independent of Turkish casing", ShortcutCulture);
            Run("Zero-duration and identical-value transitions finish", ImmediateTransitionsComplete);
            Run("Normal transition reaches its final value exactly once", NormalTransitionCompletes);
            Run("Character scan handles missing manager and invalid entries", CharacterScan);
            Run("Juice XML round trips Unicode, effects and role mapping", JuiceRoundTrip);
            Run("Juice XML normalizes null preset entries", JuiceNormalization);
            Run("Malformed Juice XML is reported and never reset on disk", JuiceMalformedIsPreserved);
            Run("Failed Juice path switch keeps old presets read-only until recovery", JuiceFailedPathSwitch);
            Run("Juice accepts a legacy nil root without rewriting it", XmlCompatibilityTests.JuiceNilRoot);
            Run("Expression accepts a nil root with its default shortcut", XmlCompatibilityTests.ExpressionNilRoot);
            Run("XML BOMs, UTF-16 and empty roots remain compatible", XmlCompatibilityTests.EncodingsAndEmptyRoots);
            Run("Empty, malformed and unsafe XML remains protected", XmlCompatibilityTests.InvalidDocumentsPreserveStore);
            Run("Failed Expression reload also blocks subsequent saves", XmlCompatibilityTests.FailedExpressionReloadBlocksSave);
            Run("XML paths containing Unicode and URI punctuation stay literal", XmlCompatibilityTests.LiteralFileNames);
            Run("XML diagnostics expose the inner parser cause and location", XmlCompatibilityTests.DiagnosticCause);
            ExpressionRuntimeRegression.Run(Run);
            Run("Absent optional plugin discovery is cached", MissingPluginDiscoveryCached);
            Run("Incompatible optional plugin discovery is cached", IncompatiblePluginDiscoveryCached);
            Run("Optional API overloads resolve by actual call signature", OverloadedPluginApi);
        } finally {
            Directory.Delete(temporary, true);
        }
        Console.WriteLine("Plugin core regressions: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test) {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + "\n" + ex); }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws(Action action) {
        bool threw = false;
        try { action(); } catch { threw = true; }
        Assert(threw, "Expected the operation to report its failure.");
    }
    private static string FilePath(string name) { return Path.Combine(temporary, name); }
    private static string ExpressionPath() { return Path.Combine(Paths.ConfigPath, "MaxMixAlex.ExpressionControl.presets.xml"); }
    private static void NewExpressionDirectory(string name) { Paths.ConfigPath = FilePath(name); Directory.CreateDirectory(Paths.ConfigPath); }

    private static void AtomicRoundTrip() {
        string path = FilePath("atomic.xml");
        AtomicXmlFile.Write(path, new Sample { Text = "原有预设" });
        string original = File.ReadAllText(path);
        AtomicXmlFile.Write(path, new Sample { Text = "新しい設定" }, path + ".bak");
        Assert(AtomicXmlFile.Read<Sample>(path).Text == "新しい設定", "Unicode round trip");
        Assert(File.ReadAllText(path + ".bak") == original, "Previous complete document is backed up");
        AtomicXmlFile.Write(path, new Sample { Text = "third" }, path + ".bak");
        Assert(AtomicXmlFile.Read<Sample>(path + ".bak").Text == "新しい設定", "Existing backup is replaced");
    }
    private static void JuiceFailedPathSwitch() {
        string first = FilePath("juice-first.xml");
        string second = FilePath("juice-second.xml");
        PresetStore store = new PresetStore();
        store.Load(first);
        store.Data.CharacterPresets.Add(new CharacterPreset { Name = "Keep available" });
        store.Save();
        string original = File.ReadAllText(first);
        PresetXml previous = store.Data;
        File.WriteAllText(second, "<JuiceControlPresets>");
        Throws(() => store.Load(second));
        Assert(!store.CanSave && ReferenceEquals(previous, store.Data), "Failed switch preserves usable memory but disables writes");
        Throws(store.Save);
        Assert(File.ReadAllText(first) == original && File.ReadAllText(second) == "<JuiceControlPresets>", "Neither old nor rejected destination may be overwritten");
        new PresetXml().Save(second);
        store.Load(second);
        Assert(store.CanSave && store.Path == second, "Successful reload restores the requested destination");
        store.Data.CharacterPresets.Add(new CharacterPreset { Name = "Recovered" });
        store.Save();
        Assert(PresetXml.Load(second).CharacterPresets[0].Name == "Recovered" && File.ReadAllText(first) == original, "Recovery writes only the new file");
        store.Invalidate();
        Throws(store.Save);
    }
    private static void FailedWritePreservesFile() {
        string path = FilePath("interrupted.xml");
        File.WriteAllText(path, "previous complete bytes");
        Throws(() => AtomicXmlFile.Write(path, (Action<Stream>)(stream => {
            byte[] fragment = Encoding.UTF8.GetBytes("partial new content");
            stream.Write(fragment, 0, fragment.Length);
            throw new IOException("Injected interrupted write");
        })));
        Assert(File.ReadAllText(path) == "previous complete bytes", "Failed writer must not truncate destination");
        Assert(Directory.GetFiles(temporary, ".interrupted.xml.*.tmp").Length == 0, "Temporary file cleanup");
    }
    private static void FailedSerializationPreservesFile() {
        string path = FilePath("serialization.xml");
        File.WriteAllText(path, "old");
        Throws(() => AtomicXmlFile.Write(path, new ThrowingSample()));
        Assert(File.ReadAllText(path) == "old", "Serializer failures must preserve destination");
        Assert(Directory.GetFiles(temporary, ".serialization.xml.*.tmp").Length == 0, "No leaked serialization temporary");
    }
    private static void ConstructorHasNoSideEffects() {
        Paths.ConfigPath = FilePath("constructor-only");
        XmlMgr model = new XmlMgr();
        Assert(!Directory.Exists(Paths.ConfigPath), "XmlSerializer constructors must not create files");
        model.Load();
        Assert(File.Exists(ExpressionPath()), "Explicit first load creates the default file");
    }
    private static void ExpressionLegacyRoundTrip() {
        NewExpressionDirectory("expression-legacy");
        File.WriteAllText(ExpressionPath(), "<root><KeyConfig><showGUI>Ctrl+L</showGUI></KeyConfig><Expressions><item guid=\"expr-1\" name=\"微笑\" eyeOpen=\"0.75\" /></Expressions><Programs><Program guid=\"program-1\" name=\"通常\"><unit idle=\"true\" morph=\"true\"><guid>expr-1</guid></unit></Program></Programs><ScenePresets><ScenePreset guid=\"scene-1\" name=\"二人\"><Role role=\"F1\" programGuid=\"program-1\" programTimer=\"7\" /></ScenePreset></ScenePresets></root>");
        XmlMgr model = new XmlMgr(); model.Load(); model.Save(true);
        XmlMgr read = new XmlMgr(); read.Load();
        Assert(read.keyConfig.showGUI == "Ctrl+L", "Legacy shortcut name");
        Assert(read.expressions[0].guid == "expr-1" && read.expressions[0].eyeOpen == 0.75f, "Expression attributes");
        Assert(read.programs[0].units[0].expressionGuids[0] == "expr-1", "Program references");
        Assert(read.scenePresets[0].roles[0].programGuid == "program-1", "Scene role references");
    }
    private static void ExpressionNormalization() {
        NewExpressionDirectory("expression-null");
        XmlMgr model = new XmlMgr {
            expressions = new List<EC.Expression> { null, new EC.Expression { name = "A" }, new EC.Expression { guid = null, name = "B" }, new EC.Expression("id", "first"), new EC.Expression("id", "duplicate") },
            programs = new List<EC.Program> { null, new EC.Program { units = new List<EC.Program.Unit> { null, new EC.Program.Unit { expressionGuids = null } } } },
            scenePresets = new List<EC.ScenePreset> { null, new EC.ScenePreset { roles = new List<EC.SceneRolePreset> { null } } }
        };
        model.Save(true);
        Assert(model.expressions.Count == 3, "Keep anonymous entries while removing duplicates by actual ID");
        Assert(model.expressions[0].guid != model.expressions[1].guid, "Assign distinct missing IDs");
        Assert(model.programs.Count == 1 && model.programs[0].units.Count == 1, "Null program/unit entries");
        Assert(model.programs[0].units[0].expressionGuids.Count == 0, "Null GUID collection");
        Assert(model.scenePresets.Count == 1 && model.scenePresets[0].roles.Count == 0, "Null scene/role entries");
        XmlMgr read = new XmlMgr(); read.Load();
        Assert(read.expressions[0].guid == model.expressions[0].guid, "Normalized IDs persist in the serialized document");
    }
    private static void FailedExpressionReload() {
        NewExpressionDirectory("expression-reload");
        XmlMgr model = new XmlMgr(); model.expressions.Add(new EC.Expression("saved", "keep")); model.Save(true);
        List<EC.Expression> previous = model.expressions;
        File.WriteAllText(ExpressionPath(), "<root><Expressions>");
        Throws(model.Load);
        Assert(ReferenceEquals(previous, model.expressions) && model.expressions[0].guid == "saved", "Failed reload is transactional");
        Assert(File.ReadAllText(ExpressionPath()) == "<root><Expressions>", "Malformed source remains available for repair");
    }
    private static void RejectDtd() {
        string path = FilePath("dtd.xml");
        string xml = "<!DOCTYPE Sample [<!ENTITY injected 'changed'>]><Sample><Text>&injected;</Text></Sample>";
        File.WriteAllText(path, xml);
        Throws(() => AtomicXmlFile.Read<Sample>(path));
        Assert(File.ReadAllText(path) == xml, "Reader does not rewrite rejected input");
    }
    private static void AllProgramStateFlags() {
        foreach (EC.FemaleState selected in Enum.GetValues(typeof(EC.FemaleState))) {
            EC.Program.Unit unit = new EC.Program.Unit();
            if (selected != EC.FemaleState.other) typeof(EC.Program.Unit).GetField(selected.ToString()).SetValue(unit, true);
            foreach (EC.FemaleState current in Enum.GetValues(typeof(EC.FemaleState)))
                Assert(ExpressionRules.MatchesState(unit, current) == (selected != EC.FemaleState.other && selected == current), "State mismatch: " + selected + "/" + current);
        }
    }
    private static void EmptyExpressionUnits() {
        List<EC.Program.Unit> units = new List<EC.Program.Unit> { null, new EC.Program.Unit { morph = true }, new EC.Program.Unit { morph = true, expressionGuids = null }, new EC.Program.Unit { morph = true, expressionGuids = new List<string> { "deleted" } } };
        string result = ExpressionRules.ChooseExpressionGuid(units, EC.ExpressionPart.morph, new List<EC.Expression>(), count => { throw new Exception("Random must not run for zero candidates"); });
        Assert(result == null, "No usable expression is a normal no-op");
    }
    private static void ExpressionSelection() {
        List<EC.Expression> expressions = new List<EC.Expression> { new EC.Expression("a", "A"), new EC.Expression("b", "B"), new EC.Expression("c", "C") };
        List<EC.Program.Unit> units = new List<EC.Program.Unit> {
            new EC.Program.Unit { tear = true, expressionGuids = new List<string> { "c" } },
            new EC.Program.Unit { morph = true, expressionGuids = new List<string> { "missing", "a", "b" } },
            new EC.Program.Unit { morph = true, expressionGuids = new List<string> { "c" } }
        };
        int call = 0;
        string result = ExpressionRules.ChooseExpressionGuid(units, EC.ExpressionPart.morph, expressions, count => {
            Assert(count == 2, "Two eligible units, then two usable expressions");
            return call++ == 0 ? 0 : 1;
        });
        Assert(result == "b" && call == 2, "Unit then expression selection");
    }
    private static bool DeleteProgram(EC editor) {
        MethodInfo method = typeof(EC).GetMethod("XML_DeleteProgram", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, "The production editor deletion method must be linked");
        return (bool)method.Invoke(editor, null);
    }
    private static EC.Program MakeProgram(string guid, int units) {
        EC.Program program = new EC.Program { guid = guid, name = guid };
        for (int i = 0; i < units; i++) program.units.Add(new EC.Program.Unit { idle = true });
        return program;
    }
    private static void DeleteProgramResetsSelection() {
        int[] expectedIndices = { 0, 0, 1 };
        string[] expectedGuids = { "program-1", "program-0", "program-1" };
        for (int removed = 0; removed < 3; removed++) {
            NewExpressionDirectory("delete-program-" + removed);
            XmlMgr model = new XmlMgr();
            for (int i = 0; i < 3; i++) model.programs.Add(MakeProgram("program-" + i, i == removed ? 3 : 1));
            string removedGuid = model.programs[removed].guid;
            model.scenePresets.Add(new EC.ScenePreset {
                guid = "scene", name = "scene",
                roles = new List<EC.SceneRolePreset> {
                    new EC.SceneRolePreset { role = "F1", programGuid = removedGuid },
                    new EC.SceneRolePreset { role = "F2", programGuid = expectedGuids[removed] }
                }
            });
            EC editor = new EC {
                xml = model, currentProgram = model.programs[removed].DeepClone(),
                currentProgramNum = removed, selectedProgramUnit = 2
            };
            Assert(DeleteProgram(editor), "Selected program is deleted");
            Assert(model.programs.Count == 2 && editor.currentProgram.guid == expectedGuids[removed], "Previous or first remaining program is selected");
            Assert(editor.currentProgramNum == expectedIndices[removed] && editor.selectedProgramUnit == 0, "Both editor indices follow the replacement program");
            Assert(editor.currentProgram.units[editor.selectedProgramUnit].idle, "The next GUI unit lookup must remain valid for a shorter program");
            Assert(!ReferenceEquals(editor.currentProgram, model.programs[editor.currentProgramNum]), "Editing uses a clone rather than changing the stored program");
            editor.currentProgram.units[0].idle = false;
            Assert(model.programs[editor.currentProgramNum].units[0].idle, "Cloned units remain independent");
            XmlMgr saved = new XmlMgr(); saved.Load();
            Assert(saved.programs.Count == 2 && saved.programs.TrueForAll(program => program.guid != removedGuid), "Deletion is actually saved");
            Assert(saved.scenePresets[0].roles[0].programGuid == string.Empty && saved.scenePresets[0].roles[1].programGuid == expectedGuids[removed], "Only references to the deleted program are cleared and persisted");
        }
    }
    private static void DeleteLastProgramResetsSelection() {
        NewExpressionDirectory("delete-last-program");
        XmlMgr model = new XmlMgr(); model.programs.Add(MakeProgram("only", 3));
        EC editor = new EC { xml = model, currentProgram = model.programs[0].DeepClone(), currentProgramNum = 0, selectedProgramUnit = 2 };
        Assert(DeleteProgram(editor), "Last program is deleted");
        Assert(editor.currentProgramNum == 0 && editor.selectedProgramUnit == 0, "Empty editor indices reset");
        Assert(editor.currentProgram.guid == string.Empty && editor.currentProgram.name == "- - -" && editor.currentProgram.units.Count == 0, "Empty editor uses the normal placeholder");
        XmlMgr saved = new XmlMgr(); saved.Load();
        Assert(saved.programs.Count == 0, "Empty program list is saved");
        string afterDeletion = File.ReadAllText(ExpressionPath());
        Assert(!DeleteProgram(editor) && File.ReadAllText(ExpressionPath()) == afterDeletion, "Deleting the placeholder is a no-op");
    }
    private static EC.FemaleData MouthState() {
        ChaControl character = new ChaControl { MouthOpen = 0.2f };
        return new EC.FemaleData {
            female = character, faceListCtrl = new FaceListCtrl(), ociFemale = new Studio.OCIChar(),
            expression = new EC.Expression(character), expressionRealtime = new EC.Expression(character)
        };
    }
    private static void MouthPatternLockAllowsOpenChange() {
        EC.FemaleData data = MouthState(); data.lockMouthPtn = true;
        data.female.MouthPattern = 3;
        data.female.MouthOpen = 0.8f;
        Assert(data.CheckExpression(true), "Native state changes are detected");
        Assert(data.female.MouthPattern == 0 && data.expressionRealtime.mouthPtn == 0, "Mouth pattern remains locked");
        Assert(data.female.MouthOpen == 0.8f && data.expressionRealtime.mouthOpen == 0.8f, "Independent mouth openness remains unlocked");
        Assert(data.female.MouthOpenWrites == 0 && data.faceListCtrl.blendMouth.Starts == 0, "Unlocked openness must not cancel the native blend");
    }
    private static void MouthOpenLockRestoresState() {
        bool previousStudio = EC.isStudio;
        try {
            foreach (bool studio in new[] { false, true }) {
                foreach (bool wholeExpression in new[] { false, true }) {
                    EC.isStudio = studio;
                    EC.FemaleData data = MouthState();
                    data.lockExpression = wholeExpression;
                    data.lockMouthOpen = !wholeExpression;
                    data.female.MouthOpen = 0.8f;
                    Assert(data.CheckExpression(true), "Native openness change is detected");
                    Assert(data.female.MouthOpen == 0.2f && data.female.MouthOpenWrites == 1 && data.expressionRealtime.mouthOpen == 0.2f, "Part or whole-expression lock restores openness");
                    GlobalMethod.FloatBlend blend = data.faceListCtrl.blendMouth;
                    Assert(blend.Starts == 1 && blend.LastFrom == 0.2f && blend.LastTo == 0.2f && blend.LastTime == 0f, "Native blend is stopped at the locked value");
                    Assert(data.ociFemale.MouthOpen == (studio ? 0.2f : 0f), "Studio value is synchronized only in Studio");
                    Assert(!data.CheckExpression(true) && data.female.MouthOpenWrites == 1, "Unchanged state is not repeatedly written");
                }
            }
        } finally { EC.isStudio = previousStudio; }
    }
    private static void MouthOpenLockExclusions() {
        for (int exclusion = 0; exclusion < 4; exclusion++) {
            EC.FemaleData data = MouthState(); data.lockMouthOpen = true;
            data.ignoreMouthOpen = exclusion == 0;
            data.ignoreMouthLoad = exclusion == 1;
            if (exclusion == 3) data.coMouthOpenSlide = new Coroutine();
            data.female.MouthOpen = 0.8f;
            bool changed = data.CheckExpression(exclusion != 2);
            Assert(data.female.MouthOpen == 0.8f && data.female.MouthOpenWrites == 0 && data.faceListCtrl.blendMouth.Starts == 0, "Ignored or animated openness must not be reset: " + exclusion);
            Assert(data.expressionRealtime.mouthOpen == (exclusion == 3 ? 0.2f : 0.8f) && changed == (exclusion != 3), "Active slide owns its snapshot; other exclusions still observe native changes: " + exclusion);
        }
    }
    private static void EmptyShortcutDisables() {
        Input.Pressed.Clear(); Input.Modifiers.Clear(); Input.Pressed.Add("l");
        ShortCutKeyMgr manager = new ShortCutKeyMgr();
        manager.Set("toggle", "L"); manager.Update();
        Assert(manager["toggle"].GetKeyState(), "Initial binding");
        manager.Set("toggle", "  "); manager.Update(); Input.StringQueries = 0;
        Assert(!manager["toggle"].GetKeyState() && !manager["toggle"].GetKeyState_Move(), "Blank binding must disable both paths");
        Assert(Input.StringQueries == 0, "No invalid empty key is sent to Unity");
    }
    private static void ShortcutCulture() {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Input.Pressed.Clear(); Input.Modifiers.Clear(); Input.Pressed.Add("i");
            ShortCutKeyMgr manager = new ShortCutKeyMgr(); manager.Set("toggle", " I "); manager.Update();
            Assert(manager["toggle"].GetKeyState(), "Invariant lowercase I and surrounding whitespace");
        } finally { CultureInfo.CurrentCulture = previous; }
    }
    private static void ImmediateTransitionsComplete() {
        foreach (float duration in new[] { 0f, -1f }) {
            int calls = 0;
            var iterator = Utl.CoSmoothUpdate((value, end) => { Assert(value == 9f && end, "Immediate final value"); calls++; }, 2f, 9f, duration);
            Assert(!iterator.MoveNext() && calls == 1, "Immediate transition completes once");
        }
        int same = 0;
        var identical = Utl.CoSmoothUpdate((value, end) => { Assert(value == 4f && end, "Identical final value"); same++; }, 4f, 4f, 1f);
        Assert(!identical.MoveNext() && same == 1, "Identical transition still notifies completion");
    }
    private static void NormalTransitionCompletes() {
        int completions = 0, steps = 0; float last = 0;
        Time.deltaTime = 0.02f;
        var iterator = Utl.CoSmoothUpdate((value, end) => { last = value; if (end) completions++; }, 0f, 1f, 0.2f);
        while (iterator.MoveNext()) Assert(++steps < 20, "Transition terminates");
        Assert(completions == 1 && last == 1f && steps > 0, "Exactly one completion at destination");
    }
    private static void CharacterScan() {
        Singleton<Manager.Character>.Instance = null;
        Assert(Utl.GetFemaleAll().Count == 0, "No manager during a scene transition");
        ChaControl alive = new ChaControl();
        Manager.Character manager = new Manager.Character { Female = new List<ChaControl> { null, alive, new ChaControl { hiPoly = false } }, Male = new List<ChaControl> { alive } };
        Singleton<Manager.Character>.Instance = manager;
        Assert(Utl.GetFemaleAll().Count == 1, "Ignore null, low-poly and duplicate characters");
        Assert(manager.Female.Count == 3 && manager.Male.Count == 1, "Do not mutate game-owned collections");
    }
    private static void JuiceRoundTrip() {
        string path = FilePath("juice.xml");
        CharaSkinState state = new CharaSkinState { TearLevel = 2, BlushLevel = 3 };
        state.SetSiruLevel(ChaFileDefine.SiruParts.SiruKao, 3); state.SetSkinEffectLevel("WetFace", 2);
        PresetXml presets = new PresetXml();
        presets.CharacterPresets.Add(new CharacterPreset { Name = "雨の日", State = state });
        presets.GroupPresets.Add(new GroupPreset { Name = "双人", Characters = new List<RolePreset> { new RolePreset { Role = "女1", DisplayName = "角色甲", State = state } } });
        presets.Save(path);
        PresetXml loaded = PresetXml.Load(path);
        Assert(loaded.FindCharacterPreset("雨の日").State.GetSiruLevel(ChaFileDefine.SiruParts.SiruKao) == 3, "Native effect XML");
        Assert(loaded.FindGroupPreset("双人").FindRole("女1").State.GetSkinEffectLevel("wetface") == 2, "Role and optional effect XML");
    }
    private static void JuiceNormalization() {
        string path = FilePath("juice-null.xml");
        File.WriteAllText(path, "<JuiceControlPresets xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><CharacterPresets><Preset xsi:nil=\"true\"/><Preset name=\"keep\"><State xsi:nil=\"true\"/></Preset></CharacterPresets><GroupPresets><Preset xsi:nil=\"true\"/><Preset name=\"team\"><Character xsi:nil=\"true\"/><Character role=\"F1\"/></Preset></GroupPresets></JuiceControlPresets>");
        PresetXml model = PresetXml.Load(path);
        Assert(model.CharacterPresets.Count == 1 && model.CharacterPresets[0].State != null, "Null preset/state handling");
        Assert(model.GroupPresets.Count == 1 && model.GroupPresets[0].Characters.Count == 1, "Null role handling");
    }
    private static void JuiceMalformedIsPreserved() {
        string path = FilePath("juice-bad.xml"); File.WriteAllText(path, "<JuiceControlPresets>");
        Throws(() => PresetXml.Load(path));
        Assert(File.ReadAllText(path) == "<JuiceControlPresets>", "Malformed presets must not silently turn into empty saved data");
    }
    private static void MissingPluginDiscoveryCached() {
        Chainloader.DiscoveryReads = 0;
        SkinEffectsBridge.Refresh(true);
        Assert(!SkinEffectsBridge.IsAvailable, "Optional plugin is absent in this process");
        for (int i = 0; i < 500; i++) SkinEffectsBridge.Refresh();
        Assert(Chainloader.DiscoveryReads == 1, "One scan across 500 cached misses");
        SkinEffectsBridge.Refresh(true);
        Assert(Chainloader.DiscoveryReads == 2, "Explicit refresh retries discovery");
    }
    private static void IncompatiblePluginDiscoveryCached() {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SkinEffectsIncompatibleFixture"), AssemblyBuilderAccess.Run);
        assembly.DefineDynamicModule("fixture").DefineType("KK_SkinEffects.SkinEffectsController", TypeAttributes.Public).CreateType();
        Chainloader.DiscoveryReads = 0; SkinEffectsBridge.Refresh(true);
        for (int i = 0; i < 500; i++) SkinEffectsBridge.Refresh();
        Assert(!SkinEffectsBridge.IsAvailable && SkinEffectsBridge.StatusLabel == "已安装但API不兼容", "Unsupported API status");
        Assert(SkinEffectsBridge.Effects.Count == 0 && Chainloader.DiscoveryReads == 1, "Unsupported API is cached without partial effects");
    }
    private static void OverloadedPluginApi() {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SkinEffectsOverloadFixture"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("fixture");
        var enumBuilder = module.DefineEnum("KK_SkinEffects.SkinEffectKind", TypeAttributes.Public, typeof(int));
        enumBuilder.DefineLiteral("WetFace", 0); Type kind = enumBuilder.CreateType();
        var controller = module.DefineType("KK_SkinEffects.SkinEffectsController", TypeAttributes.Public);
        ConstantMethod(controller, "GetEffectLevel", MethodAttributes.Public, new[] { kind }, 1);
        ConstantMethod(controller, "GetEffectLevel", MethodAttributes.Public, new[] { typeof(string) }, 2);
        var setter = controller.DefineMethod("SetEffectLevel", MethodAttributes.Public, typeof(void), new[] { kind, typeof(int), typeof(bool) });
        setter.GetILGenerator().Emit(OpCodes.Ret);
        Type controllerType = controller.CreateType();
        var loader = module.DefineType("KK_SkinEffects.TextureLoader", TypeAttributes.Public);
        ConstantMethod(loader, "GetTextureCount", MethodAttributes.Public | MethodAttributes.Static, new[] { kind }, 3);
        ConstantMethod(loader, "GetTextureCount", MethodAttributes.Public | MethodAttributes.Static, new[] { typeof(string) }, 4);
        loader.CreateType();
        Chainloader.PluginInfos.Add("Marco.SkinEffects", new PluginInfo { Instance = Activator.CreateInstance(controllerType), Metadata = new Metadata { GUID = "Marco.SkinEffects", Name = "SkinEffects" } });
        SkinEffectsBridge.Refresh(true);
        Assert(SkinEffectsBridge.IsAvailable && SkinEffectsBridge.Effects.Count == 1, "Exact API method signatures resolve despite overloads");
        Assert(SkinEffectsBridge.Effects[0].MaxLevel == 3, "Correct texture count overload");
    }
    private static void ConstantMethod(TypeBuilder type, string name, MethodAttributes flags, Type[] parameters, int value) {
        var method = type.DefineMethod(name, flags, typeof(int), parameters);
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldc_I4, value); il.Emit(OpCodes.Ret);
    }
}

public class Sample { public string Text { get; set; } }
public class ThrowingSample {
    public string Text { get { throw new InvalidOperationException("Injected serializer failure"); } set { } }
}
