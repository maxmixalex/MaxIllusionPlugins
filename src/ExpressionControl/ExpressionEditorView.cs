using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using MaximumUI;
using UnityEngine;
using Button = MaximumUI.Button;
using Slider = MaximumUI.Slider;

namespace ExpressionControl {
    public partial class ExpressionControl {
        // Two independent windows mirror the pre-migration UI. The main window
        // remains available while the program window is open.
        internal sealed class ExpressionEditorView : View {
            private readonly ExpressionControl owner;
            private readonly bool programWindow;
            private BuildContext context;
            private string status = string.Empty;
            private float nextPoll;
            private FemaleData observedFemale;
            private int observedCount = -1;
            private readonly float[] liveValues = new float[20];
            private readonly Dictionary<int, int[]> patternRanges = new Dictionary<int, int[]>();
            private readonly Dictionary<Program.Unit, int> unitIds = new Dictionary<Program.Unit, int>();
            private Program identityProgram;
            private int nextUnitId;
            private IList<Expression> resolvedPresetSource;
            private string resolvedPresetGuid;
            private int resolvedPresetCount;
            private Expression resolvedPreset;
            private const float Row = 28f;
            private const float UnitHeight = 204f;

            internal ExpressionEditorView(ExpressionControl owner, bool programWindow = false) {
                this.owner = owner;
                this.programWindow = programWindow;
            }

            internal void Invalidate() {
                MaximumUIRuntime.MarkDirty(this);
                owner.InvalidateMaximumUiViews();
            }

            public override void Update(BuildContext buildContext) {
                if (programWindow || !MaximumUIRuntime.IsVisible(MaximumUiMountId) || Time.unscaledTime < nextPoll) return;
                nextPoll = Time.unscaledTime + 0.2f;
                FemaleData female = CurrentFemale();
                bool changed = !ReferenceEquals(observedFemale, female) || observedCount != owner.femaleList.Count;
                observedFemale = female; observedCount = owner.femaleList.Count;
                if (female != null && female.expression != null) {
                    Expression e = female.expression;
                    changed |= Observe(0, e.eyebrowPtn); changed |= Observe(1, e.eyesPtn);
                    changed |= Observe(2, e.mouthPtn); changed |= Observe(3, e.eyeOpen);
                    changed |= Observe(4, e.mouthOpen); changed |= Observe(5, e.eyeY);
                    changed |= Observe(6, e.eyeSmall); changed |= Observe(7, e.tearLv);
                    changed |= Observe(8, e.hohoAka); changed |= Observe(9, e.hideHighlight ? 1f : 0f);
                    changed |= Observe(10, e.blink ? 1f : 0f);
                    changed |= Observe(11, (float)Math.Round(female.programTimerNow, 2));
                    changed |= Observe(12, female.programTimer); changed |= Observe(13, owner.GetCharacterOrgasmCount(female));
                    changed |= Observe(14, female.programValid ? 1f : 0f);
                }
                if (changed) MaximumUIRuntime.MarkDirty(this);
            }

            private bool Observe(int slot, float value) {
                if (liveValues[slot] == value) return false;
                liveValues[slot] = value; return true;
            }

            private FemaleData CurrentFemale() {
                int index = owner.currentFemaleNum;
                if (index < 0 || index >= owner.femaleList.Count) return null;
                FemaleData female = owner.femaleList[index];
                return female != null && female.female != null ? female : null;
            }

            private void Change(Action action, string success = null) {
                try { action(); status = string.Empty; }
                catch (Exception error) { status = ErrorText(error); Debug.LogException(error); }
                Invalidate();
            }

            private void ChangeFemale(FemaleData female, Action action) {
                if (female == null || !owner.femaleList.Contains(female) || female.female == null) {
                    status = "角色已离开当前场景。"; Invalidate(); return;
                }
                Change(delegate { owner.RunCharacterAction(female, action); });
            }

