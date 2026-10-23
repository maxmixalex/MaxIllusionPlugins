using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Xml;
using MaximumUI;

// A small SVG backend for the real Gallery's managed DrawList. This program
// never constructs a MaximumTheme, executes callbacks, or initializes Unity.
internal static class Program {
    private const float Width = 940f;
    private const float Height = 840f;
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
            Export(gallery, ThemePalettes.MetroDark(), "Metro Dark", "dark", args[1]);
            Export(gallery, ThemePalettes.MetroLight(), "Metro Light", "light", args[1]);
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error is TargetInvocationException && error.InnerException != null
                ? error.InnerException : error);
            return 1;
        }
    }

    private static void Export(Assembly gallery, ThemeTokens palette, string name, string suffix, string directory) {
        Type viewType = gallery.GetType("MaximumUIGallery.GalleryView", true);
        View view = (View)Activator.CreateInstance(viewType, true);
        FieldInfo stateField = viewType.BaseType.GetField("State", BindingFlags.Instance | BindingFlags.NonPublic);
        object state = stateField.GetValue(view);
        state.GetType().GetField("ThemeName").SetValue(state, name);
        state.GetType().GetField("EditableTheme").SetValue(state, palette.Clone());

        // This is the production default Overview, not a second copy of its UI.
        // Services remain null: Build registers no shortcuts and no callback runs.
        Widget widget = view.Build(new BuildContext());
        if (!(widget is Window)) throw new InvalidOperationException("The Gallery must build a Window.");
        LayoutNode layout = new LayoutEngine(PreviewMetrics(palette)).Layout(widget, new MuiRect(0f, 0f, Width, Height));
        List<LayoutNode> nodes = new List<LayoutNode>();
        Collect(layout, nodes);
        VerifyLayout(nodes);
        DrawList drawing = new WidgetPainter(palette).Paint(layout, new WidgetPaintState());
        if (drawing.Commands.Count == 0) throw new InvalidOperationException("The production painter returned no commands.");

        string file = Path.Combine(directory, "maximumui-metro-" + suffix + "-preview.svg");
        XmlWriterSettings settings = new XmlWriterSettings {
            Encoding = new UTF8Encoding(false), Indent = true, NewLineChars = "\n", NewLineHandling = NewLineHandling.Replace
        };
        using (XmlWriter xml = XmlWriter.Create(file, settings)) {
            xml.WriteStartElement("svg", SvgNamespace);
            Attr(xml, "width", Width + 64f);
            Attr(xml, "height", Height + 168f);
            xml.WriteAttributeString("viewBox", "0 0 " + F(Width + 64f) + " " + F(Height + 168f));
            xml.WriteAttributeString("role", "img");
            xml.WriteAttributeString("aria-labelledby", "title description");
            xml.WriteStartElement("title");
            xml.WriteAttributeString("id", "title");
            xml.WriteString("MaximumUI / " + name + " / actual Gallery Overview");
            xml.WriteEndElement();
            xml.WriteStartElement("desc");
            xml.WriteAttributeString("id", "description");
            xml.WriteString(Disclosure + " Geometry and colors come from GalleryView.Build, LayoutEngine, WidgetPainter and ThemePalettes. " +
                "SVG uses estimated text metrics and text roles from the real widget tree. Native window chrome and scrollbars are outside this preview.");
            xml.WriteEndElement();
            xml.WriteStartElement("metadata");
            xml.WriteString("Source: MaximumUIGallery.GalleryView.Build; MaximumUI.LayoutEngine; MaximumUI.WidgetPainter; " +
                "palette=" + name + "; nodes=" + nodes.Count + "; commands=" + drawing.Commands.Count + "; viewport=940x840; default page=Overview");
            xml.WriteEndElement();
            Rect(xml, new MuiRect(0f, 0f, Width + 64f, Height + 168f), palette.TitleBg, false, 0f);
            Text(xml, 32f, 33f, "MAXIMUM UI   /   DESIGN REVIEW", 11f, palette.TextMuted, "start", null);
            Text(xml, 32f, 65f, name, 25f, palette.Text, "start", null);
            Text(xml, Width + 32f, 63f, "ACTUAL GALLERY  /  MANAGED DRAWLIST", 11f, palette.TextMuted, "end", null);
            xml.WriteStartElement("g");
            xml.WriteAttributeString("transform", "translate(32 96)");
            WriteCommands(xml, drawing, nodes, palette);
            xml.WriteEndElement();
            Text(xml, 32f, Height + 125f, Disclosure, 12f, palette.Text, "start", null);
            Text(xml, 32f, Height + 147f, "Production widget tree + layout + palette + drawing commands. SVG typography is approximate; native chrome is omitted.", 11f, palette.TextMuted, "start", null);
            xml.WriteEndElement();
        }

        // Verify the saved artifact, not just the source string.
        XmlDocument saved = new XmlDocument();
        saved.Load(file);
        if (saved.DocumentElement.LocalName != "svg" || !saved.InnerText.Contains(Disclosure))
            throw new InvalidOperationException("The SVG or its validation disclosure is missing.");
        Console.WriteLine(Path.GetFileName(file) + ": " + nodes.Count + " real layout nodes; " + drawing.Commands.Count + " real drawing commands; viewport " + F(Width) + "x" + F(Height));
    }

    private static LayoutMetrics PreviewMetrics(ThemeTokens palette) {
        // Explicit, deterministic managed estimates. MaximumTheme's GUIStyle
        // measurement needs the game; these are not claims about Unity glyphs.
        float size = palette.FontSize;
        return new LayoutMetrics {
            RowHeight = Math.Max(26f, size + 12f), SmallRowHeight = Math.Max(16f, size + 4f),
            TitleHeight = 44f, WindowPaddingLeft = 16f, WindowPaddingTop = 44f,
            WindowPaddingRight = 16f, WindowPaddingBottom = 14f, PanelPadding = 12f,
            TextWidth = size * 0.6f,
            MeasureText = value => (value ?? string.Empty).Length * size * 0.6f,
            MeasureLabelWidth = label => (label.Text ?? string.Empty).Length * TextSize(label, size) * 0.6f,
            MeasureLabelHeight = (label, width) => Math.Max(TextSize(label, size) + 10f,
                (label.WordWrap ? Wrap(label.Text, width, TextSize(label, size)).Count : 1) * TextSize(label, size) * 1.4f)
        };
    }

    private static void Collect(LayoutNode node, List<LayoutNode> result) {
        result.Add(node);
        for (int i = 0; i < node.Children.Count; i++) Collect(node.Children[i], result);
    }

    private static void VerifyLayout(List<LayoutNode> nodes) {
        bool overview = false, primary = false, selected = false;
        for (int i = 0; i < nodes.Count; i++) {
            LayoutNode node = nodes[i];
            MuiRect rect = node.Rect;
            if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height) || rect.Width < 0f || rect.Height < 0f)
                throw new InvalidOperationException("Invalid production layout geometry at " + node.Path);
            // Overview is shown in a taller window so this exporter does not fake
            // scrolling/clipping that belongs to the native IMGUI renderer.
            if (rect.Width > 0f && rect.Height > 0f && (rect.X < -0.1f || rect.Y < -0.1f || rect.Right > Width + 0.1f || rect.Bottom > Height + 0.1f))
                throw new InvalidOperationException("The Overview no longer fits the preview viewport: " + node.Path + " " + node.Widget.GetType().Name);
            if (node.Widget is Label label && label.Text == "Overview") overview = true;
            if (node.Widget is Button button && button.Variant == ButtonVariant.Primary) primary = true;
            if (node.Widget is Selectable selectable && selectable.Selected) selected = true;
        }
        if (!overview || !primary || !selected) throw new InvalidOperationException("The production Overview lost its page title, primary action, or selected navigation item.");
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
        List<string> lines = label != null && label.WordWrap ? Wrap(command.Text, command.Rect.Width, size) : new List<string> { command.Text ?? string.Empty };
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
        int columns = !float.IsFinite(width) ? int.MaxValue : Math.Max(1, (int)(width / (size * 0.6f)));
        string[] paragraphs = (value ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        foreach (string paragraph in paragraphs) {
            string line = string.Empty;
            foreach (string word in paragraph.Split(' ')) {
                if (line.Length > 0 && line.Length + 1 + word.Length > columns) { lines.Add(line); line = string.Empty; }
                line = line.Length == 0 ? word : line + " " + word;
            }
            lines.Add(line);
        }
        return lines;
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
        xml.WriteAttributeString("font-family", "'Segoe UI', 'DejaVu Sans', sans-serif");
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
