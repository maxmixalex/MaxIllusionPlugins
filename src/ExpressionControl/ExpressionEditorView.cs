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
        internal sealed class ExpressionEditorView : View {
            private readonly ExpressionControl owner;
            private BuildContext context;
            private int page;
            private string filter = string.Empty;
            private string status = "选择角色后编辑表情；预设、程序和场景设置沿用现有 XML。";
            private string shortcutDraft;
            private bool lightTheme = true;
            private float nextPoll;
            private FemaleData observedFemale;
            private int observedCount = -1;
            private readonly float[] liveValues = new float[20];
            private IList<Expression> filteredSource;
            private List<Expression> filteredExpressions = new List<Expression>();
            private string filteredQuery;
            private readonly Dictionary<int, int[]> patternRanges = new Dictionary<int, int[]>();
            private const float Row = 30f;

            internal ExpressionEditorView(ExpressionControl owner) { this.owner = owner; }

            internal void Invalidate() {
                MaximumUIRuntime.MarkDirty(this);
            }

            public override void Update(BuildContext buildContext) {
                if (!MaximumUIRuntime.IsVisible(MaximumUiMountId) || Time.unscaledTime < nextPoll) return;
                nextPoll = Time.unscaledTime + 0.2f;
                FemaleData female = CurrentFemale();
                bool changed = !ReferenceEquals(observedFemale, female) || observedCount != owner.femaleList.Count;
                observedFemale = female;
                observedCount = owner.femaleList.Count;
                // Live values only refresh the character page, and only when the
                // visible snapshot changed. Program/scene editing stays retained.
                if (page == 0 && female != null && female.expression != null) {
                    Expression e = female.expression;
                    changed |= Observe(0, e.eyebrowPtn); changed |= Observe(1, e.eyesPtn);
                    changed |= Observe(2, e.mouthPtn); changed |= Observe(3, e.eyeOpen);
                    changed |= Observe(4, e.mouthOpen); changed |= Observe(5, e.eyeY);
                    changed |= Observe(6, e.eyeSmall); changed |= Observe(7, e.tearLv);
                    changed |= Observe(8, e.hohoAka); changed |= Observe(9, e.hideHighlight ? 1f : 0f);
                    changed |= Observe(10, e.blink ? 1f : 0f);
                    changed |= Observe(11, (float)Math.Round(female.programTimerNow, 1));
                    changed |= Observe(12, female.programTimer); changed |= Observe(13, owner.orgazumCount);
                    changed |= Observe(14, female.programValid ? 1f : 0f);
                }
                if (changed) MaximumUIRuntime.MarkDirty(this);
            }

            private bool Observe(int slot, float value) {
                if (liveValues[slot] == value) return false;
                liveValues[slot] = value;
                return true;
            }

            private FemaleData CurrentFemale() {
                int index = owner.currentFemaleNum;
                if (index < 0 || index >= owner.femaleList.Count) return null;
                FemaleData female = owner.femaleList[index];
                return female != null && female.female != null ? female : null;
            }

            private void Change(Action action, string success = null) {
                try { action(); if (success != null) status = success; }
                catch (Exception error) {
                    status = ErrorText(error);
                    Debug.LogException(error);
                }
                Invalidate();
            }

            private void ChangeFemale(FemaleData female, Action action) {
                if (female == null || !owner.femaleList.Contains(female) || female.female == null) {
                    status = "角色已离开当前场景，请刷新角色列表。"; Invalidate(); return;
                }
                Change(action);
            }

            public override Widget Build(BuildContext buildContext) {
                context = buildContext;
                if (owner.currentProgram == null) owner.currentProgram = new Program();
                VStack body = new VStack(10f) {
                    new HStack(8f) {
                        new Label("Expression Control") { Role = LabelRole.Heading }.ExpandWidth(),
                        new Button(lightTheme ? "深色" : "浅色", delegate {
                            lightTheme = !lightTheme;
                            MaximumUIRuntime.SetTheme(MaximumUiMountId,
                                lightTheme ? MaximumTheme.MetroLight : MaximumTheme.MetroDark);
                            Invalidate();
                        }, "theme").Width(65f),
                        new Button("刷新角色", delegate { Change(owner.UpdateFemale, "角色列表已刷新。"); }, "refresh").Width(90f)
                    }.Height(36f),
                    new HStack(6f) {
                        PageButton("表情", 0), PageButton("程序", 1), PageButton("场景", 2), PageButton("设置", 3)
                    }.Height(Row),
                    new Separator()
                };
                if (owner.presetReadError != null) {
                    body.Add(new HelpBox("XML 载入失败，保存已停止。\n" + PresetPath + "\n" + ErrorText(owner.presetReadError), HelpBoxType.Error).MinHeight(70f));
                    body.Add(new Button("修复文件后重新载入", delegate { Change(owner.ReloadMaximumUiPresets, "XML 已重新载入。"); }, "reload-error").Height(Row));
                }
                Widget content = page == 1 ? BuildProgramPage() : page == 2 ? BuildScenePage() : page == 3 ? BuildSettingsPage() : BuildCharacterPage();
                body.Add(content.ExpandWidth().ExpandHeight());
                body.Add(new Separator());
                body.Add(new Label(status) { Role = LabelRole.Muted, WordWrap = true }.MinHeight(34f));
                return new Window("EXPRESSION CONTROL  /  " + ExpressionControlPlugin.Version,
                    1080f, 760f, "expression-window", 900f, 540f, true, true,
                    delegate(bool visible) { MaximumUIRuntime.SetVisible(MaximumUiMountId, visible); })
                    .Content(body.ExpandHeight());
            }

            private Widget PageButton(string title, int index) {
                return new Button(title, delegate { page = index; Invalidate(); }, "page-" + index) {
                    Variant = page == index ? ButtonVariant.Primary : ButtonVariant.Quiet
                }.ExpandWidth();
            }

            private Widget BuildCharacterPage() {
                FemaleData female = CurrentFemale();
                VStack main = new VStack(10f) {
                    new SearchableComboBox<FemaleData>("角色", female, owner.femaleList,
                        f => f == null || f.female == null ? "（不可用）" : f.fullName,
                        delegate(FemaleData selected) { Change(delegate { owner.currentFemaleNum = owner.femaleList.IndexOf(selected); }); }, "character") {
                        LabelWidth = 60f, FilterPlaceholder = "搜索角色"
                    }.Height(Row)
                };
                if (female == null || female.expression == null) {
                    main.Add(new HelpBox("当前没有可编辑角色。进入角色场景后刷新列表；仍可编辑程序和查看预设。"));
                } else {
                    Expression expression = female.expression;
                    main.Add(new HStack(8f) {
                        new Button("获取当前表情", delegate { ChangeFemale(female, female.Update); }, "read-face").ExpandWidth(),
                        new Checkbox("固定表情", female.lockExpression, v => ChangeFemale(female, delegate { female.lockExpression = v; }), "lock-all").Width(130f)
                    }.Height(Row));
                    main.Add(new SeparatorText("眉眼与瞳孔").Height(Row));
                    main.Add(Pattern("眉", PartType.eyebrow, expression.eyebrowPtn, female.maxEyebrowPtn,
                        owner.eyeBrowPtnNameDict, female, female.lockEyebrowPtn, v => female.lockEyebrowPtn = v));
                    main.Add(Pattern("眼", PartType.eyes, expression.eyesPtn, female.maxEyesPtn,
                        owner.eyesPtnNameDict, female, female.lockEyePtn, v => female.lockEyePtn = v));
                    main.Add(ValueSlider("眼睛开合", "eye-open", expression.eyeOpen, 0f, 1f, female,
                        v => female.ChangeOpen(PartType.eyes, v), female.lockEyeOpen, v => female.lockEyeOpen = v));
                    main.Add(ValueSlider("瞳孔上移", "eye-y", expression.eyeY, 0f, 0.5f, female,
                        female.ChangeEyeY, female.lockEyeY, v => female.lockEyeY = v));
                    main.Add(ValueSlider("缩瞳", "eye-small", expression.eyeSmall, 0f, 1f, female,
                        female.ChangeEyeSmall, female.lockEyeSmall, v => female.lockEyeSmall = v));
                    main.Add(ToggleWithLock("隐藏高光", "highlight", expression.hideHighlight, female,
                        female.ChangeHighlight, female.lockHighlight, v => female.lockHighlight = v));
                    main.Add(ToggleWithLock("允许眨眼", "blink", expression.blink, female,
                        female.ChangeBlink, female.lockBlink, v => female.lockBlink = v));
                    if (female.female.fbsCtrl != null && female.female.fbsCtrl.BlinkCtrl != null) {
                        main.Add(BlinkSetting("眨眼频率", "blink-rate", female.female.fbsCtrl.BlinkCtrl.BlinkFrequency,
                            100f, 30f, female, female.ChangeBlinkRate));
                        main.Add(BlinkSetting("眨眼速度", "blink-speed", female.female.fbsCtrl.BlinkCtrl.BaseSpeed,
                            0.3f, 0.15f, female, female.ChangeBlinkSpeed));
                    }
                    main.Add(new SeparatorText("口型与皮肤").Height(Row));
                    main.Add(Pattern("口", PartType.mouth, expression.mouthPtn, female.maxMouthPtn,
                        owner.mouthPtnNameDict, female, female.lockMouthPtn, v => female.lockMouthPtn = v));
                    main.Add(ValueSlider("嘴巴开合", "mouth-open", expression.mouthOpen, 0f, 1f, female,
                        v => female.ChangeOpen(PartType.mouth, v), female.lockMouthOpen, v => female.lockMouthOpen = v));
                    main.Add(new HStack(8f) {
                        new Label("眼泪").Width(96f),
                        new SliderInt(string.Empty, expression.tearLv, 0, 3,
                            v => ChangeFemale(female, delegate { female.ChangeTear((byte)v); }), "tear").ExpandWidth(),
                        Lock(female, female.lockTear, v => female.lockTear = v, "tear-lock")
                    }.Height(Row));
                    main.Add(ValueSlider("脸红", "blush", expression.hohoAka, 0f, 1f, female,
                        female.ChangeHohoAka, female.lockHohoAka, v => female.lockHohoAka = v));
                }
                return new HStack(16f) {
                    new Panel("face-panel").Content(new ScrollView("face-scroll").Content(main.ExpandWidth()).ExpandHeight()).ExpandWidth().ExpandHeight(),
                    new VStack(10f) { BuildPresets(female).ExpandHeight(), BuildRunningProgram(female) }.Width(330f).ExpandHeight()
                }.ExpandHeight();
            }

            private Widget Pattern(string title, PartType part, int value, int count, Dictionary<int, string> names,
                FemaleData female, bool locked, Action<bool> setLock) {
                int[] options;
                if (!patternRanges.TryGetValue(count, out options)) {
                    options = new int[Math.Max(0, count)];
                    for (int i = 0; i < options.Length; i++) options[i] = i;
                    patternRanges[count] = options;
                }
                return new HStack(6f) {
                    new Label(title).Width(40f),
                    new ArrowButton(ArrowDirection.Left, delegate { ChangeFemale(female, delegate { female.PrevPart(part); }); }).Width(28f),
                    new SearchableComboBox<int>(string.Empty, value, options,
                        v => names.ContainsKey(v) ? names[v] : v.ToString(),
                        v => ChangeFemale(female, delegate { female.SetPart(part, v); }), "pattern-" + part) {
                        FilterPlaceholder = "搜索动作"
                    }.ExpandWidth(),
                    new ArrowButton(ArrowDirection.Right, delegate { ChangeFemale(female, delegate { female.NextPart(part); }); }).Width(28f),
                    Lock(female, locked, setLock, "pattern-lock-" + part)
                }.Height(Row);
            }

            private Widget ValueSlider(string title, string key, float value, float min, float max,
                FemaleData female, Action<float> apply, bool locked, Action<bool> setLock) {
                return new HStack(8f) {
                    new Label(title).Width(96f),
                    new Slider(value, min, max, v => ChangeFemale(female, delegate { apply(v); }), key).ExpandWidth(),
                    new InputFloat(string.Empty, value, v => ChangeFemale(female, delegate { apply(v); }), key + "-value") {
                        HasMin = true, Min = min, HasMax = true, Max = max, Step = 0.01f, StepFast = 0.1f, Format = "0.###"
                    }.Width(112f),
                    Lock(female, locked, setLock, key + "-lock")
                }.Height(Row);
            }

            private Widget ToggleWithLock(string label, string key, bool value, FemaleData female,
                Action<bool> apply, bool locked, Action<bool> setLock) {
                return new HStack(8f) {
                    new Checkbox(label, value, v => ChangeFemale(female, delegate { apply(v); }), key).ExpandWidth(),
                    Lock(female, locked, setLock, key + "-lock")
                }.Height(Row);
            }

            private Widget Lock(FemaleData female, bool value, Action<bool> apply, string key) {
                return new Checkbox("锁定", value, v => ChangeFemale(female, delegate { apply(v); }), key).Width(68f);
            }

            private Widget BlinkSetting(string label, string key, float value, float maximum, float reset,
                FemaleData female, Action<float> apply) {
                return new HStack(8f) {
                    new SliderFloat(label, value, 0f, maximum, v => ChangeFemale(female, delegate { apply(v); }), key).ExpandWidth(),
                    new Button("重置", delegate { ChangeFemale(female, delegate { apply(reset); }); }, key + "-reset").Width(68f)
                }.Height(Row);
            }

            private List<Expression> FilteredExpressions() {
                // Saving/loading replaces the source list; ordinary face/lock
                // edits must not refilter a large preset library on every tick.
                if (!ReferenceEquals(filteredSource, owner.xml.expressions) || filteredQuery != filter) {
                    filteredSource = owner.xml.expressions;
                    filteredQuery = filter;
                    filteredExpressions = ExpressionProgramWidgets.Filter(filteredSource, filter);
                }
                return filteredExpressions;
            }

            private Widget BuildPresets(FemaleData female) {
                List<Expression> choices = FilteredExpressions();
                string selected = female == null || female.expression == null ? null : female.expression.guid;
                return new Panel("presets").Content(new VStack(8f) {
                    new Label("表情预设") { Role = LabelRole.Heading }.Height(32f),
                    new SearchBox(filter, delegate(string value) { filter = value; Invalidate(); }, "expression-search") { Placeholder = "搜索预设" }.Height(Row),
                    new VirtualList(choices.Count, Row, delegate(int index) {
                        Expression expression = choices[index];
                        return new Selectable(expression.name, expression.guid == selected, delegate(bool value) {
                            if (female != null) ChangeFemale(female, delegate { female.SetExpression(expression); });
                        }, "preset-" + expression.guid).Height(Row).ExpandWidth().Enabled(female != null);
                    }, "expression-list") { Height = 200f }.ExpandHeight().ExpandWidth(),
                    new HStack(6f) {
                        new Button("新建保存", delegate { SaveExpression(female, false); }, "expression-save").ExpandWidth().Enabled(female != null && CanWrite),
                        new Button("覆盖", delegate { SaveExpression(female, true); }, "expression-overwrite").Width(65f).Enabled(!string.IsNullOrEmpty(selected) && CanWrite),
                        new Button("删除", delegate { DeleteExpression(female); }, "expression-delete") { Variant = ButtonVariant.Danger }.Width(65f).Enabled(!string.IsNullOrEmpty(selected) && CanWrite)
                    }.Height(Row)
                }.ExpandHeight()).ExpandWidth();
            }

            private Widget BuildRunningProgram(FemaleData female) {
                if (female == null) return new HelpBox("选择角色后可应用表情预设和运行程序。");
                Program selected = SavedProgramOrDraft(female.program);
                return new Panel("running-program").Content(new VStack(8f) {
                    new Label("角色程序") { Role = LabelRole.Heading }.Height(32f),
                    new SearchableComboBox<Program>(string.Empty, selected, owner.xml.programs, p => p == null ? "（无）" : p.name,
                        delegate(Program program) { ChangeFemale(female, delegate {
                            bool old = female.programValid; female.SetProgram(program);
                            if (old != female.programValid) female.UpdateState(false, owner.nowMotion, owner.isFeraAction);
                        }); }, "running-program-choice") { FilterPlaceholder = "搜索程序" }.Height(Row),
                    new SliderFloat("切换间隔", female.programTimer, 1f, 20f, delegate(float value) {
                        ChangeFemale(female, delegate { bool old = female.programValid; female.programTimer = value; owner.SetProgramTimer(female);
                            if (old != female.programValid) female.UpdateState(false, owner.nowMotion, owner.isFeraAction); });
                    }, "program-timer").Height(Row),
                    new Label(female.programTimer <= 1f ? "程序已停用（间隔为 1）" : female.programTimer >= 20f ? "由场景事件触发表情" :
                        "倒计时 " + female.programTimerNow.ToString("0.0") + " / " + female.programTimer.ToString("0.0") + " 秒") { Role = LabelRole.Muted }.Height(Row),
                    new InputInt("绝顶次数", owner.orgazumCount, value => Change(delegate { owner.orgazumCount = value; }), "orgasm-count") {
                        HasMin = true, Min = 0, HasMax = true, Max = 10
                    }.Height(Row),
                    new HStack(6f) {
                        new Button("停止程序", delegate { ChangeFemale(female, delegate {
                            female.SetProgram(new Program()); female.UpdateState(false, owner.nowMotion, owner.isFeraAction);
                        }); }, "stop-program").ExpandWidth(),
                        new Button("编辑程序", delegate {
                            if (selected != null) { int index = owner.xml.programs.FindIndex(p => p.guid == selected.guid); if (index >= 0) owner.SelectProgram(index); }
                            page = 1; Invalidate();
                        }, "edit-program").ExpandWidth()
                    }.Height(Row)
                });
            }

            private Widget BuildProgramPage() {
                Program program = owner.currentProgram;
                owner.selectedProgramUnit = Math.Max(0, Math.Min(program.units.Count - 1, owner.selectedProgramUnit));
                Program.Unit unit = program.units.Count == 0 ? null : program.units[owner.selectedProgramUnit];
                List<Expression> expressions = FilteredExpressions();
                return new VStack(10f) {
                    new HStack(8f) {
                        new SearchableComboBox<Program>("程序", SavedProgramOrDraft(program), owner.xml.programs, p => p == null ? "（无）" : p.name,
                            p => Change(delegate { int index = owner.xml.programs.IndexOf(p); if (index >= 0) owner.SelectProgram(index); }), "program-choice") {
                            LabelWidth = 50f, FilterPlaceholder = "搜索程序"
                        }.ExpandWidth(),
                        new Button("空白程序", delegate { Change(delegate { owner.currentProgram = new Program(); owner.selectedProgramUnit = 0; }); }, "new-program").Width(90f),
                        new Button("另存为", delegate { SaveProgram(false); }, "program-save").Width(80f).Enabled(program.units.Count > 0 && CanWrite),
                        new Button("覆盖", delegate { SaveProgram(true); }, "program-overwrite").Width(65f).Enabled(!string.IsNullOrEmpty(program.guid) && CanWrite),
                        new Button("删除", DeleteProgram, "program-delete") { Variant = ButtonVariant.Danger }.Width(65f).Enabled(!string.IsNullOrEmpty(program.guid) && CanWrite)
                    }.Height(Row),
                    ExpressionProgramWidgets.BuildFlags(program, Invalidate),
                    new HStack(12f) {
                        new Panel("units").Content(new VStack(8f) {
                            new Label("程序单位") { Role = LabelRole.Heading }.Height(32f),
                            new VirtualList(program.units.Count, Row, delegate(int index) {
                                Program.Unit item = program.units[index];
                                return new Selectable((index + 1).ToString("D2") + "  ·  " + item.expressionGuids.Count + " 个表情",
                                    index == owner.selectedProgramUnit, v => Change(delegate { owner.selectedProgramUnit = program.units.IndexOf(item); }), "unit-" + index).Height(Row);
                            }, "unit-list") { Height = 250f }.ExpandWidth().ExpandHeight(),
                            new Button("新增单位", delegate { Change(owner.AddUnit); }, "unit-add").Height(Row),
                            new HStack(6f) {
                                new ArrowButton(ArrowDirection.Up, delegate { Change(delegate { owner.MoveUnit(true); }); }, "unit-up").ExpandWidth().Enabled(unit != null && owner.selectedProgramUnit > 0),
                                new ArrowButton(ArrowDirection.Down, delegate { Change(delegate { owner.MoveUnit(false); }); }, "unit-down").ExpandWidth().Enabled(unit != null && owner.selectedProgramUnit < program.units.Count - 1)
                            }.Height(Row),
                            new Button("复制到末尾", delegate { Change(delegate { owner.CopyUnit(false); }); }, "unit-copy").Height(Row).Enabled(unit != null),
                            new Button("复制到下方", delegate { Change(delegate { owner.CopyUnit(true); }); }, "unit-copy-below").Height(Row).Enabled(unit != null),
                            new Button("删除单位", delegate { Change(owner.DeleteUnit); }, "unit-delete") { Variant = ButtonVariant.Danger }.Height(Row).Enabled(unit != null)
                        }.ExpandHeight()).Width(164f).ExpandHeight(),
                        new Panel("conditions").Content(new ScrollView("unit-conditions-scroll").Content(
                            ExpressionProgramWidgets.BuildUnit(unit, Invalidate)).ExpandHeight()).ExpandWidth().ExpandHeight(),
                        new Panel("unit-expressions").Content(new VStack(8f) {
                            new Label("候选表情") { Role = LabelRole.Heading }.Height(32f),
                            new SearchBox(filter, v => Change(delegate { filter = v; }), "unit-expression-search") { Placeholder = "搜索表情" }.Height(Row),
                            new Checkbox("调整表情顺序", owner.isSortExpression, delegate(bool value) {
                                Change(delegate {
                                    if (!value && owner.isSortExpressionModify) { owner.xml.Save(true); owner.isSortExpressionModify = false; }
                                    owner.isSortExpression = value;
                                });
                            }, "sort-expressions").Height(Row).Enabled(CanWrite),
                            new VirtualList(expressions.Count, Row, delegate(int index) {
                                Expression expression = expressions[index];
                                if (!owner.isSortExpression) return ExpressionProgramWidgets.ExpressionChoice(unit, expression, Invalidate);
                                return new HStack(3f) {
                                    new ArrowButton(ArrowDirection.Up, delegate { ReorderExpression(expression, true); }).Width(25f),
                                    new ArrowButton(ArrowDirection.Down, delegate { ReorderExpression(expression, false); }).Width(25f),
                                    new Label(expression.name).ExpandWidth()
                                }.Height(Row);
                            }, "unit-expression-list") { Height = 300f }.ExpandHeight().ExpandWidth(),
                            new Label(unit == null ? "先选择一个程序单位" : "已选择 " + unit.expressionGuids.Count + " 个候选表情") { Role = LabelRole.Muted }.Height(Row)
                        }.ExpandHeight()).Width(235f).ExpandHeight()
                    }.ExpandHeight().ExpandWidth()
                }.ExpandHeight();
            }

            private void ReorderExpression(Expression expression, bool up) {
                Change(delegate {
                    int index = owner.xml.expressions.IndexOf(expression);
                    if (index >= 0) { owner.MoveExpression(index, up); filteredSource = null; }
                });
            }

            private Program SavedProgramOrDraft(Program program) {
                if (program == null || string.IsNullOrEmpty(program.guid)) return program;
                return owner.xml.programs.Find(p => p.guid == program.guid) ?? program;
            }

            private Widget BuildScenePage() {
                ScenePreset preset = owner.CurrentScenePreset();
                VStack roles = new VStack(8f);
                if (preset != null) {
                    for (int i = 0; i < preset.roles.Count; i++) {
                        SceneRolePreset role = preset.roles[i];
                        Program program = owner.xml.programs.Find(p => p.guid == role.programGuid);
                        roles.Add(new Panel("role-" + i).Content(new VStack(4f) {
                            new Label(role.role + "  ·  " + role.displayName) { Role = LabelRole.Heading }.Height(Row),
                            new Label("程序：" + (program == null ? "（无或已删除）" : program.name) + "    间隔：" + role.programTimer.ToString("0.0") + " 秒") { Role = LabelRole.Muted }.Height(Row)
                        }));
                    }
                } else roles.Add(new HelpBox("尚无场景预设。为各角色指定程序后，可以保存整个场景的分配。"));
                return new VStack(12f) {
                    new SearchableComboBox<ScenePreset>("场景预设", preset, owner.xml.scenePresets, p => p == null ? "（无）" : p.name,
                        p => Change(delegate { owner.SelectScenePreset(owner.xml.scenePresets.IndexOf(p)); }), "scene-choice") {
                        LabelWidth = 90f, FilterPlaceholder = "搜索场景"
                    }.Height(Row),
                    new HStack(8f) {
                        new Button("应用选中预设", delegate { Change(owner.ApplyCurrentScenePreset, "场景程序设置已应用。"); }, "scene-apply") { Variant = ButtonVariant.Primary }.ExpandWidth().Enabled(preset != null),
                        new Button("保存当前场景", delegate { SaveScene(false); }, "scene-save").ExpandWidth().Enabled(owner.femaleList.Count > 0 && CanWrite),
                        new Button("覆盖", delegate { SaveScene(true); }, "scene-overwrite").Width(80f).Enabled(preset != null && owner.femaleList.Count > 0 && CanWrite),
                        new Button("删除", DeleteScene, "scene-delete") { Variant = ButtonVariant.Danger }.Width(80f).Enabled(preset != null && CanWrite)
                    }.Height(Row),
                    new Label("场景预设按 F1 / F2 / M1 / M2 等角色位置保存程序和切换间隔。") { Role = LabelRole.Muted, WordWrap = true }.Height(36f),
                    new ScrollView("scene-roles").Content(roles.ExpandWidth()).ExpandHeight().ExpandWidth()
                }.ExpandHeight();
            }

            private Widget BuildSettingsPage() {
                if (shortcutDraft == null) shortcutDraft = owner.xml.keyConfig.showGUI;
                return new ScrollView("settings-scroll").Content(new VStack(12f) {
                    new Label("快捷键与预设文件") { Role = LabelRole.Heading }.Height(34f),
                    new Label("窗口快捷键（如 L、Ctrl+L；输入框编辑和模态对话框期间不会触发）") { WordWrap = true }.Height(38f),
                    new HStack(8f) {
                        new TextField(shortcutDraft, v => Change(delegate { shortcutDraft = v; }), "shortcut").ExpandWidth(),
                        new Button("保存快捷键", delegate { Change(delegate {
                            if (string.IsNullOrEmpty(shortcutDraft.Trim())) throw new InvalidOperationException("快捷键不能为空。");
                            owner.xml.keyConfig.showGUI = shortcutDraft.Trim(); owner.xml.Save(false);
                            owner.hotkey.Set(hotkeyShowGUI, owner.xml.keyConfig.showGUI);
                        }, "快捷键已保存。"); }, "shortcut-save").Width(120f).Enabled(CanWrite)
                    }.Height(Row),
                    new Separator(),
                    new Label(PresetPath) { WordWrap = true, Role = LabelRole.Muted }.MinHeight(50f),
                    new Button("从 XML 重新载入", delegate { Change(delegate { owner.ReloadMaximumUiPresets(); shortcutDraft = owner.xml.keyConfig.showGUI; }, "XML 已重新载入。"); }, "reload-presets").Height(Row),
                    new HelpBox("重新载入会用文件内容替换尚未保存的程序编辑。角色正在使用的表情与程序不会在读取过程中被清空。"),
                    new Label("MaximumUI · Metro") { Role = LabelRole.Heading }.Height(34f),
                    new Label("窗口可以拖动、缩放和折叠；长列表按可见行构建。点击标题栏的关闭按钮或再次按快捷键隐藏窗口。") { WordWrap = true }.MinHeight(50f)
                }.ExpandWidth()).ExpandHeight();
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
                Dialog dialog = new Dialog(title, "expression-name-dialog") { Width = 480f, Height = 235f };
                TextField input = new TextField(text, key: "preset-name") { AutoFocus = true };
                input.OnChange = delegate(string value) { text = value; input.Value = value; };
                Action submit = delegate {
                    string name = text.Trim();
                    if (name.Length == 0) { errorLabel.Text = "请输入名称。"; return; }
                    try {
                        if (!save(name)) { errorLabel.Text = "未能保存，请检查名称是否重复以及当前选择是否仍然有效。"; return; }
                        context.Dialogs.Close(dialog); status = "已保存：" + name; Invalidate();
                    } catch (Exception error) { errorLabel.Text = ErrorText(error); Debug.LogException(error); }
                };
                input.OnSubmit = delegate { submit(); };
                dialog.Add(new VStack(10f) {
                    new Label(title).Height(Row), input.Height(Row), errorLabel.Height(45f),
                    new HStack(8f) {
                        new Spacer(),
                        new Button("取消", delegate { context.Dialogs.Close(dialog); }).Width(90f),
                        new Button("保存", submit) { Variant = ButtonVariant.Primary }.Width(90f)
                    }.Height(Row)
                });
                context.Dialogs.Show(dialog);
            }

            private void SaveExpression(FemaleData female, bool overwrite) {
                if (female == null) return;
                Prompt(overwrite ? "覆盖表情预设" : "保存新表情", overwrite ? female.expression.name : string.Empty, delegate(string name) {
                    if (!ReferenceEquals(CurrentFemale(), female)) return false;
                    return overwrite ? owner.XML_UpdateExpression(female.expression.guid, name) : owner.XML_SaveNewExpression(name);
                });
            }

            private void DeleteExpression(FemaleData female) {
                if (female == null || context == null || context.Dialogs == null || !CanWrite) return;
                string guid = female.expression.guid;
                context.Dialogs.Confirm("删除表情", "删除「" + female.expression.name + "」及程序中对它的引用？", delegate {
                    Change(delegate {
                        if (!ReferenceEquals(CurrentFemale(), female) || female.expression.guid != guid) return;
                        if (!owner.XML_DeleteExpression(guid)) throw new InvalidOperationException("所选表情预设已不存在。");
                    }, "表情预设已删除。");
                });
            }

            private void SaveProgram(bool overwrite) {
                Program program = owner.currentProgram;
                Prompt(overwrite ? "覆盖程序" : "保存新程序", overwrite ? program.name : string.Empty, delegate(string name) {
                    if (!ReferenceEquals(owner.currentProgram, program)) return false;
                    bool saved = overwrite ? owner.XML_UpdateProgram(name) : owner.XML_SaveNewProgram(name);
                    if (saved) owner.currentProgramNum = owner.xml.programs.FindIndex(p => p.guid == program.guid);
                    return saved;
                });
            }

            private void DeleteProgram() {
                if (context == null || context.Dialogs == null || !CanWrite) return;
                Program program = owner.currentProgram;
                context.Dialogs.Confirm("删除程序", "删除「" + program.name + "」？", delegate {
                    Change(delegate { if (ReferenceEquals(owner.currentProgram, program)) owner.XML_DeleteProgram(); }, "程序已删除。");
                });
            }

            private void SaveScene(bool overwrite) {
                ScenePreset preset = owner.CurrentScenePreset();
                Prompt(overwrite ? "覆盖场景预设" : "保存当前场景", overwrite && preset != null ? preset.name : string.Empty,
                    delegate(string name) {
                        if (overwrite && !ReferenceEquals(owner.CurrentScenePreset(), preset)) return false;
                        return overwrite ? owner.XML_UpdateScenePreset(name) : owner.XML_SaveNewScenePreset(name);
                    });
            }

            private void DeleteScene() {
                if (context == null || context.Dialogs == null || !CanWrite) return;
                ScenePreset preset = owner.CurrentScenePreset();
                if (preset == null) return;
                context.Dialogs.Confirm("删除场景预设", "删除「" + preset.name + "」？", delegate {
                    Change(delegate { if (ReferenceEquals(owner.CurrentScenePreset(), preset)) owner.XML_DeleteScenePreset(); }, "场景预设已删除。");
                });
            }
        }
    }
}