            public override Widget Build(BuildContext buildContext) {
                context = buildContext;
                if (owner.currentProgram == null) owner.currentProgram = new Program();
                VStack body = new VStack(6f).ExpandWidth().ExpandHeight();
                if (owner.presetReadError != null) {
                    body.Add(new HelpBox("XML 载入失败，保存已停止。\n" + PresetPath + "\n" + ErrorText(owner.presetReadError), HelpBoxType.Error).MinHeight(64f));
                    body.Add(new Button("修复文件后重新载入", delegate { Change(owner.ReloadMaximumUiPresets); resolvedPresetSource = null; }, "reload-error").Height(Row));
                }
                body.Add((programWindow ? BuildProgramEditor() : BuildCharacterEditor()).ExpandWidth().ExpandHeight());
                if (!string.IsNullOrEmpty(status)) body.Add(new Label(status) { WordWrap = true }.MinHeight(Row));
                return new Window("ExpressionControl " + ExpressionControlPlugin.Version,
                    programWindow ? 1040f : 480f, programWindow ? 760f : 900f,
                    programWindow ? "expression-program-window" : "expression-window",
                    programWindow ? 1000f : 400f, 520f, true, true,
                    delegate(bool visible) {
                        if (programWindow) owner.SetMaximumUiProgramVisible(visible);
                        else owner.SetMaximumUiVisible(visible);
                    }).Content(body);
            }

            private Widget BuildCharacterEditor() {
                FemaleData female = CurrentFemale();
                VStack main = new VStack(4f).ExpandWidth();
                if (female == null || female.expression == null) {
                    main.Add(new Label("没有角色").Height(Row));
                    main.Add(EditProgramButton());
                    return main;
                }
                Expression expression = female.expression;
                main.Add(new HStack(4f) {
                    new ArrowButton(ArrowDirection.Left, delegate { Change(owner.PrevFemale); }, "character-previous").Width(28f),
                    new ArrowButton(ArrowDirection.Right, delegate { Change(owner.NextFemale); }, "character-next").Width(28f),
                    new Label(female.fullName, "character").ExpandWidth()
                }.Height(Row));
                main.Add(new Separator());
                main.Add(new HStack(12f) {
                    new Button("获取当前表情", delegate { ChangeFemale(female, female.Update); }, "read-face").Width(160f),
                    new Checkbox("固定表情", female.lockExpression, v => ChangeFemale(female, delegate { female.lockExpression = v; }), "lock-all").ExpandWidth()
                }.Height(Row));
                main.Add(Pattern("眉", PartType.eyebrow, expression.eyebrowPtn, female.maxEyebrowPtn, owner.eyeBrowPtnNameDict,
                    female, female.lockEyebrowPtn, v => female.lockEyebrowPtn = v));
                main.Add(Pattern("眼", PartType.eyes, expression.eyesPtn, female.maxEyesPtn, owner.eyesPtnNameDict,
                    female, female.lockEyePtn, v => female.lockEyePtn = v));
                main.Add(ValueSlider("开合", "eye-open", expression.eyeOpen, 0f, 1f, female,
                    v => female.ChangeOpen(PartType.eyes, v), female.lockEyeOpen, v => female.lockEyeOpen = v));
                main.Add(ValueSlider("瞳孔↑", "eye-y", expression.eyeY, 0f, 0.5f, female,
                    female.ChangeEyeY, female.lockEyeY, v => female.lockEyeY = v));
                main.Add(ValueSlider("缩瞳", "eye-small", expression.eyeSmall, 0f, 1f, female,
                    female.ChangeEyeSmall, female.lockEyeSmall, v => female.lockEyeSmall = v));
                main.Add(ToggleWithLock("隐藏高光", "highlight", expression.hideHighlight, female,
                    female.ChangeHighlight, female.lockHighlight, v => female.lockHighlight = v));
                main.Add(ToggleWithLock("不眨眼", "blink", !expression.blink, female,
                    v => female.ChangeBlink(!v), female.lockBlink, v => female.lockBlink = v));
                if (female.female.fbsCtrl != null && female.female.fbsCtrl.BlinkCtrl != null) {
                    main.Add(BlinkSetting("眨眼频率", "blink-rate", female.female.fbsCtrl.BlinkCtrl.BlinkFrequency, 100f, 30f, female, female.ChangeBlinkRate));
                    main.Add(BlinkSetting("眨眼速度", "blink-speed", female.female.fbsCtrl.BlinkCtrl.BaseSpeed, 0.3f, 0.15f, female, female.ChangeBlinkSpeed));
                }
                main.Add(Pattern("口", PartType.mouth, expression.mouthPtn, female.maxMouthPtn, owner.mouthPtnNameDict,
                    female, female.lockMouthPtn, v => female.lockMouthPtn = v));
                main.Add(ValueSlider("开合", "mouth-open", expression.mouthOpen, 0f, 1f, female,
                    v => female.ChangeOpen(PartType.mouth, v), female.lockMouthOpen, v => female.lockMouthOpen = v));
                main.Add(ValueSlider("泪", "tear", expression.tearLv, 0f, 3f, female,
                    v => female.ChangeTear((byte)v), female.lockTear, v => female.lockTear = v));
                main.Add(ValueSlider("脸红", "blush", expression.hohoAka, 0f, 1f, female,
                    female.ChangeHohoAka, female.lockHohoAka, v => female.lockHohoAka = v));
                main.Add(new Separator());
                main.Add(BuildPresets(female));
                main.Add(new Separator());
                main.Add(BuildScenePresets());
                main.Add(BuildRunningProgram(female));
                main.Add(EditProgramButton());
                return new ScrollView("expression-main-scroll") { Height = 0f }.Content(main).ExpandHeight();
            }

