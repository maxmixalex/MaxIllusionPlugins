using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Xml;
using MaximumUI;

// SVG backend for the real Gallery and production plugin View trees.
// No MaximumTheme, Unity host, game state or user configuration is initialized.
internal static class Program {
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private const string Disclosure = "Managed drawing preview; Unity font/input not validated.";

    private static int Main(string[] args) {
        if (args.Length != 2) {
            Console.Error.WriteLine("Usage: MaximumUI.Preview <KK_MaximumUIGallery.dll> <output-directory>");
            return 2;
        }

        try {
            string galleryPath = Path.GetFullPath(args[0]);
            string directory = Path.GetDirectoryName(galleryPath);
            AssemblyLoadContext.Default.Resolving += (context, name) => {
                string local = Path.Combine(directory, name.Name + ".dll");
                return File.Exists(local) ? context.LoadFromAssemblyPath(local) : null;
            };
            Assembly gallery = AssemblyLoadContext.Default.LoadFromAssemblyPath(galleryPath);
            Directory.CreateDirectory(args[1]);
            List<PreviewScene> scenes = new List<PreviewScene> {
                Gallery(gallery, ThemePalettes.MetroDark(), "Metro Dark", "dark"),
                Gallery(gallery, ThemePalettes.MetroLight(), "Metro Light", "light")
            };
            scenes.AddRange(PluginPreviewScenes.Create());
            AddHostScenes(scenes, directory);
            List<object> manifest = new List<object>();
            foreach (PreviewScene scene in scenes) manifest.Add(Export(scene, args[1]));
            File.WriteAllText(Path.Combine(args[1], "preview-manifest.json"),
                JsonSerializer.Serialize(new { disclosure = Disclosure, previews = manifest },
                    new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error is TargetInvocationException && error.InnerException != null
                ? error.InnerException : error);
            return 1;
        }
    }

    private static PreviewScene Gallery(Assembly gallery, ThemeTokens palette, string name, string suffix) {
        Type viewType = gallery.GetType("MaximumUIGallery.GalleryView", true);
        View view = (View)Activator.CreateInstance(viewType, true);
        FieldInfo stateField = viewType.BaseType.GetField("State", BindingFlags.Instance | BindingFlags.NonPublic);
        object state = stateField.GetValue(view);
        state.GetType().GetField("ThemeName").SetValue(state, name);
        state.GetType().GetField("EditableTheme").SetValue(state, palette.Clone());

        // This is the production default Overview, not a second copy of its UI.
        // Services remain null: Build registers no shortcuts and no callback runs.
        return new PreviewScene {
            Slug = "maximumui-metro-" + suffix + "-preview", Title = "MaximumUI / " + name,
            Source = "MaximumUIGallery.GalleryView.Build", Detail = "Production default Overview",
            Widget = view.Build(new BuildContext()), Palette = palette, PaletteName = name,
            Width = 940f, Height = 840f, Gallery = true
        };
    }

    private static void AddHostScenes(List<PreviewScene> scenes, string galleryDirectory) {
        AddHostScene(scenes, galleryDirectory, "PluginCore.Regression", "ExpressionEditorPreviewFactory", "CreatePreview",
            "expressioncontrol-expressions-preview", "ExpressionControl / 表情编辑", "ExpressionControl.ExpressionControl.ExpressionEditorView.Build");
        AddHostScene(scenes, galleryDirectory, "PluginCore.Regression", "ExpressionEditorPreviewFactory", "CreateProgramPreview",
            "expressioncontrol-program-preview", "ExpressionControl / 程序规则", "ExpressionControl.ExpressionControl.ExpressionEditorView.Build");
        AddHostScene(scenes, galleryDirectory, "AccessoryClothing.Regression", "CompressionEditorPreview", "CreatePreview",
            "clothingcompression-editor-preview", "ClothingCompression / 服装压缩", "ClothingCompression.CompressionEditorView.Build");
    }

    private static void AddHostScene(List<PreviewScene> scenes, string galleryDirectory, string project,
        string typeName, string methodName, string slug, string title, string source) {
        string path = Path.GetFullPath(Path.Combine(galleryDirectory, "..", project, project + ".dll"));
        if (!File.Exists(path)) {
            Console.WriteLine("Optional " + slug + " skipped: build tests/" + project + " first.");
            return;
        }
        Assembly host = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        Type factory = host.GetType(typeName, true);
        MethodInfo method = factory.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null) throw new InvalidOperationException(typeName + "." + methodName + " is missing.");
        Widget widget = method.Invoke(null, null) as Widget;
        if (widget == null) throw new InvalidOperationException(typeName + "." + methodName + " did not return a real Widget.");
        scenes.Add(new PreviewScene {
            Slug = slug, Title = title, Source = source, Detail = "Anonymous fixture from " + typeName + "." + methodName,
            Widget = widget
        });
    }

