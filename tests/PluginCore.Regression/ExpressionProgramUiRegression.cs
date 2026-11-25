using System;
using System.Collections.Generic;
using MaximumUI;
using EC = ExpressionControl.ExpressionControl;
using UI = ExpressionControl.ExpressionProgramWidgets;

internal static class ExpressionProgramUiRegression {
    internal static void Run(Action<string, Action> run) {
        run("Expression program UI exposes every condition and controlled part", Conditions);
        run("Expression program UI callbacks modify the captured unit only", CapturedUnit);
        run("Expression program UI range controls preserve min max ordering", Ranges);
        run("Expression program UI flags update real serialized fields", Flags);
        run("Expression choice deselection removes legacy duplicates", Duplicates);
        run("Expression choice selection preserves unrelated preset references", Selection);
        run("Expression search preserves preset identity and order", Filter);
        run("Expression program UI empty selection is a usable empty state", Empty);
    }

    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Find<T>(Widget root, string key) where T : Widget {
        if (root is T && Equals(root.Key, key)) return (T)root;
        for (int i = 0; i < root.Children.Count; i++) {
            T match = Find<T>(root.Children[i], key);
            if (match != null) return match;
        }
        return null;
    }

    private static void Conditions() {
        var unit = new EC.Program.Unit(); int changes = 0;
        Widget tree = UI.BuildUnit(unit, delegate { changes++; });
        string[] fields = { "aibu", "houshi", "sounyuu", "fera", "masturbation", "lesbian", "idle", "kiss", "loopW", "loopS",
            "spurtM", "spurtF", "orgazumM", "orgazumF", "afterOrgazumM", "afterOrgazumF", "drinkVomitWait",
            "morph", "tear", "hoho", "highlight", "eyeY", "eyeSmall" };
        foreach (string field in fields) {
            Checkbox control = Find<Checkbox>(tree, field);
            Assert(control != null, "missing editor field " + field);
            control.OnChange(true);
            Assert((bool)typeof(EC.Program.Unit).GetField(field).GetValue(unit), "wrong callback target " + field);
        }
        Assert(changes == fields.Length, "updates did not invalidate retained tree");
    }

    private static void CapturedUnit() {
        var selected = new EC.Program.Unit(); var next = new EC.Program.Unit();
        bool editable = true;
        Widget first = UI.BuildUnit(selected, delegate { }, "", () => editable);
        UI.BuildUnit(next, delegate { });
        Find<Checkbox>(first, "morph").OnChange(true);
        Assert(selected.morph && !next.morph, "deferred callback changed a different selected unit");
        editable = false;
        Find<Checkbox>(first, "morph").OnChange(false);
        Find<TextField>(first, "feeling-min").OnSubmit("50");
        Assert(selected.morph && selected.feelingL == 0, "removed or switched program still accepted a queued unit edit");
    }

    private static void Ranges() {
        var unit = new EC.Program.Unit { feelingL = 10, feelingH = 70, orgazumNumL = 2, orgazumNumH = 7 };
        Widget tree = UI.BuildUnit(unit, delegate { });
        Find<TextField>(tree, "feeling-min").OnSubmit("90");
        Assert(unit.feelingL == 70 && unit.feelingH == 70, "minimum crossed maximum");
        Find<TextField>(tree, "feeling-max").OnBlur("1");
        Assert(unit.feelingH == 70, "maximum crossed current minimum");
        Find<TextField>(tree, "feeling-max").OnSubmit("1000");
        Assert(unit.feelingH == 100, "feeling cap ignored");
        Find<TextField>(tree, "orgasm-min").OnSubmit("-99");
        Find<TextField>(tree, "orgasm-max").OnBlur("99");
        Assert(unit.orgazumNumL == 0 && unit.orgazumNumH == 10, "orgasm bounds ignored");
        Find<TextField>(tree, "feeling-min").OnSubmit("not a number");
        Assert(unit.feelingL == 70 && Find<TextField>(tree, "feeling-min").Value == "70", "invalid text changed or hid the last valid value");
    }

    private static void Flags() {
        var program = new EC.Program(); int changes = 0;
        Widget tree = UI.BuildFlags(program, delegate { changes++; });
        Find<Checkbox>(tree, "apply-mouth").OnChange(true);
        Find<Checkbox>(tree, "ignore-fera").OnChange(true);
        Find<Checkbox>(tree, "ignore-kiss").OnChange(true);
        Assert(program.applyMouthOpen && program.ignoreMouthInFera && program.ignoreKiss && changes == 3, "program options not bound");
    }

    private static void Duplicates() {
        var unit = new EC.Program.Unit(); unit.expressionGuids.AddRange(new[] { "same", "other", "same" });
        Checkbox choice = (Checkbox)UI.ExpressionChoice(unit, new EC.Expression { guid = "same", name = "A" }, delegate { });
        choice.OnChange(false);
        Assert(unit.expressionGuids.Count == 1 && unit.expressionGuids[0] == "other", "unchecking left duplicate preset links");
    }

    private static void Selection() {
        var unit = new EC.Program.Unit(); unit.expressionGuids.Add("other"); int changes = 0;
        Checkbox choice = (Checkbox)UI.ExpressionChoice(unit, new EC.Expression { guid = "new", name = "A" }, delegate { changes++; });
        choice.OnChange(true); choice.OnChange(true);
        Assert(unit.expressionGuids.Count == 2 && unit.expressionGuids[0] == "other" && changes == 2, "selection replaced or duplicated references");
    }

    private static void Filter() {
        var first = new EC.Expression { guid = "1", name = "Smile A" };
        var second = new EC.Expression { guid = "2", name = "微笑" };
        var third = new EC.Expression { guid = "3", name = "SMILE B" };
        IList<EC.Expression> source = new List<EC.Expression> { first, second, third };
        List<EC.Expression> result = UI.Filter(source, " smile ");
        Assert(result.Count == 2 && ReferenceEquals(result[0], first) && ReferenceEquals(result[1], third), "filter lost stable identity/order");
        Assert(UI.Filter(source, "微笑")[0] == second && UI.Filter(source, "").Count == 3 && source.Count == 3, "Unicode/clear filtering mutated the source");
    }

    private static void Empty() {
        Assert(UI.BuildUnit(null, delegate { }) is HelpBox, "empty program lacks guidance");
        var choice = (Checkbox)UI.ExpressionChoice(null, new EC.Expression { guid = "a" }, delegate { throw new Exception("disabled callback invoked"); });
        Assert(!choice.Enabled, "empty unit allowed selection");
        choice.OnChange(true);
    }
}
