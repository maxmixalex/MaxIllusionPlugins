using MaximumUI;

internal sealed class PreviewIssue {
    public string Scene { get; private set; }
    public string Kind { get; private set; }
    public string Path { get; private set; }
    public string Detail { get; private set; }
    internal PreviewIssue(string scene, string kind, string path, string detail) {
        Scene = scene; Kind = kind; Path = path; Detail = detail;
    }
}

internal readonly struct PreviewTextBox {
    internal readonly LayoutNode Owner;
    internal readonly MuiRect Rect;
    internal readonly string Text;
    internal readonly int Layer;
    internal PreviewTextBox(LayoutNode owner, MuiRect rect, string text, int layer) { Owner = owner; Rect = rect; Text = text; Layer = layer; }
}