            private Widget EditProgramButton() {
                return new Button("编辑表情程序", owner.ToggleMaximumUiProgram, "edit-program").Height(Row).ExpandWidth();
            }

            private Widget Pattern(string title, PartType part, int value, int count, Dictionary<int, string> names,
                FemaleData female, bool locked, Action<bool> setLock) {
                int[] options;
                if (!patternRanges.TryGetValue(count, out options)) {
                    options = new int[Math.Max(0, count)];
                    for (int i = 0; i < options.Length; i++) options[i] = i;
                    patternRanges[count] = options;
                }
                return new HStack(4f) {
                    new Label(title).Width(52f),
                    new ArrowButton(ArrowDirection.Left, delegate { ChangeFemale(female, delegate { female.PrevPart(part); }); }, "pattern-previous-" + part).Width(28f),
                    Choice(value, options, v => names.ContainsKey(v) ? names[v] : v.ToString(),
                        v => ChangeFemale(female, delegate { female.SetPart(part, v); }), "pattern-" + part).ExpandWidth(),
                    new ArrowButton(ArrowDirection.Right, delegate { ChangeFemale(female, delegate { female.NextPart(part); }); }, "pattern-next-" + part).Width(28f),
                    Lock(female, locked, setLock, "pattern-lock-" + part)
                }.Height(Row);
            }

            private Widget ValueSlider(string title, string key, float value, float min, float max,
                FemaleData female, Action<float> apply, bool locked, Action<bool> setLock) {
                return new HStack(4f) {
                    new Label(title).Width(52f),
                    new Slider(value, min, max, v => ChangeFemale(female, delegate { apply(v); }), key).ExpandWidth(),
                    Lock(female, locked, setLock, key + "-lock")
                }.Height(Row);
            }

            private Widget ToggleWithLock(string label, string key, bool value, FemaleData female,
                Action<bool> apply, bool locked, Action<bool> setLock) {
                return new HStack(4f) {
                    new Checkbox(label, value, v => ChangeFemale(female, delegate { apply(v); }), key).ExpandWidth(),
                    Lock(female, locked, setLock, key + "-lock")
                }.Height(Row);
            }

            private Widget Lock(FemaleData female, bool value, Action<bool> apply, string key) {
                return new Checkbox(string.Empty, value, v => ChangeFemale(female, delegate { apply(v); }), key).Width(24f).Tooltip("固定此项");
            }

            private Widget BlinkSetting(string label, string key, float value, float maximum, float reset,
                FemaleData female, Action<float> apply) {
                return new HStack(4f) {
                    new Label(label).Width(84f),
                    new Slider(value, 0f, maximum, v => ChangeFemale(female, delegate { apply(v); }), key).ExpandWidth(),
                    new Button("|", delegate { ChangeFemale(female, delegate { apply(reset); }); }, key + "-reset").Width(28f).Tooltip("恢复默认值")
                }.Height(Row);
            }

