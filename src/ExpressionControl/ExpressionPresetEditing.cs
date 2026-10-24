using System;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private bool XML_DeleteExpression(string guid) {
            if (string.IsNullOrEmpty(guid)) return false;
            int removed = xml.expressions.RemoveAll(expression => expression.guid == guid);
            if (removed == 0) return false;

            // A preset may be used by many units, by the unsaved editor copy,
            // and by multiple active characters. Remove every reference so a
            // later save or character switch cannot resurrect a deleted ID.
            foreach (Program program in xml.programs) RemoveExpressionReferences(program, guid);
            RemoveExpressionReferences(currentProgram, guid);
            foreach (FemaleData female in femaleList) {
                if (female == null) continue;
                RemoveExpressionReferences(female.program, guid);
                ClearDeletedIdentity(female.expression, guid);
                ClearDeletedIdentity(female.expressionRealtime, guid);
            }
            xml.Save(true);
            return true;
        }

        private static void RemoveExpressionReferences(Program program, string guid) {
            if (program == null || program.units == null) return;
            foreach (Program.Unit unit in program.units) {
                if (unit == null || unit.expressionGuids == null) continue;
                unit.expressionGuids.RemoveAll(value => value == guid);
            }
        }

        private static void ClearDeletedIdentity(Expression expression, string guid) {
            if (expression == null || expression.guid != guid) return;
            expression.guid = string.Empty;
            expression.name = "- - -";
        }
    }
}
