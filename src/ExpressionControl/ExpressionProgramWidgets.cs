using System;
using System.Collections.Generic;
using MaximumUI;

namespace ExpressionControl {
    // The production program editor only needs the serialized program model.
    // It can be exercised without a character or the Unity drawing backend.
    internal static class ExpressionProgramWidgets {
        internal static Widget BuildUnit(ExpressionControl.Program.Unit unit, Action changed,
            string prefix = "", Func<bool> canEdit = null) {
            if (unit == null) return new HelpBox("选择或新增一个程序单位。");
            // The original editor shows every unit as one horizontal condition
            // panel. Keep the columns and row ordering, rather than requiring
            // selection in a separate master/detail editor.
            return new HStack(8f, prefix + "unit-editor") {
                new VStack(0f) {
                    Check("爱抚", "aibu", unit.aibu, v => unit.aibu = v, changed, prefix, canEdit),
                    Check("侍奉", "houshi", unit.houshi, v => unit.houshi = v, changed, prefix, canEdit),
                    Check("插入", "sounyuu", unit.sounyuu, v => unit.sounyuu = v, changed, prefix, canEdit),
                    Check("口交", "fera", unit.fera, v => unit.fera = v, changed, prefix, canEdit),
                    Check("自慰", "masturbation", unit.masturbation, v => unit.masturbation = v, changed, prefix, canEdit),
                    Check("百合", "lesbian", unit.lesbian, v => unit.lesbian = v, changed, prefix, canEdit)
                }.Width(64f),
                new VStack(0f) {
                    Check("待机", "idle", unit.idle, v => unit.idle = v, changed, prefix, canEdit),
                    Check("接吻", "kiss", unit.kiss, v => unit.kiss = v, changed, prefix, canEdit),
                    Check("弱循环", "loopW", unit.loopW, v => unit.loopW = v, changed, prefix, canEdit),
                    Check("强循环", "loopS", unit.loopS, v => unit.loopS = v, changed, prefix, canEdit)
                }.Width(72f),
                new VStack(0f) {
                    Check("男冲刺", "spurtM", unit.spurtM, v => unit.spurtM = v, changed, prefix, canEdit),
                    Check("女冲刺", "spurtF", unit.spurtF, v => unit.spurtF = v, changed, prefix, canEdit),
                    Check("男绝顶", "orgazumM", unit.orgazumM, v => unit.orgazumM = v, changed, prefix, canEdit),
                    Check("女绝顶", "orgazumF", unit.orgazumF, v => unit.orgazumF = v, changed, prefix, canEdit)
                }.Width(80f),
                new VStack(0f) {
                    Check("男绝顶后", "afterOrgazumM", unit.afterOrgazumM, v => unit.afterOrgazumM = v, changed, prefix, canEdit),
                    Check("女绝顶后", "afterOrgazumF", unit.afterOrgazumF, v => unit.afterOrgazumF = v, changed, prefix, canEdit),
                    Check("精饮待机", "drinkVomitWait", unit.drinkVomitWait, v => unit.drinkVomitWait = v, changed, prefix, canEdit)
                }.Width(88f),
                new VStack(0f) {
                    new Label("敏感度:").Height(28f),
                    new HStack(4f) {
                        Number("feeling-min", () => unit.feelingL, v => unit.feelingL = Clamp(v, 0, unit.feelingH), changed, prefix, canEdit).ExpandWidth(),
                        new Label("～").Width(20f),
                        Number("feeling-max", () => unit.feelingH, v => unit.feelingH = Clamp(v, unit.feelingL, 100), changed, prefix, canEdit).ExpandWidth()
                    }.Height(28f),
                    new Label("绝顶次数:").Height(28f),
                    new HStack(4f) {
                        Number("orgasm-min", () => unit.orgazumNumL, v => unit.orgazumNumL = Clamp(v, 0, unit.orgazumNumH), changed, prefix, canEdit).ExpandWidth(),
                        new Label("～").Width(20f),
                        Number("orgasm-max", () => unit.orgazumNumH, v => unit.orgazumNumH = Clamp(v, unit.orgazumNumL, 10), changed, prefix, canEdit).ExpandWidth()
                    }.Height(28f)
                }.Width(140f),
                new VStack(0f) {
                    Check("形态", "morph", unit.morph, v => unit.morph = v, changed, prefix, canEdit),
                    Check("泪", "tear", unit.tear, v => unit.tear = v, changed, prefix, canEdit),
                    Check("脸红", "hoho", unit.hoho, v => unit.hoho = v, changed, prefix, canEdit),
                    Check("高光", "highlight", unit.highlight, v => unit.highlight = v, changed, prefix, canEdit)
                }.Width(64f),
                new VStack(0f) {
                    Check("瞳孔Y", "eyeY", unit.eyeY, v => unit.eyeY = v, changed, prefix, canEdit),
                    Check("缩瞳", "eyeSmall", unit.eyeSmall, v => unit.eyeSmall = v, changed, prefix, canEdit)
                }.Width(78f)
            }.ExpandWidth();
        }

        internal static Widget BuildFlags(ExpressionControl.Program program, Action changed, Func<bool> canEdit = null) {
            return new VStack(4f, "program-flags") {
                new HStack(12f) {
                    Check("应用嘴巴开合", "apply-mouth", program.applyMouthOpen, v => program.applyMouthOpen = v, changed, "", canEdit).Width(150f),
                    Check("口交时不应用口型", "ignore-fera", program.ignoreMouthInFera, v => program.ignoreMouthInFera = v, changed, "", canEdit).Width(186f)
                }.Height(28f),
                Check("接吻时禁用程序", "ignore-kiss", program.ignoreKiss, v => program.ignoreKiss = v, changed, "", canEdit).Height(28f)
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

        private static Checkbox Check(string label, string key, bool value, Action<bool> apply, Action changed,
            string prefix, Func<bool> canEdit) {
            return new Checkbox(label, value, delegate(bool next) {
                if (canEdit != null && !canEdit()) return;
                apply(next); changed();
            }, prefix + key).Height(28f).ExpandWidth();
        }

        private static TextField Number(string key, Func<int> current, Action<int> apply, Action changed,
            string prefix, Func<bool> canEdit) {
            TextField input = new TextField(current().ToString(), key: prefix + key) { Flags = TextInputFlags.Decimal };
            Action<string> commit = delegate(string text) {
                if (canEdit != null && !canEdit()) return;
                int parsed;
                apply(int.TryParse(text, out parsed) ? parsed : current());
                input.Value = current().ToString();
                changed();
            };
            input.OnSubmit = commit;
            input.OnBlur = commit;
            return input;
        }

        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