            private Expression SavedExpression(Expression current) {
                string guid = current == null ? null : current.guid;
                if (!ReferenceEquals(resolvedPresetSource, owner.xml.expressions) || resolvedPresetGuid != guid || resolvedPresetCount != owner.xml.expressions.Count) {
                    resolvedPresetSource = owner.xml.expressions; resolvedPresetGuid = guid; resolvedPresetCount = owner.xml.expressions.Count;
                    resolvedPreset = owner.xml.expressions.Find(e => e.guid == guid);
                }
                return resolvedPreset ?? current;
            }

            private Widget BuildPresets(FemaleData female) {
                string selected = female.expression.guid;
                return new VStack(4f) {
                    new HStack(4f) {
                        new Label("表情").Width(52f),
                        new ArrowButton(ArrowDirection.Left, delegate { CycleExpression(female, -1); }, "expression-previous").Width(28f),
                        Choice(SavedExpression(female.expression), owner.xml.expressions, e => e == null ? string.Empty : e.name,
                            e => ChangeFemale(female, delegate { if (owner.xml.expressions.Contains(e)) female.SetExpression(e); }), "expression-choice").ExpandWidth(),
                        new ArrowButton(ArrowDirection.Right, delegate { CycleExpression(female, 1); }, "expression-next").Width(28f)
                    }.Height(Row),
                    new HStack(4f) {
                        new Button("新建保存", delegate { SaveExpression(female, false); }, "expression-save").ExpandWidth().Enabled(CanWrite),
                        new Button("覆盖保存", delegate { SaveExpression(female, true); }, "expression-overwrite").ExpandWidth().Enabled(!string.IsNullOrEmpty(selected) && CanWrite),
                        new Button("删除", delegate { DeleteExpression(female); }, "expression-delete").ExpandWidth().Enabled(!string.IsNullOrEmpty(selected) && CanWrite)
                    }.Height(Row)
                };
            }

            private void CycleExpression(FemaleData female, int direction) {
                ChangeFemale(female, delegate {
                    int count = owner.xml.expressions.Count;
                    if (count == 0) return;
                    int index = owner.xml.expressions.FindIndex(e => e.guid == female.expression.guid);
                    index = index < 0 ? (direction < 0 ? count - 1 : 0) : (index + direction + count) % count;
                    female.SetExpression(owner.xml.expressions[index]);
                });
            }

            private Widget BuildScenePresets() {
                ScenePreset preset = owner.CurrentScenePreset();
                return new VStack(4f) {
                    new HStack(4f) {
                        new Label("场景预设").Width(84f),
                        new ArrowButton(ArrowDirection.Left, delegate { Change(delegate { owner.SelectScenePreset(owner.selectedScenePreset - 1); }); }, "scene-previous").Width(28f),
                        Choice(preset, owner.xml.scenePresets, p => p == null ? "（无场景预设）" : p.name,
                            p => Change(delegate { if (owner.xml.scenePresets.Contains(p)) owner.SelectScenePreset(owner.xml.scenePresets.IndexOf(p)); }), "scene-choice").ExpandWidth(),
                        new ArrowButton(ArrowDirection.Right, delegate { Change(delegate { owner.SelectScenePreset(owner.selectedScenePreset + 1); }); }, "scene-next").Width(28f)
                    }.Height(Row),
                    new HStack(4f) {
                        new Button("应用选中", delegate { Change(owner.ApplyCurrentScenePreset); }, "scene-apply").ExpandWidth().Enabled(preset != null),
                        new Button("新建保存", delegate { SaveScene(false); }, "scene-save").ExpandWidth().Enabled(owner.femaleList.Count > 0 && CanWrite),
                        new Button("覆盖保存", delegate { SaveScene(true); }, "scene-overwrite").ExpandWidth().Enabled(preset != null && owner.femaleList.Count > 0 && CanWrite),
                        new Button("删除选中", DeleteScene, "scene-delete").ExpandWidth().Enabled(preset != null && CanWrite)
                    }.Height(Row)
                };
            }

