// Export the actual production View using synthetic data only. This factory is
// consumed by the SVG preview tool; it never opens a user's configuration file.
using System.Collections.Generic;
using ExpressionControl;
using MaximumUI;
using EC = ExpressionControl.ExpressionControl;

public static class ExpressionEditorPreviewFactory {
    public static Widget CreatePreview() { return Create(false); }
    public static Widget CreateProgramPreview() { return Create(true); }

    private static Widget Create(bool programPage) {
        var owner = new EC { xml = new XmlMgr(), hotkey = new ShortCutKeyMgr() };
        string[] names = { "自然", "微笑", "闭眼", "惊讶", "凝视", "轻笑", "困倦", "专注" };
        for (int i = 0; i < 32; i++) owner.xml.expressions.Add(new EC.Expression {
            guid = "preview-expression-" + i, name = names[i % names.Length] + " " + (i / names.Length + 1),
            eyebrowPtn = i % 4, eyesPtn = i % 6, mouthPtn = i % 3, eyeOpen = 0.8f, blink = true, mouthOpen = 0.1f
        });
        var program = new EC.Program { guid = "preview-program", name = "自然表情", applyMouthOpen = true, ignoreMouthInFera = true };
        for (int i = 0; i < 6; i++) program.units.Add(new EC.Program.Unit {
            aibu = true, houshi = true, sounyuu = true, idle = true, loopW = true, morph = true, highlight = true,
            feelingL = i * 10, feelingH = 100, orgazumNumH = 10,
            expressionGuids = new List<string> { "preview-expression-" + i, "preview-expression-" + (i + 8) }
        });
        owner.xml.programs.Add(program); owner.currentProgram = program.DeepClone();
        for (int i = 0; i < 2; i++) {
            var character = new ChaControl(); character.chaFile.parameter.fullname = "示例角色 " + (i == 0 ? "A" : "B");
            owner.femaleList.Add(new EC.FemaleData {
                female = character, expression = new EC.Expression(owner.xml.expressions[1]),
                expressionRealtime = new EC.Expression(owner.xml.expressions[1]), program = program,
                programValid = true, programTimer = 7f, programTimerNow = 4.5f
            });
        }
        var view = new EC.ExpressionEditorView(owner, programPage);
        var context = new BuildContext { Dialogs = new DialogService() };
        return view.Build(context);
    }
}