    private static object Export(PreviewScene scene, string directory) {
        Window window = scene.Widget as Window;
        if (window == null) throw new InvalidOperationException(scene.Source + " must build a Window.");
        float width = scene.Width > 0f ? scene.Width : window.Width;
        float height = scene.Height > 0f ? scene.Height : window.Height;
        ThemeTokens palette = scene.Palette;
        ManagedPreviewLayout preview = ManagedPreviewLayout.Create(scene.Widget, new MuiRect(0f, 0f, width, height), PreviewMetrics(palette), palette);
        List<LayoutNode> nodes = preview.Nodes;
        VerifyScene(scene, nodes);
        DrawList drawing = preview.Drawing;
        if (drawing.Commands.Count == 0) throw new InvalidOperationException("The production painter returned no commands.");

        string file = Path.Combine(directory, scene.Slug + ".svg");
        float canvasWidth = width + 64f;
        float canvasHeight = height + 220f;
        XmlWriterSettings settings = new XmlWriterSettings {
            Encoding = new UTF8Encoding(false), Indent = true, NewLineChars = "\n", NewLineHandling = NewLineHandling.Replace
        };
        using (XmlWriter xml = XmlWriter.Create(file, settings)) {
            xml.WriteStartElement("svg", SvgNamespace);
            Attr(xml, "width", canvasWidth);
            Attr(xml, "height", canvasHeight);
            xml.WriteAttributeString("viewBox", "0 0 " + F(canvasWidth) + " " + F(canvasHeight));
            xml.WriteAttributeString("role", "img");
            xml.WriteAttributeString("aria-labelledby", "title description");
            xml.WriteStartElement("title");
            xml.WriteAttributeString("id", "title");
            xml.WriteString(scene.Title + " / actual production View");
            xml.WriteEndElement();
            xml.WriteStartElement("desc");
            xml.WriteAttributeString("id", "description");
            xml.WriteString(Disclosure + " Geometry and colors come from " + scene.Source + ", LayoutEngine, WidgetPainter and ThemePalettes. " +
                "Virtual rows are built by their production factory and laid out with LayoutEngine. " +
                "SVG uses estimated text metrics and text roles from the real widget tree. Native window chrome and scrollbars are outside this preview.");
            xml.WriteEndElement();
            xml.WriteStartElement("metadata");
            xml.WriteString("Source: " + scene.Source + "; MaximumUI.LayoutEngine; MaximumUI.WidgetPainter; " +
                "palette=" + scene.PaletteName + "; nodes=" + nodes.Count + "; commands=" + drawing.Commands.Count +
                "; viewport=" + F(width) + "x" + F(height) + "; virtual rows built=" + preview.VirtualRowsBuilt + "; " + scene.Detail);
            xml.WriteEndElement();
            Rect(xml, new MuiRect(0f, 0f, canvasWidth, canvasHeight), palette.TitleBg, false, 0f);
            Text(xml, 32f, 33f, "MAXIMUM UI   /   DESIGN REVIEW", 11f, palette.TextMuted, "start", null);
            Text(xml, 32f, 65f, scene.Title, 25f, palette.Text, "start", null);
            Text(xml, 32f, 88f, scene.PaletteName + "  /  ACTUAL PRODUCTION VIEW  /  MANAGED DRAWLIST", 11f, palette.TextMuted, "start", null);
            xml.WriteStartElement("g");
            xml.WriteAttributeString("transform", "translate(32 112)");
            WriteCommands(xml, drawing, nodes, palette);
            xml.WriteEndElement();
            Text(xml, 32f, height + 144f, Disclosure, 12f, palette.Text, "start", null);
            List<string> footer = Wrap("Production widgets, layout, palette and drawing commands. Anonymous example data. SVG typography is approximate; native chrome and scrollbars are omitted.", width, 11f);
            for (int i = 0; i < footer.Count; i++) Text(xml, 32f, height + 166f + i * 16f, footer[i], 11f, palette.TextMuted, "start", null);
            xml.WriteEndElement();
        }