            private Widget BuildRunningProgram(FemaleData female) {
                Program selected = SavedProgramOrDraft(female.program);
                return new VStack(4f) {
                    new Label("表情程序").Height(Row),
                    new HStack(4f) {
                        new ArrowButton(ArrowDirection.Left, delegate { CycleRunningProgram(female, -1); }, "running-program-previous").Width(28f),
                        Choice(selected, owner.xml.programs, p => p == null ? string.Empty : p.name,
                            p => SetRunningProgram(female, p), "running-program-choice").ExpandWidth(),
                        new ArrowButton(ArrowDirection.Right, delegate { CycleRunningProgram(female, 1); }, "running-program-next").Width(28f)
                    }.Height(Row),
                    new Label("倒计时： " + (female.programTimer >= 20f ? "-/-" : female.programTimerNow.ToString("F2") + "/" + female.programTimer.ToString("F2")), "program-countdown").Height(Row),
                    new Slider(female.programTimer, 1f, 20f, delegate(float value) {
                        ChangeFemale(female, delegate {
                            bool old = female.programValid; female.programTimer = value; owner.SetProgramTimer(female);
                            if (old != female.programValid) female.UpdateState(false, owner.nowMotion, owner.isFeraAction);
                        });
                    }, "program-timer").Height(Row),
                    new HStack(4f) {
                        new ArrowButton(ArrowDirection.Left, delegate { ChangeFemale(female, delegate { owner.AdjustCharacterOrgasmCount(female, -1); }); }, "orgasm-previous").Width(28f),
                        new ArrowButton(ArrowDirection.Right, delegate { ChangeFemale(female, delegate { owner.AdjustCharacterOrgasmCount(female, 1); }); }, "orgasm-next").Width(28f),
                        new Label("绝顶次数: " + owner.GetCharacterOrgasmCount(female), "orgasm-count").ExpandWidth()
                    }.Height(Row)
                };
            }

            private void CycleRunningProgram(FemaleData female, int direction) {
                int count = owner.xml.programs.Count;
                if (count == 0) return;
                int index = owner.xml.programs.FindIndex(p => female.program != null && p.guid == female.program.guid);
                index = index < 0 ? (direction < 0 ? count - 1 : 0) : (index + direction + count) % count;
                SetRunningProgram(female, owner.xml.programs[index]);
            }

            private void SetRunningProgram(FemaleData female, Program program) {
                ChangeFemale(female, delegate {
                    if (!owner.xml.programs.Contains(program)) return;
                    bool old = female.programValid; female.SetProgram(program);
                    if (old != female.programValid) female.UpdateState(false, owner.nowMotion, owner.isFeraAction);
                });
            }

