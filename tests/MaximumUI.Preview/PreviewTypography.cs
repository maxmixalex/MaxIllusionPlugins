using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Preview-only native boundary. Pango shapes exactly the font stack used by the
// SVG rasterizer; it does not initialize Unity or become a plugin dependency.
internal sealed class PreviewTypography : IDisposable {
    // Prefer a Latin desktop face before CJK fallback. Putting Noto Sans CJK
    // first also applies its much taller em box to English headings, unlike the
    // native Segoe-first stack. Selawik is Microsoft's open Segoe-compatible face.
    internal const string Family = "Segoe UI, Selawik, Noto Sans, DejaVu Sans, Noto Sans CJK SC, Microsoft YaHei";
    internal const string SvgFamily = "'Segoe UI', Selawik, 'Noto Sans', 'DejaVu Sans', 'Noto Sans CJK SC', 'Microsoft YaHei', sans-serif";
    private const double Scale = 1024d;
    private readonly Dictionary<string, TextMeasurement> cache = new Dictionary<string, TextMeasurement>();
    private IntPtr context;
    private IntPtr layout;
    private IntPtr description;
    internal bool IsShaped { get; private set; }
    internal string Backend { get; private set; }
    internal string PrimaryFont { get; private set; }
    internal string FontNotes { get { return PrimaryFont == "Selawik"
        ? "Pinned Selawik declares Light and Semilight as weight 300. Requested 350 resolves to Light in Pango and is encoded as SVG weight 300 for the same face; native Segoe UI Semilight is unchanged."
        : "Installed font fallback is recorded; native Unity/Windows font matching is not executed."; } }
    internal int UnknownGlyphs { get; private set; }

    internal PreviewTypography() {
        Backend = "Deterministic fallback estimates (Pango unavailable)";
        if (!OperatingSystem.IsLinux()) return;
        try {
            context = pango_font_map_create_context(pango_cairo_font_map_get_default());
            // SVG uses continuous outline metrics. Pango's defaults round each
            // glyph advance to whole pixels, which underestimates some strings
            // by several pixels even when both backends use the same font.
            // Disable both Pango rounding and Cairo hint metrics before creating
            // the layout; the independent Inkscape check still verifies the ink.
            pango_context_set_round_glyph_positions(context, false);
            IntPtr options = cairo_font_options_create();
            try {
                cairo_font_options_set_hint_metrics(options, 1); // CAIRO_HINT_METRICS_OFF
                cairo_font_options_set_hint_style(options, 1); // CAIRO_HINT_STYLE_NONE
                pango_cairo_context_set_font_options(context, options);
            } finally { cairo_font_options_destroy(options); }
            layout = pango_layout_new(context);
            description = pango_font_description_new();
            pango_font_description_set_family(description, Family);
            pango_font_description_set_absolute_size(description, 14f * Scale);
            IsShaped = context != IntPtr.Zero && layout != IntPtr.Zero && description != IntPtr.Zero;
            if (IsShaped) {
                Backend = "Pango shaping with unhinted fractional SVG metrics and installed font fallback; Unity glyph rasterization remains untested";
                IntPtr font = pango_context_load_font(context, description);
                if (font != IntPtr.Zero) {
                    IntPtr resolved = pango_font_describe(font);
                    if (resolved != IntPtr.Zero) {
                        PrimaryFont = Marshal.PtrToStringUTF8(pango_font_description_get_family(resolved));
                        pango_font_description_free(resolved);
                    }
                    g_object_unref(font);
                }
            }
        } catch (DllNotFoundException) { Dispose(); }
          catch (EntryPointNotFoundException) { Dispose(); }
    }

    internal int SvgWeight(int requested) {
        // The pinned Selawik release declares both Light and Semilight as
        // usWeightClass=300. Pango resolves requested 350 to that Light face.
        // SVG 1.1 only accepts multiples of 100: Inkscape treats literal 350 as
        // 400, silently selecting Regular. Use the matching supported weight
        // for this known preview fallback and retain the request in metadata.
        return requested == 350 && PrimaryFont == "Selawik" ? 300 : requested;
    }

