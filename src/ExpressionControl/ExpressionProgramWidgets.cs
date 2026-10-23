using System;
using System.Collections.Generic;
using MaximumUI;

namespace ExpressionControl {
    // The production program editor only needs the serialized program model.
    // It can be exercised without a character or the Unity drawing backend.
    internal static class ExpressionProgramWidgets {
        internal static Widget BuildUnit(ExpressionControl.Program.Unit unit, Action changed) {
            if (unit == null) return new HelpBox("选择或新增一个程序单位。");
            return new VStack(gap: 10f, key: "unit-editor") {
                new Label("动作类型") { Role = LabelRole.Heading }.Height(30f),
                new Grid(3, 5f, 8f) {
                    Check("爱抚", "aibu", unit.aibu, v => unit.aibu = v, changed),
                    Check("侍奉", "houshi", unit.houshi, v => unit.houshi = v, changed),
                    Check("插入", "sounyuu", unit.sounyuu, v => unit.sounyuu = v, changed),
                    Check("口交", "fera", unit.fera, v => unit.fera = v, changed),
                    Check("自慰", "masturbation", unit.masturbation, v => unit.masturbation = v, changed),
                    Check("百合", "lesbian", unit.lesbian, v => unit.lesbian = v, changed)
                },
                new Separator(),
                new Label("动作状态") { Role = LabelRole.Heading }.Height(30f),
                new Grid(3, 5f, 8f) {
                    Check("待机", "idle", unit.idle, v => unit.idle = v, changed),
                    Check("接吻", "kiss", unit.kiss, v => unit.kiss = v, changed),
                    Check("弱循环", "loopW", unit.loopW, v => unit.loopW = v, changed),
                    Check("强循环", "loopS", unit.loopS, v => unit.loopS = v, changed),
                    Check("男冲刺", "spurtM", unit.spurtM, v => unit.spurtM = v, changed),
                    Check("女冲刺", "spurtF", unit.spurtF, v => unit.spurtF = v, changed),
                    Check("男绝顶", "orgazumM", unit.orgazumM, v => unit.orgazumM = v, changed),
                    Check("女绝顶", "orgazumF", unit.orgazumF, v => unit.orgazumF = v, changed),
                    Check("男绝顶后", "afterOrgazumM", unit.afterOrgazumM, v => unit.afterOrgazumM = v, changed),
                    Check("女绝顶后", "afterOrgazumF", unit.afterOrgazumF, v => unit.afterOrgazumF = v, changed),
                    Check("精饮待机", "drinkVomitWait", unit.drinkVomitWait, v => unit.drinkVomitWait = v, changed)
                },
                new Separator(),
                new Label("触发范围") { Role = LabelRole.Heading }.Height(30f),
                new HStack(gap: 8f) {
                    Number("敏感度下限", "feeling-min", unit.feelingL, 0, 100, delegate(int v) {
                        unit.feelingL = Clamp(v, 0, unit.feelingH); changed();
                    }).ExpandWidth(),
                    Number("上限", "feeling-max", unit.feelingH, 0, 100, delegate(int v) {
                        unit.feelingH = Clamp(v, unit.feelingL, 100); changed();
                    }).ExpandWidth()
                }.Height(30f),
                new HStack(gap: 8f) {
                    Number("绝顶次数下限", "orgasm-min", unit.orgazumNumL, 0, 10, delegate(int v) {
                        unit.orgazumNumL = Clamp(v, 0, unit.orgazumNumH); changed();
                    }).ExpandWidth(),
                    Number("上限", "orgasm-max", unit.orgazumNumH, 0, 10, delegate(int v) {
                        unit.orgazumNumH = Clamp(v, unit.orgazumNumL, 10); changed();
                    }).ExpandWidth()
                }.Height(30f),
                new Separator(),
                new Label("应用部位") { Role = LabelRole.Heading }.Height(30f),
                new Grid(3, 5f, 8f) {
                    Check("形态", "morph", unit.morph, v => unit.morph = v, changed),
                    Check("泪", "tear", unit.tear, v => unit.tear = v, changed),
                    Check("脸红", "hoho", unit.hoho, v => unit.hoho = v, changed),
                    Check("高光", "highlight", unit.highlight, v => unit.highlight = v, changed),
                    Check("瞳孔 Y", "eyeY", unit.eyeY, v => unit.eyeY = v, changed),
                    Check("缩瞳", "eyeSmall", unit.eyeSmall, v => unit.eyeSmall = v, changed)
                }
            }.ExpandWidth();
        }

        internal static Widget BuildFlags(ExpressionControl.Program program, Action changed) {
            return new Grid(3, 6f, 8f, "program-flags") {
                Check("应用嘴巴开合", "apply-mouth", program.applyMouthOpen, v => program.applyMouthOpen = v, changed),
                Check("口交时不应用口型", "ignore-fera", program.ignoreMouthInFera, v => program.ignoreMouthInFera = v, changed),
                Check("接吻时禁用程序", "ignore-kiss", program.ignoreKiss, v => program.ignoreKiss = v, changed)
            };
        }

        internal static Widget ExpressionChoice(ExpressionControl.Program.Unit unit,
            ExpressionControl.Expression expression, Action changed) {
            return new Checkbox(expression.name, unit != null && unit.expressionGuids.Contains(expression.guid), delegate(bool value) {
                if (unit == null) return;
                SetExpressionSelected(unit, expression.guid, value);
                changed();
            }, "unit-expression-" + expression.guid).Height(28f).ExpandWidth().Enabled(unit != null);
        }

        internal static void SetExpressionSelected(ExpressionControl.Program.Unit unit, string guid, bool selected) {
            if (unit == null || string.IsNullOrEmpty(guid)) return;
            if (selected) {
                if (!unit.expressionGuids.Contains(guid)) unit.expressionGuids.Add(guid);
            } else {
                // Older hand-edited files may contain the same expression twice.
                while (unit.expressionGuids.Remove(guid)) { }
            }
        }

        internal static List<ExpressionControl.Expression> Filter(IList<ExpressionControl.Expression> source, string query) {
            List<ExpressionControl.Expression> result = new List<ExpressionControl.Expression>();
            string filter = (query ?? string.Empty).Trim();
            for (int i = 0; i < source.Count; i++) {
                ExpressionControl.Expression expression = source[i];
                if (expression != null && (filter.Length == 0 ||
                    (expression.name ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)) result.Add(expression);
            }
            return result;
        }

        private static Checkbox Check(string label, string key, bool value, Action<bool> apply, Action changed) {
            return new Checkbox(label, value, delegate(bool next) { apply(next); changed(); }, key).Height(28f).ExpandWidth();
        }

        private static InputInt Number(string label, string key, int value, int min, int max, Action<int> apply) {
            return new InputInt(label, value, apply, key) { HasMin = true, Min = min, HasMax = true, Max = max, StepFast = 5 };
        }

        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