            private Widget BuildProgramEditor() {
                Program program = owner.currentProgram;
                if (!ReferenceEquals(identityProgram, program)) { unitIds.Clear(); identityProgram = program; }
                owner.selectedProgramUnit = Math.Max(0, Math.Min(program.units.Count - 1, owner.selectedProgramUnit));
                Program.Unit selectedUnit = program.units.Count == 0 ? null : program.units[owner.selectedProgramUnit];
                Func<bool> current = delegate { return ReferenceEquals(owner.currentProgram, program); };
                Func<bool> selectionCurrent = delegate {
                    return current() && selectedUnit != null && owner.selectedProgramUnit >= 0 && owner.selectedProgramUnit < program.units.Count &&
                        ReferenceEquals(program.units[owner.selectedProgramUnit], selectedUnit);
                };
                return new VStack(8f) {
                    new HStack(8f) {
                        new HStack(4f) {
                            new ArrowButton(ArrowDirection.Left, delegate { Change(owner.PrevProgram); }, "program-previous").Width(28f),
                            Choice(SavedProgramOrDraft(program), owner.xml.programs, p => p == null ? "- - -" : p.name,
                                p => Change(delegate { int index = owner.xml.programs.IndexOf(p); if (index >= 0) owner.SelectProgram(index); }), "program-choice").Width(225f),
                            new ArrowButton(ArrowDirection.Right, delegate { Change(owner.NextProgram); }, "program-next").Width(28f),
                            new Button("新建保存", delegate { if (current()) SaveProgram(false); }, "program-save").Width(96f).Enabled(program.units.Count > 0 && CanWrite),
                            new Button("覆盖保存", delegate { if (current()) SaveProgram(true); }, "program-overwrite").Width(96f).Enabled(!string.IsNullOrEmpty(program.guid) && CanWrite),
                            new Button("删除", delegate { if (current()) DeleteProgram(); }, "program-delete").Width(64f).Enabled(!string.IsNullOrEmpty(program.guid) && CanWrite),
                            new Spacer()
                        }.ExpandWidth(),
                        new Button("表情排序", ToggleExpressionSort, "sort-expressions").Width(196f).Enabled(CanWrite)
                    }.Height(Row),
                    new HStack(12f) {
                        new VirtualList(program.units.Count, UnitHeight, index => BuildUnitPanel(program, program.units[index]), "unit-list") {
                            Height = 0f, AlwaysShowVertical = true
                        }.ExpandWidth().ExpandHeight(),
                        new VirtualList(owner.xml.expressions.Count, Row, index => {
                            Expression expression = owner.xml.expressions[index];
                            if (owner.isSortExpression) return new HStack(4f) {
                                new ArrowButton(ArrowDirection.Up, delegate { ReorderExpression(expression, true); }, "expression-up-" + expression.guid).Width(26f),
                                new ArrowButton(ArrowDirection.Down, delegate { ReorderExpression(expression, false); }, "expression-down-" + expression.guid).Width(26f),
                                new Label(expression.name).ExpandWidth()
                            }.Height(Row);
                            return new Checkbox(expression.name, selectedUnit != null && selectedUnit.expressionGuids.Contains(expression.guid), delegate(bool value) {
                                if (!current() || selectedUnit == null || !program.units.Contains(selectedUnit) || !owner.xml.expressions.Contains(expression)) return;
                                ExpressionProgramWidgets.SetExpressionSelected(selectedUnit, expression.guid, value); Invalidate();
                            }, "unit-expression-" + expression.guid).Height(Row).ExpandWidth().Enabled(selectedUnit != null);
                        }, "unit-expression-list") { Height = 0f, AlwaysShowVertical = true }.Width(196f).ExpandHeight()
                    }.ExpandHeight(),
                    new HStack(12f) {
                        new HStack(4f) {
                            new ArrowButton(ArrowDirection.Up, delegate { if (selectionCurrent()) Change(delegate { owner.MoveUnit(true); }); }, "unit-up").Width(32f).Enabled(selectedUnit != null && owner.selectedProgramUnit > 0),
                            new ArrowButton(ArrowDirection.Down, delegate { if (selectionCurrent()) Change(delegate { owner.MoveUnit(false); }); }, "unit-down").Width(32f).Enabled(selectedUnit != null && owner.selectedProgramUnit + 1 < program.units.Count),
                            new MaximumUI.Space(8f),
                            new Button("添加", delegate { if (current()) Change(owner.AddUnit); }, "unit-add").Width(64f),
                            new Button("复制", delegate { if (selectionCurrent()) Change(delegate { owner.CopyUnit(false); }); }, "unit-copy").Width(64f).Enabled(selectedUnit != null),
                            new Button("复制到下方", delegate { if (selectionCurrent()) Change(delegate { owner.CopyUnit(true); }); }, "unit-copy-below").Width(112f).Enabled(selectedUnit != null),
                            new Button("删除", delegate { if (selectionCurrent()) Change(owner.DeleteUnit); }, "unit-delete").Width(64f).Enabled(selectedUnit != null)
                        }.Height(Row).Width(420f),
                        ExpressionProgramWidgets.BuildFlags(program, Invalidate, current).ExpandWidth()
                    }.Height(60f)
                }.ExpandHeight();
            }