    internal TextMeasurement Measure(string text, float size, int weight = 400) {
        text = text ?? string.Empty;
        string key = size.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + weight + ":" + text;
        if (cache.TryGetValue(key, out TextMeasurement found)) return found;
        TextMeasurement result;
        if (IsShaped) {
            pango_font_description_set_absolute_size(description, size * Scale);
            pango_font_description_set_weight(description, weight);
            pango_layout_set_font_description(layout, description);
            pango_layout_set_text(layout, text, -1);
            pango_layout_get_size(layout, out int width, out int height);
            pango_layout_get_extents(layout, out PangoRectangle ink, out PangoRectangle logical);
            int missing = pango_layout_get_unknown_glyphs_count(layout);
            UnknownGlyphs += missing;
            result = new TextMeasurement((float)(width / Scale), (float)(height / Scale),
                (float)(pango_layout_get_baseline(layout) / Scale), missing,
                (float)(ink.X / Scale), (float)(ink.Y / Scale), (float)(ink.Width / Scale), (float)(ink.Height / Scale));
        } else {
            float width = 0f;
            foreach (char character in text) width += size * (character >= 0x2e80 ? 1f : 0.6f);
            result = new TextMeasurement(width, size * 1.4f, size * 1.1f, 0, 0f, size * 0.1f, width, size);
        }
        cache.Add(key, result);
        return result;
    }

    internal List<string> Wrap(string value, float width, float size, int weight = 400) {
        List<string> lines = new List<string>();
        foreach (string paragraph in (value ?? string.Empty).Replace("\r", string.Empty).Split('\n')) {
            string line = string.Empty;
            foreach (char character in paragraph) {
                if (line.Length > 0 && Measure(line + character, size, weight).Width > Math.Max(1f, width)) {
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

    public void Dispose() {
        if (description != IntPtr.Zero) pango_font_description_free(description);
        if (layout != IntPtr.Zero) g_object_unref(layout);
        if (context != IntPtr.Zero) g_object_unref(context);
        description = layout = context = IntPtr.Zero;
        IsShaped = false;
    }

    [DllImport("libpangocairo-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_cairo_font_map_get_default();
    [DllImport("libpangocairo-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_cairo_context_set_font_options(IntPtr context, IntPtr options);
    [DllImport("libcairo.so.2", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr cairo_font_options_create();
    [DllImport("libcairo.so.2", CallingConvention = CallingConvention.Cdecl)] private static extern void cairo_font_options_set_hint_metrics(IntPtr options, int hintMetrics);
    [DllImport("libcairo.so.2", CallingConvention = CallingConvention.Cdecl)] private static extern void cairo_font_options_set_hint_style(IntPtr options, int hintStyle);
    [DllImport("libcairo.so.2", CallingConvention = CallingConvention.Cdecl)] private static extern void cairo_font_options_destroy(IntPtr options);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_font_map_create_context(IntPtr fontmap);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_context_set_round_glyph_positions(IntPtr context, [MarshalAs(UnmanagedType.Bool)] bool roundPositions);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_layout_new(IntPtr context);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_context_load_font(IntPtr context, IntPtr description);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_font_describe(IntPtr font);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_font_description_get_family(IntPtr description);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pango_font_description_new();
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_font_description_set_family(IntPtr description, [MarshalAs(UnmanagedType.LPUTF8Str)] string family);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_font_description_set_absolute_size(IntPtr description, double size);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_font_description_set_weight(IntPtr description, int weight);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_layout_set_font_description(IntPtr layout, IntPtr description);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_layout_set_text(IntPtr layout, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, int length);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_layout_get_size(IntPtr layout, out int width, out int height);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_layout_get_extents(IntPtr layout, out PangoRectangle ink, out PangoRectangle logical);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern int pango_layout_get_baseline(IntPtr layout);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern int pango_layout_get_unknown_glyphs_count(IntPtr layout);
    [DllImport("libpango-1.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void pango_font_description_free(IntPtr description);
    [DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)] private static extern void g_object_unref(IntPtr instance);

    [StructLayout(LayoutKind.Sequential)]
    private struct PangoRectangle { internal int X; internal int Y; internal int Width; internal int Height; }
}

internal readonly struct TextMeasurement {
    internal readonly float Width;
    internal readonly float Height;
    internal readonly float Baseline;
    internal readonly int MissingGlyphs;
    internal readonly float InkX;
    internal readonly float InkY;
    internal readonly float InkWidth;
    internal readonly float InkHeight;
    internal TextMeasurement(float width, float height, float baseline, int missingGlyphs,
        float inkX, float inkY, float inkWidth, float inkHeight) {
        Width = width; Height = height; Baseline = baseline; MissingGlyphs = missingGlyphs;
        InkX = inkX; InkY = inkY; InkWidth = inkWidth; InkHeight = inkHeight;
    }
}
