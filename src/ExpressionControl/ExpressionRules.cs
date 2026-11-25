using System;
using System.Collections.Generic;

namespace ExpressionControl {
    internal static class ExpressionRules {
        internal static bool ControlsPart(ExpressionControl.Program.Unit unit, ExpressionControl.ExpressionPart part) {
            if (unit == null) return false;
            switch (part) {
                case ExpressionControl.ExpressionPart.morph: return unit.morph;
                case ExpressionControl.ExpressionPart.tear: return unit.tear;
                case ExpressionControl.ExpressionPart.hoho: return unit.hoho;
                case ExpressionControl.ExpressionPart.highlight: return unit.highlight;
                case ExpressionControl.ExpressionPart.eyeY: return unit.eyeY;
                case ExpressionControl.ExpressionPart.eyeSmall: return unit.eyeSmall;
                default: return false;
            }
        }

        internal static string ChooseExpressionGuid(IList<ExpressionControl.Program.Unit> units,
            ExpressionControl.ExpressionPart part, IList<ExpressionControl.Expression> expressions, Func<int, int> chooseIndex) {
            if (units == null || expressions == null) return null;
            HashSet<string> knownExpressions = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExpressionControl.Expression expression in expressions) {
                if (expression != null && !string.IsNullOrEmpty(expression.guid)) knownExpressions.Add(expression.guid);
            }
            List<List<string>> candidates = new List<List<string>>();
            foreach (ExpressionControl.Program.Unit unit in units) {
                if (!ControlsPart(unit, part) || unit.expressionGuids == null) continue;
                List<string> valid = new List<string>();
                foreach (string guid in unit.expressionGuids) {
                    if (!string.IsNullOrEmpty(guid) && knownExpressions.Contains(guid)) valid.Add(guid);
                }
                if (valid.Count > 0) candidates.Add(valid);
            }
            if (candidates.Count == 0) return null;
            // Preserve the existing equal-unit, then equal-expression weighting.
            List<string> selected = candidates[chooseIndex(candidates.Count)];
            return selected[chooseIndex(selected.Count)];
        }

        internal static bool MatchesState(ExpressionControl.Program.Unit unit, ExpressionControl.FemaleState state) {
            if (unit == null) return false;
            switch (state) {
                case ExpressionControl.FemaleState.idle: return unit.idle;
                case ExpressionControl.FemaleState.kiss: return unit.kiss;
                case ExpressionControl.FemaleState.loopW: return unit.loopW;
                case ExpressionControl.FemaleState.loopS: return unit.loopS;
                case ExpressionControl.FemaleState.spurtM: return unit.spurtM;
                case ExpressionControl.FemaleState.spurtF: return unit.spurtF;
                case ExpressionControl.FemaleState.orgazumM: return unit.orgazumM;
                case ExpressionControl.FemaleState.orgazumF: return unit.orgazumF;
                case ExpressionControl.FemaleState.afterOrgazumM: return unit.afterOrgazumM;
                case ExpressionControl.FemaleState.afterOrgazumF: return unit.afterOrgazumF;
                case ExpressionControl.FemaleState.drinkVomitWait: return unit.drinkVomitWait;
                default: return false;
            }
        }
    }
}