            private Widget BuildUnitPanel(Program program, Program.Unit unit) {
                int id;
                if (!unitIds.TryGetValue(unit, out id)) { id = ++nextUnitId; unitIds[unit] = id; }
                string prefix = "unit:" + id + ":";
                Func<bool> current = delegate { return ReferenceEquals(owner.currentProgram, program) && program.units.Contains(unit); };
                int index = program.units.IndexOf(unit);
                Panel panel = new Panel(prefix + "panel") { Selected = index == owner.selectedProgramUnit }.Content(new HStack(6f) {
                    new RadioButton(string.Empty, index == owner.selectedProgramUnit,
                        delegate { if (current()) Change(delegate { owner.selectedProgramUnit = program.units.IndexOf(unit); }); }, prefix + "select").Width(24f).ExpandHeight().Tooltip("选择此项"),
                    new VStack(4f) {
                        new ArrowButton(ArrowDirection.Up, delegate { if (current()) Change(delegate { owner.MoveUnit(program.units.IndexOf(unit), true); }); }, prefix + "up").ExpandHeight().Enabled(index > 0),
                        new ArrowButton(ArrowDirection.Down, delegate { if (current()) Change(delegate { owner.MoveUnit(program.units.IndexOf(unit), false); }); }, prefix + "down").ExpandHeight().Enabled(index + 1 < program.units.Count)
                    }.Width(26f).ExpandHeight(),
                    ExpressionProgramWidgets.BuildUnit(unit, Invalidate, prefix, current).ExpandWidth()
                }.ExpandHeight()).Height(UnitHeight - 6f).ExpandWidth();
                // A virtual row receives the full row rectangle. Keep the gap
                // inside its root so adjacent panels do not merge visually.
                return new VStack(0f, prefix + "row") { panel, new MaximumUI.Space(6f) }.ExpandWidth();
            }

            private void ToggleExpressionSort() {
                if (!CanWrite) return;
                Change(delegate {
                    bool next = !owner.isSortExpression;
                    if (!next && owner.isSortExpressionModify) owner.xml.Save(true);
                    owner.isSortExpressionModify = false; owner.isSortExpression = next;
                });
            }

            private void ReorderExpression(Expression expression, bool up) {
                Change(delegate { int index = owner.xml.expressions.IndexOf(expression); if (index >= 0) owner.MoveExpression(index, up); });
            }

            private Program SavedProgramOrDraft(Program program) {
                if (program == null || string.IsNullOrEmpty(program.guid)) return program;
                return owner.xml.programs.Find(p => p.guid == program.guid) ?? program;
            }

            private static ComboBox<T> Choice<T>(T value, IList<T> options, Func<T, string> format, Action<T> change, string key) {
                // List identity is retained; constructing the narrow main window
                // must not copy thousands of presets on every live refresh.
                return new ComboBox<T>(string.Empty, value, null, format, change, key) {
                    Options = options, Items = options, MaxVisibleItems = 12
                };
            }

            private bool CanWrite { get { return owner.presetReadError == null; } }
            private static string PresetPath { get { return Path.Combine(Paths.ConfigPath, "MaxMixAlex.ExpressionControl.presets.xml"); } }
            private static string ErrorText(Exception error) {
                while (error.InnerException != null) error = error.InnerException;
                return error.Message;
            }

            private void Prompt(string title, string initial, Func<string, bool> save) {
                if (!CanWrite || context == null || context.Dialogs == null) return;
                string text = initial ?? string.Empty;
                Label errorLabel = new Label(string.Empty) { WordWrap = true, Role = LabelRole.Muted };
                Dialog dialog = new Dialog("ExpressionControl " + ExpressionControlPlugin.Version, "expression-name-dialog") { Width = 480f, Height = 235f };
                TextField input = new TextField(text, key: "preset-name") { AutoFocus = true };
                input.OnChange = delegate(string value) { text = value; input.Value = value; };
                Action submit = delegate {
                    if (!CanWrite) { errorLabel.Text = "XML 载入失败，保存已停止。"; return; }
                    string name = text.Trim();
                    if (name.Length == 0) { errorLabel.Text = "请输入名称。"; return; }
                    try {
                        if (!save(name)) { errorLabel.Text = "未能保存，请检查名称是否重复以及当前选择是否仍然有效。"; return; }
                        context.Dialogs.Close(dialog); resolvedPresetSource = null; status = string.Empty; Invalidate();
                    } catch (Exception error) { errorLabel.Text = ErrorText(error); Debug.LogException(error); }
                };
                input.OnSubmit = delegate { submit(); };
                dialog.Add(new VStack(10f) {
                    new Label(title).Height(Row), input.Height(Row), errorLabel.Height(45f),
                    new HStack(8f) {
                        new Button("确定", submit).ExpandWidth(),
                        new Button("取消", delegate { context.Dialogs.Close(dialog); }).ExpandWidth()
                    }.Height(Row)
                });
                context.Dialogs.Show(dialog);
            }