        // Verify the saved artifact, not just the source string.
        XmlDocument saved = new XmlDocument();
        saved.Load(file);
        if (saved.DocumentElement.LocalName != "svg" || !saved.InnerText.Contains(Disclosure))
            throw new InvalidOperationException("The SVG or its validation disclosure is missing.");
        Console.WriteLine(Path.GetFileName(file) + ": " + nodes.Count + " real layout nodes; " + drawing.Commands.Count +
            " real drawing commands; " + preview.VirtualRowsBuilt + " virtual rows; viewport " + F(width) + "x" + F(height));
        return new {
            file = Path.GetFileName(file), source = scene.Source, detail = scene.Detail, palette = scene.PaletteName,
            viewport = new { width, height }, nodes = nodes.Count, commands = drawing.Commands.Count,
            virtualRowsBuilt = preview.VirtualRowsBuilt, virtualItems = preview.VirtualItems, scrollViews = preview.ScrollViews
        };
    }

    private static LayoutMetrics PreviewMetrics(ThemeTokens palette) {
        // Explicit, deterministic managed estimates. MaximumTheme's GUIStyle
        // measurement needs the game; these are not claims about Unity glyphs.
        float size = palette.FontSize;
        return new LayoutMetrics {
            RowHeight = Math.Max(26f, size + 12f), SmallRowHeight = Math.Max(16f, size + 4f),
            TitleHeight = 44f, WindowPaddingLeft = 16f, WindowPaddingTop = 44f,
            WindowPaddingRight = 16f, WindowPaddingBottom = 14f, PanelPadding = 12f,
            DialogPaddingLeft = 20f, DialogPaddingTop = 48f, DialogPaddingRight = 20f, DialogPaddingBottom = 18f,
            TextWidth = size * 0.6f,
            MeasureText = value => TextWidth(value, size),
            MeasureWrappedTextHeight = (value, width) => Math.Max(size + 10f, Wrap(value, width, size).Count * size * 1.4f),
            MeasureLabelWidth = label => TextWidth(label.Text, TextSize(label, size)),
            MeasureLabelHeight = (label, width) => Math.Max(TextSize(label, size) + 10f,
                (label.WordWrap ? Wrap(label.Text, width, TextSize(label, size)).Count : 1) * TextSize(label, size) * 1.4f)
        };
    }

