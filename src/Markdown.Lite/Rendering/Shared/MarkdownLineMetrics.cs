using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

internal static class MarkdownLineMetrics
{
    // A paragraph strut supplies a minimum line box; actual runs retain their own
    // sizes and each physical line expands only for the glyphs on that line.
    internal static double StrutFontSize(Typeface typeface, double fontSize, double minimumLineHeight)
    {
        if (!double.IsFinite(minimumLineHeight) || minimumLineHeight <= 0) return fontSize;
        var metrics = typeface.GlyphTypeface.Metrics;
        var naturalHeight = metrics.LineSpacing * fontSize / metrics.DesignEmHeight;
        return naturalHeight > 0 ? fontSize * Math.Max(1, minimumLineHeight / naturalHeight) : fontSize;
    }
}
