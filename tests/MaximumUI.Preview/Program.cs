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
    private static readonly PreviewTypography Typography = new PreviewTypography();
    private static readonly List<PreviewIssue> Issues = new List<PreviewIssue>();

    private static int Main(string[] args) {
        if (args.Length < 2 || args.Length > 3) {
            Console.Error.WriteLine("Usage: MaximumUI.Preview <KK_MaximumUIGallery.dll> <output-directory> [--require-shaped]");
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
            if (args.Length == 3 && (args[2] != "--require-shaped" || !Typography.IsShaped))
                throw new InvalidOperationException("Shaped typography requires Linux Pango (install libpango-1.0-0 and libpangocairo-1.0-0).");
            List<PreviewScene> scenes = new List<PreviewScene>(GalleryPreviewScenes.Create(gallery));
            scenes.AddRange(PluginPreviewScenes.Create());
            AddHostScenes(scenes, directory);
            List<object> manifest = new List<object>();
            List<string> errors = new List<string>();
            foreach (PreviewScene scene in scenes) {
                try {
                    ManagedPreviewLayout preview = Prepare(scene);
                    manifest.Add(Export(scene, args[1], preview));
                    if (scene.Gallery) {
                        foreach (PreviewScene sample in ScrollSamples(scene, preview)) manifest.Add(Export(sample, args[1], Prepare(sample)));
                        if (scene.StateId == "default") {
                            foreach (PreviewScene sample in PopupSamples(scene, preview)) manifest.Add(Export(sample, args[1], Prepare(sample)));
                        }
                        if (scene.StateId == "default" && scene.SizeName == "standard" && scene.PaletteName == "Metro Light")
                            foreach (PreviewScene sample in InteractionSamples(scene, preview)) manifest.Add(Export(sample, args[1], Prepare(sample)));
                    }
                } catch (Exception error) {
                    Exception cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                    errors.Add(scene.Slug + ": " + cause.Message);
                    Console.Error.WriteLine(scene.Slug + ": " + cause);
                }
            }
            File.WriteAllText(Path.Combine(args[1], "preview-manifest.json"),
                JsonSerializer.Serialize(new { disclosure = Disclosure, typography = Typography.Backend,
                    fontFamily = PreviewTypography.Family, primaryFont = Typography.PrimaryFont, fontNotes = Typography.FontNotes,
                    missingGlyphs = Typography.UnknownGlyphs,
                    sourceCommit = Environment.GetEnvironmentVariable("GITHUB_SHA"),
                    galleryAssembly = gallery.GetName().Name, galleryVersion = gallery.GetName().Version.ToString(),
                    uiVersion = typeof(WidgetPainter).Assembly.GetName().Version.ToString(),
                    galleryPages = GalleryPages(scenes), galleryCases = GalleryCases(scenes),
                    galleryStates = GalleryStates(scenes), previews = manifest, errors, issues = Issues },
                    new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            Console.WriteLine("Gallery export: " + manifest.Count + " snapshots; " + errors.Count + " geometry errors; " + Issues.Count + " text review items; typography: " + Typography.Backend);
            return errors.Count == 0 ? 0 : 1;
        } catch (Exception error) {
            Console.Error.WriteLine(error is TargetInvocationException && error.InnerException != null
                ? error.InnerException : error);
            return 1;
        } finally {
            Typography.Dispose();
        }
    }

    private static string[] GalleryPages(List<PreviewScene> scenes) {
        HashSet<string> pages = new HashSet<string>(StringComparer.Ordinal);
        foreach (PreviewScene scene in scenes) if (scene.Gallery) pages.Add(scene.PageId);
        string[] result = new string[pages.Count]; pages.CopyTo(result); return result;
    }

    private static List<object> GalleryCases(List<PreviewScene> scenes) {
        List<object> cases = new List<object>();
        foreach (PreviewScene scene in scenes) if (scene.Gallery) cases.Add(new {
            file = scene.Slug + ".svg", page = scene.PageId, state = scene.StateId, size = scene.SizeName, palette = scene.PaletteName
        });
        return cases;
    }

    private static List<object> GalleryStates(List<PreviewScene> scenes) {
        HashSet<string> known = new HashSet<string>(StringComparer.Ordinal);
        List<object> states = new List<object>();
        foreach (PreviewScene scene in scenes) if (scene.Gallery && !scene.StateId.StartsWith("service-", StringComparison.Ordinal) && known.Add(scene.PageId + ":" + scene.StateId))
            states.Add(new { page = scene.PageId, state = scene.StateId });
        return states;
    }

    private static string[] WidgetTypes(ManagedPreviewLayout preview, bool visibleOnly) {
        HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
        foreach (LayoutNode node in preview.Nodes) {
            if (visibleOnly && !preview.VisiblePaths.Contains(node.Path)) continue;
            string name = node.Widget.GetType().Name;
            int generic = name.IndexOf('`');
            types.Add(generic < 0 ? name : name.Substring(0, generic));
        }
        string[] result = new string[types.Count]; types.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result;
    }

    private static string[] DeclaredWidgetTypes(Widget root) {
        HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
        CollectDeclaredTypes(root, types);
        string[] result = new string[types.Count]; types.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result;
    }

    private static void CollectDeclaredTypes(Widget widget, HashSet<string> types) {
        if (widget == null) return;
        string name = widget.GetType().Name;
        int generic = name.IndexOf('`');
        types.Add(generic < 0 ? name : name.Substring(0, generic));
        foreach (Widget child in widget.Children) CollectDeclaredTypes(child, types);
        if (widget.ContextMenuContent != null) CollectDeclaredTypes(widget.ContextMenuContent, types);
    }

    private static ManagedPreviewLayout Prepare(PreviewScene scene) {
        if (scene.Build != null) scene.Widget = scene.Build();
        Window window = scene.Widget as Window;
        if (window == null) throw new InvalidOperationException(scene.Source + " must build a Window.");
        return ManagedPreviewLayout.Create(scene.Widget, new MuiRect(0f, 0f,
            scene.Width > 0f ? scene.Width : window.Width, scene.Height > 0f ? scene.Height : window.Height),
            PreviewMetrics(scene.Palette), scene.Palette, scene);
    }

    private static IEnumerable<PreviewScene> ScrollSamples(PreviewScene scene, ManagedPreviewLayout preview) {
        int scrollIndex = 0;
        foreach (PreviewScrollArea area in preview.ScrollAreas) {
            if (area.Maximum <= 1f || area.ViewHeight < 24f) continue;
            scrollIndex++;
            PreviewScene visibleScene = scene.Copy(scene.Slug);
            ManagedPreviewLayout revealed = Reveal(visibleScene, area.Path, preview);
            PreviewScrollArea visible = revealed.ScrollAreas.Find(item => item.Path == area.Path);
            List<float> positions = new List<float>();
            if (area.VisibleHeight < area.ViewHeight - 1f) positions.Add(0f);
            if (area.Virtual) { positions.Add(area.Maximum * 0.5f); positions.Add(area.Maximum); }
            else {
                float step = Math.Max(24f, visible.VisibleHeight * 0.85f);
                for (float y = step; y < area.Maximum; y += step) positions.Add(y);
                positions.Add(area.Maximum);
            }
            for (int i = 0; i < positions.Count; i++) {
                PreviewScene copy = visibleScene.Copy(scene.Slug + "-scroll" + scrollIndex + "-" + (i + 1));
                copy.ScrollOf = scene.Slug + ".svg";
                copy.Detail += "; " + area.Key + " offset " + F(positions[i]) + "/" + F(area.Maximum);
                copy.ScrollOffsets[area.Path] = positions[i];
                yield return copy;
            }
        }
    }

    // Reveal a nested scroll viewport or popup anchor by moving its real scroll
    // ancestors. Logical offsets in an entirely hidden list are not coverage.
    private static ManagedPreviewLayout Reveal(PreviewScene scene, string path, ManagedPreviewLayout preview) {
        List<string> ancestors = new List<string>();
        foreach (PreviewScrollArea area in preview.ScrollAreas)
            if (path.StartsWith(area.Path + ".", StringComparison.Ordinal)) ancestors.Add(area.Path);
        for (int i = ancestors.Count - 1; i >= 0; i--) {
            PreviewScrollArea area = preview.ScrollAreas.Find(item => item.Path == ancestors[i]);
            LayoutNode target = preview.Nodes.Find(node => node.Path == path);
            if (target == null) throw new InvalidOperationException("Missing scroll target " + path);
            float delta = target.Rect.Y < area.Viewport.Y ? target.Rect.Y - area.Viewport.Y :
                target.Rect.Bottom > area.Viewport.Bottom ? target.Rect.Bottom - area.Viewport.Bottom : 0f;
            float next = Math.Max(0f, Math.Min(area.Maximum, area.Offset + delta));
            if (Math.Abs(next - area.Offset) < 0.1f) continue;
            scene.ScrollOffsets[area.Path] = next;
            preview = Prepare(scene);
        }
        return preview;
    }

    private static IEnumerable<PreviewScene> PopupSamples(PreviewScene scene, ManagedPreviewLayout preview) {
        List<PreviewScene> samples = new List<PreviewScene>();
        if (scene.PageId == "selection") {
            samples.Add(PopupSample(scene, "combo-open", "combo", "mode"));
            samples.Add(PopupSample(scene, "list-open", "combo", "samples"));
            PreviewScene bottom = PopupSample(scene, "list-bottom", "combo", "samples"); bottom.PopupBottom = true; samples.Add(bottom);
            samples.Add(PopupSample(scene, "search-open", "combo", "searchable-samples"));
            PreviewScene filtered = PopupSample(scene, "search-filtered", "combo", "searchable-samples"); filtered.PopupFilter = "Se"; samples.Add(filtered);
            PreviewScene empty = PopupSample(scene, "search-empty", "combo", "searchable-samples"); empty.PopupFilter = "no matches"; samples.Add(empty);
        } else if (scene.PageId == "layout") {
            foreach (string menu in new[] { "file", "view", "edit" }) {
                PreviewScene item = PopupSample(scene, "menu-" + menu, "menu", "gallery-menu");
                item.PopupMenuIndex = menu == "file" ? 0 : menu == "view" ? 1 : 2;
                samples.Add(item);
            }
            LayoutNode bar = preview.Nodes.Find(node => Equals(node.Widget.Key, "gallery-menu"));
            if (bar != null && bar.Widget.Children.Count > 1 && bar.Widget.Children[1] is Menu view) {
                for (int i = 0; i < view.Children.Count; i++) if (view.Children[i] is Menu) {
                    PreviewScene item = PopupSample(scene, "menu-submenu", "menu", "gallery-menu");
                    item.PopupMenuIndex = 1; item.PopupSubmenuIndex = i; samples.Add(item); break;
                }
            }
        } else if (scene.PageId == "buttons") {
            samples.Add(PopupSample(scene, "context-menu", "context", "context-actions"));
            samples.Add(PopupSample(scene, "tooltip", "tooltip", "context-actions"));
        }
        foreach (PreviewScene sample in samples) {
            LayoutNode target = preview.Nodes.Find(node => Equals(node.Widget.Key, sample.PopupKey));
            if (target == null) throw new InvalidOperationException("Missing production popup target " + sample.PopupKey);
            // Reveal without opening until all ancestor positions are settled.
            string kind = sample.PopupKind; sample.PopupKind = null;
            Reveal(sample, target.Path, preview);
            sample.PopupKind = kind;
            yield return sample;
        }
    }

    private static PreviewScene PopupSample(PreviewScene scene, string state, string kind, string key) {
        PreviewScene copy = scene.Copy(scene.Slug + "-" + state);
        copy.StateId = state; copy.PopupKind = kind; copy.PopupKey = key;
        copy.Detail += "; production " + kind + " popup " + key;
        return copy;
    }

    private static IEnumerable<PreviewScene> InteractionSamples(PreviewScene scene, ManagedPreviewLayout preview) {
        if (scene.PageId == "buttons") {
            LayoutNode target = preview.Nodes.Find(node => node.Widget is Button button && button.Variant == ButtonVariant.Primary);
            if (target != null) {
                foreach (string state in new[] { "hovered", "pressed", "focused" }) {
                    PreviewScene copy = scene.Copy(scene.Slug + "-" + state);
                    copy.PaintState = state; copy.PaintKey = target.Widget.Key; copy.PaintPath = target.Path; copy.StateId = state;
                    yield return copy;
                }
            }
        }
        if (scene.PageId == "text-input") {
            LayoutNode target = preview.Nodes.Find(node => node.Widget is TextField && node.Widget.Enabled);
            if (target != null) {
                PreviewScene copy = scene.Copy(scene.Slug + "-focused");
                copy.PaintState = "focused"; copy.PaintKey = target.Widget.Key; copy.PaintPath = target.Path; copy.StateId = "focused";
                yield return copy;
            }
        }
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

    private static object Export(PreviewScene scene, string directory, ManagedPreviewLayout preview) {
        Window window = scene.Widget as Window;
        if (window == null) throw new InvalidOperationException(scene.Source + " must build a Window.");
        float width = scene.Width > 0f ? scene.Width : window.Width;
        float height = scene.Height > 0f ? scene.Height : window.Height;
        ThemeTokens palette = scene.Palette;
        List<LayoutNode> nodes = preview.Nodes;
        VerifyScene(scene, nodes);
        DrawList drawing = preview.Drawing;
        if (drawing.Commands.Count == 0) throw new InvalidOperationException("The production painter returned no commands.");

        string file = Path.Combine(directory, scene.Slug + ".svg");
        float canvasWidth = width;
        float canvasHeight = height;
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
                "SVG text uses " + Typography.Backend + ". Native Unity hosting and input are outside this preview.");
            xml.WriteEndElement();
            xml.WriteStartElement("metadata");
            xml.WriteString("Source: " + scene.Source + "; MaximumUI.LayoutEngine; MaximumUI.WidgetPainter; " +
                "palette=" + scene.PaletteName + "; nodes=" + nodes.Count + "; commands=" + drawing.Commands.Count +
                "; viewport=" + F(width) + "x" + F(height) + "; virtual rows built=" + preview.VirtualRowsBuilt + "; " + scene.Detail);
            xml.WriteEndElement();
            WriteCommands(xml, drawing, preview, palette, scene);
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
            file = Path.GetFileName(file), title = scene.Title, source = scene.Source, detail = scene.Detail, palette = scene.PaletteName,
            page = scene.PageId, state = scene.StateId, size = scene.SizeName, paintState = scene.PaintState, paintPath = scene.PaintPath,
            scrollOf = scene.ScrollOf,
            viewport = new { width, height }, nodes = nodes.Count, commands = drawing.Commands.Count,
            virtualRowsBuilt = preview.VirtualRowsBuilt, virtualItems = preview.VirtualItems, scrollViews = preview.ScrollViews,
            dialogs = preview.DialogCount, popups = preview.Popups,
            scrollAreas = preview.ScrollAreas, visibleWidgetPaths = preview.VisiblePaths,
            declaredWidgetTypes = DeclaredWidgetTypes(scene.Widget),
            widgetTypes = WidgetTypes(preview, false), visibleWidgetTypes = WidgetTypes(preview, true)
        };
    }

    internal static LayoutMetrics PreviewMetrics(ThemeTokens palette) {
        LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(palette);
        metrics.MeasureText = value => Typography.Measure(value, palette.FontSize).Width;
        metrics.MeasureWrappedTextHeight = (value, width) =>
            Typography.Wrap(value, width, palette.FontSize).Count * ThemeTypography.LineHeight(palette, LabelRole.Body);
        metrics.MeasureLabelWidth = label => Typography.Measure(label.Text,
            ThemeTypography.FontSize(palette, label.Role), Weight(ThemeTypography.Format(palette, label.Role).Weight)).Width;
        metrics.MeasureLabelHeight = (label, width) =>
            (label.WordWrap ? Typography.Wrap(label.Text, width, ThemeTypography.FontSize(palette, label.Role),
                Weight(ThemeTypography.Format(palette, label.Role).Weight)).Count : 1) * ThemeTypography.LineHeight(palette, label.Role);
        return metrics;
    }

    private static void VerifyScene(PreviewScene scene, List<LayoutNode> nodes) {
        bool selected = false;
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++) {
            LayoutNode node = nodes[i];
            if (node.Widget.Key is string key) keys.Add(key);
            if (node.Widget is Selectable selectable && selectable.Selected) selected = true;
        }
        if (scene.Gallery && !selected)
            throw new InvalidOperationException("The production Gallery lost its selected navigation item.");
        foreach (string key in scene.RequiredKeys)
            if (!keys.Contains(key)) throw new InvalidOperationException(scene.Source + " lost required widget " + key);
    }

    private static void WriteCommands(XmlWriter xml, DrawList drawing, ManagedPreviewLayout preview, ThemeTokens palette, PreviewScene scene) {
        int clipCount = 0, clipDepth = 0;
        Stack<MuiRect> clips = new Stack<MuiRect>();
        clips.Push(preview.Root.Rect);
        List<PreviewTextBox> textBoxes = new List<PreviewTextBox>();
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
                    clips.Push(ManagedPreviewLayout.Intersect(clips.Peek(), command.Rect));
                    break;
                case DrawCommandType.PopClip:
                    if (clipDepth-- <= 0) throw new InvalidOperationException("Unbalanced DrawList clipping.");
                    xml.WriteEndElement();
                    clips.Pop();
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
                case DrawCommandType.Text:
                    preview.CommandOwners.TryGetValue(command, out LayoutNode owner);
                    preview.CommandLayers.TryGetValue(command, out int layer);
                    DrawText(xml, command, owner, palette, scene, clips.Peek(), i, layer, textBoxes);
                    break;
                case DrawCommandType.Image:
                    if (command.Resource == null) break; // Unity also ignores a missing texture.
                    throw new NotSupportedException("Native texture export is deliberately unsupported; no image placeholder is fabricated.");
                default: throw new NotSupportedException("Draw command " + command.Type + " needs an explicit SVG backend.");
            }
        }
        if (clipDepth != 0) throw new InvalidOperationException("Unbalanced DrawList clipping.");
        VerifyTextOverlaps(scene, textBoxes);
    }

    private static void DrawText(XmlWriter xml, DrawCommand command, LayoutNode owner, ThemeTokens palette,
        PreviewScene scene, MuiRect viewport, int index, int layer, List<PreviewTextBox> textBoxes) {
        TextFormat format = command.TextFormat;
        if (format.FontSize <= 0f) format = ThemeTypography.Format(palette,
            owner != null && owner.Widget is Label label ? label.Role : LabelRole.Body);
        float size = format.FontSize;
        int weight = Weight(format.Weight);
        LabelRole role = owner != null && owner.Widget is Label ownerLabel ? ownerLabel.Role : LabelRole.Body;
        float leading = ThemeTypography.LineHeight(palette, role);
        if (Math.Abs(size - ThemeTypography.FontSize(palette, role)) > 0.1f) leading = (float)Math.Ceiling(size * 1.4f);
        List<string> lines = format.WordWrap ? Typography.Wrap(command.Text, command.Rect.Width, size, weight)
            : new List<string>((command.Text ?? string.Empty).Replace("\r", string.Empty).Split('\n'));
        float textHeight = leading * lines.Count;
        float top = format.VerticalAlignment == TextVerticalAlignment.Top ? command.Rect.Y :
            format.VerticalAlignment == TextVerticalAlignment.Bottom ? command.Rect.Bottom - textHeight :
            command.Rect.Y + (command.Rect.Height - textHeight) * 0.5f;
        float x = format.Alignment == TextAlignment.Center ? command.Rect.X + command.Rect.Width * 0.5f :
            format.Alignment == TextAlignment.End ? command.Rect.Right : command.Rect.X;
        string anchor = format.Alignment == TextAlignment.Center ? "middle" : format.Alignment == TextAlignment.End ? "end" : "start";
        MuiRect visible = ManagedPreviewLayout.Intersect(command.Rect, viewport);
        // Native GUIStyle clips text to its content box. Explicit SVG clips avoid
        // falsely painting long inputs over neighboring controls.
        string clipId = "text-box-" + index;
        xml.WriteStartElement("defs"); xml.WriteStartElement("clipPath"); xml.WriteAttributeString("id", clipId);
        Rect(xml, command.Rect, new MuiColor(1f, 1f, 1f, 1f), false, 0f);
        xml.WriteEndElement(); xml.WriteEndElement();
        xml.WriteStartElement("g"); xml.WriteAttributeString("clip-path", "url(#" + clipId + ")");
        for (int i = 0; i < lines.Count; i++) {
            TextMeasurement measure = Typography.Measure(lines[i], size, weight);
            float y = top + i * leading + (leading - measure.Height) * 0.5f + measure.Baseline;
            Text(xml, x, y, lines[i], size, command.Color, anchor, owner == null ? null : owner.Path, weight,
                "text-" + index + "-" + i, command.Rect, visible, CanClipText(owner), layer);
            float left = format.Alignment == TextAlignment.Center ? x - measure.Width * 0.5f :
                format.Alignment == TextAlignment.End ? x - measure.Width : x;
            MuiRect ink = new MuiRect(left + measure.InkX, top + i * leading + (leading - measure.Height) * 0.5f + measure.InkY,
                measure.InkWidth, measure.InkHeight);
            MuiRect shown = ManagedPreviewLayout.Intersect(ink, visible);
            if (shown.Width > 1f && shown.Height > 1f) textBoxes.Add(new PreviewTextBox(owner, shown, lines[i], layer));
            if (visible.Width > 1f && visible.Height > 1f &&
                (ink.X < command.Rect.X - 1f || ink.Right > command.Rect.Right + 1f) && !CanClipText(owner))
                Issues.Add(new PreviewIssue(scene.Slug, "text-overflow", owner == null ? null : owner.Path,
                    "Text needs " + F(measure.Width) + "px in " + F(command.Rect.Width) + "px: " + lines[i]));
            if (visible.Width > 1f && visible.Height > 1f &&
                (ink.Y < command.Rect.Y - 1f || ink.Bottom > command.Rect.Bottom + 1f) && !CanClipText(owner))
                Issues.Add(new PreviewIssue(scene.Slug, "text-height", owner == null ? null : owner.Path,
                    "Glyph ink exceeds its " + F(command.Rect.Height) + "px text box: " + lines[i]));
            if (visible.Width > 1f && visible.Height > 1f && measure.MissingGlyphs > 0)
                Issues.Add(new PreviewIssue(scene.Slug, "missing-glyph", owner == null ? null : owner.Path, lines[i]));
        }
        xml.WriteEndElement();
    }

    private static bool CanClipText(LayoutNode node) {
        if (node == null) return false;
        Widget widget = node.Widget;
        return widget is TextField || widget is TextArea || widget is PasswordField || widget is SearchBox ||
            widget is MetricsWindow || widget is DebugLogWindow || widget is LayoutDebugger || widget is DrawListDebugger ||
            widget is StateStoreInspector || widget is InputDebugger || widget is IDStackTool || widget is ItemPicker;
    }

    private static int Weight(TextWeight weight) {
        if (weight == TextWeight.Light) return 300;
        if (weight == TextWeight.Semilight) return 350;
        if (weight == TextWeight.Semibold) return 600;
        if (weight == TextWeight.Bold) return 700;
        return 400;
    }

    private static void VerifyTextOverlaps(PreviewScene scene, List<PreviewTextBox> boxes) {
        for (int i = 0; i < boxes.Count; i++) for (int j = i + 1; j < boxes.Count; j++) {
            PreviewTextBox a = boxes[i], b = boxes[j];
            if (a.Layer != b.Layer) continue; // Overlay surfaces intentionally cover lower layers.
            if (a.Owner == null || b.Owner == null) continue;
            MuiRect overlap = ManagedPreviewLayout.Intersect(a.Rect, b.Rect);
            if (overlap.Width > 2f && overlap.Height > 2f)
                Issues.Add(new PreviewIssue(scene.Slug, "text-overlap", a.Owner.Path,
                    a.Text + " overlaps " + b.Owner.Path + " (" + b.Text + ")"));
        }
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

    private static void Text(XmlWriter xml, float x, float y, string text, float size, MuiColor color, string anchor, string path,
        int weight, string id, MuiRect content, MuiRect visible, bool canClip, int layer) {
        xml.WriteStartElement("text");
        xml.WriteAttributeString("id", id);
        xml.WriteAttributeString("data-content-box", RectData(content));
        xml.WriteAttributeString("data-visible-box", RectData(visible));
        xml.WriteAttributeString("data-can-clip", canClip ? "true" : "false");
        xml.WriteAttributeString("data-layer", layer.ToString(CultureInfo.InvariantCulture));
        Attr(xml, "x", x); Attr(xml, "y", y); Attr(xml, "font-size", size);
        xml.WriteAttributeString("font-family", PreviewTypography.SvgFamily);
        xml.WriteAttributeString("data-requested-font-weight", weight.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("font-weight", Typography.SvgWeight(weight).ToString(CultureInfo.InvariantCulture));
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
    private static string RectData(MuiRect rect) { return F(rect.X) + "," + F(rect.Y) + "," + F(rect.Width) + "," + F(rect.Height); }
    private static string F(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
    private static void Attr(XmlWriter xml, string name, float value) { xml.WriteAttributeString(name, F(value)); }
}