    private static void VerifyScene(PreviewScene scene, List<LayoutNode> nodes) {
        bool overview = false, primary = false, selected = false;
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++) {
            LayoutNode node = nodes[i];
            if (node.Widget.Key is string key) keys.Add(key);
            if (node.Widget is Label label && label.Text == "Overview") overview = true;
            if (node.Widget is Button button && button.Variant == ButtonVariant.Primary) primary = true;
            if (node.Widget is Selectable selectable && selectable.Selected) selected = true;
        }
        if (scene.Gallery && (!overview || !primary || !selected))
            throw new InvalidOperationException("The production Overview lost its page title, primary action, or selected navigation item.");
        foreach (string key in scene.RequiredKeys)
            if (!keys.Contains(key)) throw new InvalidOperationException(scene.Source + " lost required widget " + key);
    }

    private static void WriteCommands(XmlWriter xml, DrawList drawing, List<LayoutNode> nodes, ThemeTokens palette) {
        int clipCount = 0, clipDepth = 0;
        for (int i = 0; i < drawing.Commands.Count; i++) {
            DrawCommand command = drawing.Commands[i];
            if (command == null) throw new InvalidOperationException("A null command cannot be exported.");
            switch (command.Type) {
                case DrawCommandType.PushClip:
                    string clip = "clip-" + clipCount++;
                    xml.WriteStartElement("defs");
                    xml.WriteStartElement("clipPath");
                    xml.WriteAttributeString("id", clip);
                    Rect(xml, command.Rect, new MuiColor(1f, 1f, 1f, 1f), false, 0f);
                    xml.WriteEndElement(); xml.WriteEndElement();
                    xml.WriteStartElement("g");
                    xml.WriteAttributeString("clip-path", "url(#" + clip + ")");
                    clipDepth++;
                    break;
                case DrawCommandType.PopClip:
                    if (clipDepth-- <= 0) throw new InvalidOperationException("Unbalanced DrawList clipping.");
                    xml.WriteEndElement();
                    break;
                case DrawCommandType.FillRect: Rect(xml, command.Rect, command.Color, false, 0f); break;
                case DrawCommandType.StrokeRect: Rect(xml, command.Rect, command.Color, true, command.Thickness); break;
                case DrawCommandType.Line: DrawPath(xml, "M " + Point(command.P1) + " L " + Point(command.P2), command.Color, false, command.Thickness); break;
                case DrawCommandType.Polyline:
                    StringBuilder path = new StringBuilder();
                    for (int j = 0; j < command.Points.Length; j++) path.Append(j == 0 ? "M " : " L ").Append(Point(command.Points[j]));
                    DrawPath(xml, path.ToString(), command.Color, false, command.Thickness);
                    break;
                case DrawCommandType.FillTriangle:
                case DrawCommandType.StrokeTriangle:
                    DrawPath(xml, "M " + Point(command.P1) + " L " + Point(command.P2) + " L " + Point(command.P3) + " Z", command.Color, command.Type == DrawCommandType.FillTriangle, command.Thickness);
                    break;
                case DrawCommandType.FillCircle:
                case DrawCommandType.StrokeCircle:
                    xml.WriteStartElement("circle");
                    Attr(xml, "cx", command.P1.X); Attr(xml, "cy", command.P1.Y); Attr(xml, "r", command.Radius);
                    Paint(xml, command.Color, command.Type == DrawCommandType.StrokeCircle, command.Thickness);
                    xml.WriteEndElement();
                    break;
                case DrawCommandType.Bezier:
                    DrawPath(xml, "M " + Point(command.P1) + " C " + Point(command.P2) + " " + Point(command.P3) + " " + Point(command.P4), command.Color, false, command.Thickness);
                    break;
                case DrawCommandType.Text: DrawText(xml, command, nodes, palette.FontSize); break;
                case DrawCommandType.Image:
                    throw new NotSupportedException("Native texture export is deliberately unsupported; no image placeholder is fabricated.");
                default: throw new NotSupportedException("Draw command " + command.Type + " needs an explicit SVG backend.");
            }
        }
        if (clipDepth != 0) throw new InvalidOperationException("Unbalanced DrawList clipping.");
    }

    private static void DrawText(XmlWriter xml, DrawCommand command, List<LayoutNode> nodes, float bodySize) {
        LayoutNode node = FindTextOwner(command.Rect, nodes);
        Label label = node == null ? null : node.Widget as Label;
        float size = TextSize(label, bodySize);
        bool centered = node != null && (node.Widget is Button || node.Widget is StatusBadge ||
            (node.Widget.GetType().IsGenericType && node.Widget.GetType().GetGenericTypeDefinition() == typeof(SegmentedControl<>)));
        bool wrapped = label != null && label.WordWrap || node != null && node.Widget is HelpBox;
        List<string> lines = wrapped ? Wrap(command.Text, command.Rect.Width, size) : new List<string> { command.Text ?? string.Empty };
        float leading = size * 1.4f;
        float y = command.Rect.Y + (command.Rect.Height - leading * lines.Count) * 0.5f + size;
        float x = centered ? command.Rect.X + command.Rect.Width * 0.5f : command.Rect.X;
        // DrawList carries text boxes, not font/ascent/alignment. This backend
        // reads the actual widget role instead of inventing a second UI tree.
        for (int i = 0; i < lines.Count; i++)
            Text(xml, x, y + i * leading, lines[i], size, command.Color, centered ? "middle" : "start", node == null ? null : node.Path);
    }

    private static LayoutNode FindTextOwner(MuiRect text, List<LayoutNode> nodes) {
        LayoutNode best = null;
        float area = float.PositiveInfinity;
        for (int i = 0; i < nodes.Count; i++) {
            LayoutNode node = nodes[i];
            MuiRect rect = node.Rect;
            if (rect.X > text.X + 0.1f || rect.Y > text.Y + 0.1f || rect.Right < text.Right - 0.1f || rect.Bottom < text.Bottom - 0.1f) continue;
            if (rect.Width * rect.Height >= area) continue;
            area = rect.Width * rect.Height;
            best = node;
        }
        return best;
    }

    private static float TextSize(Label label, float bodySize) {
        return label != null && label.Role == LabelRole.Heading ? bodySize + 8f : bodySize;
    }

    private static List<string> Wrap(string value, float width, float size) {
        List<string> lines = new List<string>();
        string[] paragraphs = (value ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        foreach (string paragraph in paragraphs) {
            string line = string.Empty;
            foreach (char character in paragraph) {
                if (line.Length > 0 && TextWidth(line + character, size) > width) {
                    int space = line.LastIndexOf(' ');
                    if (space > 0 && character < 0x2e80) {
                        lines.Add(line.Substring(0, space));
                        line = line.Substring(space + 1);
                    } else { lines.Add(line); line = string.Empty; }
                }
                if (line.Length > 0 || character != ' ') line += character;
            }
            lines.Add(line);
        }
        return lines;
    }

    private static float TextWidth(string value, float size) {
        float width = 0f;
        foreach (char character in value ?? string.Empty)
            width += size * (character >= 0x2e80 ? 1f : 0.6f);
        return width;
    }

    private static void Rect(XmlWriter xml, MuiRect rect, MuiColor color, bool stroke, float thickness) {
        xml.WriteStartElement("rect");
        float inset = stroke ? Math.Max(0f, thickness) * 0.5f : 0f;
        Attr(xml, "x", rect.X + inset); Attr(xml, "y", rect.Y + inset);
        Attr(xml, "width", Math.Max(0f, rect.Width - inset * 2f)); Attr(xml, "height", Math.Max(0f, rect.Height - inset * 2f));
        Paint(xml, color, stroke, thickness);
        xml.WriteEndElement();
    }

    private static void DrawPath(XmlWriter xml, string path, MuiColor color, bool fill, float thickness) {
        xml.WriteStartElement("path"); xml.WriteAttributeString("d", path);
        Paint(xml, color, !fill, thickness);
        xml.WriteEndElement();
    }

    private static void Paint(XmlWriter xml, MuiColor color, bool stroke, float thickness) {
        if (stroke) { xml.WriteAttributeString("fill", "none"); Attr(xml, "stroke-width", thickness); }
        xml.WriteAttributeString(stroke ? "stroke" : "fill", Color(color));
        if (color.A < 1f) Attr(xml, stroke ? "stroke-opacity" : "fill-opacity", color.A);
    }

    private static void Text(XmlWriter xml, float x, float y, string text, float size, MuiColor color, string anchor, string path) {
        xml.WriteStartElement("text");
        Attr(xml, "x", x); Attr(xml, "y", y); Attr(xml, "font-size", size);
        xml.WriteAttributeString("font-family", "'Segoe UI', 'Noto Sans CJK SC', 'Microsoft YaHei', 'DejaVu Sans', sans-serif");
        xml.WriteAttributeString("text-anchor", anchor);
        xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        if (path != null) xml.WriteAttributeString("data-widget-path", path);
        Paint(xml, color, false, 0f);
        xml.WriteString(text ?? string.Empty);
        xml.WriteEndElement();
    }

    private static string Color(MuiColor color) {
        return "#" + Byte(color.R).ToString("x2") + Byte(color.G).ToString("x2") + Byte(color.B).ToString("x2");
    }
    private static int Byte(float value) { return (int)Math.Round(Math.Max(0f, Math.Min(1f, value)) * 255f); }
    private static string Point(MuiVec2 point) { return F(point.X) + " " + F(point.Y); }
    private static string F(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
    private static void Attr(XmlWriter xml, string name, float value) { xml.WriteAttributeString(name, F(value)); }
}