            private void ConfirmDelete(string name, Action delete) {
                Dialog dialog = new Dialog("ExpressionControl " + ExpressionControlPlugin.Version, "expression-delete-dialog") { Width = 420f, Height = 195f };
                dialog.Add(new VStack(8f) {
                    new Label("确定要删除吗？").Height(Row),
                    new Label(name ?? string.Empty) { WordWrap = true }.MinHeight(Row),
                    new HStack(8f) {
                        new Button("确定", delegate { if (!CanWrite) return; delete(); resolvedPresetSource = null; context.Dialogs.Close(dialog); }).ExpandWidth(),
                        new Button("取消", delegate { context.Dialogs.Close(dialog); }).ExpandWidth()
                    }.Height(Row)
                });
                context.Dialogs.Show(dialog);
            }

            private void SaveExpression(FemaleData female, bool overwrite) {
                if (female == null) return;
                string guid = female.expression.guid;
                Prompt(overwrite ? "要覆盖（并重命名）吗？" : "请输入表情名", overwrite ? female.expression.name : string.Empty, delegate(string name) {
                    if (!ReferenceEquals(CurrentFemale(), female) || (overwrite && female.expression.guid != guid)) return false;
                    return overwrite ? owner.XML_UpdateExpression(guid, name) : owner.XML_SaveNewExpression(name);
                });
            }

            private void DeleteExpression(FemaleData female) {
                if (female == null || context == null || context.Dialogs == null || !CanWrite) return;
                string guid = female.expression.guid;
                ConfirmDelete(female.expression.name, delegate {
                    Change(delegate {
                        if (!ReferenceEquals(CurrentFemale(), female) || female.expression.guid != guid) return;
                        if (!owner.XML_DeleteExpression(guid)) throw new InvalidOperationException("所选表情预设已不存在。");
                    }, "表情预设已删除。");
                });
            }

            private void SaveProgram(bool overwrite) {
                Program program = owner.currentProgram;
                Prompt(overwrite ? "要覆盖（并重命名）吗？" : "请输入程序名", overwrite ? program.name : string.Empty, delegate(string name) {
                    if (!ReferenceEquals(owner.currentProgram, program)) return false;
                    bool saved = overwrite ? owner.XML_UpdateProgram(name) : owner.XML_SaveNewProgram(name);
                    if (saved) owner.currentProgramNum = owner.xml.programs.FindIndex(p => p.guid == program.guid);
                    return saved;
                });
            }

            private void DeleteProgram() {
                if (context == null || context.Dialogs == null || !CanWrite) return;
                Program program = owner.currentProgram;
                ConfirmDelete(program.name, delegate {
                    Change(delegate { if (ReferenceEquals(owner.currentProgram, program)) owner.XML_DeleteProgram(); }, "程序已删除。");
                });
            }

            private void SaveScene(bool overwrite) {
                ScenePreset preset = owner.CurrentScenePreset();
                Prompt(overwrite ? "要覆盖（并重命名）吗？" : "请输入场景预设名", overwrite && preset != null ? preset.name : string.Empty,
                    delegate(string name) {
                        if (overwrite && !ReferenceEquals(owner.CurrentScenePreset(), preset)) return false;
                        return overwrite ? owner.XML_UpdateScenePreset(name) : owner.XML_SaveNewScenePreset(name);
                    });
            }

            private void DeleteScene() {
                if (context == null || context.Dialogs == null || !CanWrite) return;
                ScenePreset preset = owner.CurrentScenePreset();
                if (preset == null) return;
                ConfirmDelete(preset.name, delegate {
                    Change(delegate { if (ReferenceEquals(owner.CurrentScenePreset(), preset)) owner.XML_DeleteScenePreset(); }, "场景预设已删除。");
                });
            }
        }
    }
}
