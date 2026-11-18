using System.Globalization;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Styling;

namespace CodeWF.Avalonia.Markdown;

public sealed record MarkdownExportStyle(
    string BodyFontFamily,
    string MonoFontFamily,
    double BodyFontSize,
    double Heading1FontSize,
    double Heading2FontSize,
    double Heading3FontSize,
    double Heading4FontSize,
    double Heading5FontSize,
    double Heading6FontSize,
    double CodeFontSize,
    double TableFontSize,
    double LineHeightRatio,
    string PageBackgroundColor,
    string BodyColor,
    string HeadingColor,
    string MutedColor,
    string BorderColor,
    string CodeBackgroundColor,
    string CodeForegroundColor,
    string InlineCodeBackgroundColor,
    string InlineCodeForegroundColor,
    string LinkColor,
    string TableHeaderBackgroundColor,
    string QuoteBorderColor)
{
    private const string DefaultBodyFontFamily = "Inter, Microsoft YaHei UI, Segoe UI";
    private const string DefaultMonoFontFamily = "Cascadia Mono, Consolas";
    private const string CompactTypographySize = "Compact";

    public string QuoteBackgroundColor { get; init; } = PageBackgroundColor;

    public static MarkdownExportStyle Resolve(string? typographyTheme, string? typographySize)
    {
        var palette = ResolvePalette(typographyTheme);
        var scale = string.Equals(typographySize, CompactTypographySize, StringComparison.OrdinalIgnoreCase)
            ? 0.92d
            : 1d;

        return new MarkdownExportStyle(
            DefaultBodyFontFamily,
            DefaultMonoFontFamily,
            Scale(15, scale),
            Scale(32, scale),
            Scale(26, scale),
            Scale(22, scale),
            Scale(19, scale),
            Scale(17, scale),
            Scale(16, scale),
            Scale(13, scale),
            Scale(14, scale),
            1.55d,
            palette.PageBackground,
            palette.Body,
            palette.Heading,
            palette.Muted,
            palette.Border,
            palette.CodeBackground,
            palette.CodeForeground,
            palette.InlineCodeBackground,
            palette.InlineCodeForeground,
            palette.Link,
            palette.TableHeaderBackground,
            palette.QuoteBorder)
        {
            QuoteBackgroundColor = palette.QuoteBackground
        };
    }

    /// <summary>
    /// Creates an export style from Markdown typography resources.
    /// </summary>
    public static MarkdownExportStyle FromResources(
        IResourceNode resources,
        ThemeVariant? themeVariant = null,
        MarkdownExportStyle? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var resolvedFallback = fallback ?? Resolve(null, null);
        var targetTheme = themeVariant ?? ThemeVariant.Light;
        var bodyFontSize = GetDoubleResource(
            resources,
            MarkdownStyleKeys.ParagraphFontSizeResource,
            targetTheme,
            resolvedFallback.BodyFontSize);
        var paragraphLineHeight = GetDoubleResource(
            resources,
            MarkdownStyleKeys.ParagraphLineHeightResource,
            targetTheme,
            bodyFontSize * resolvedFallback.LineHeightRatio);
        var lineHeightRatio = bodyFontSize > 0
            ? Math.Round(paragraphLineHeight / bodyFontSize, 3)
            : resolvedFallback.LineHeightRatio;
        var textColor = GetColorResource(
            resources,
            MarkdownStyleKeys.TextBrushResource,
            targetTheme,
            resolvedFallback.BodyColor);
        var mutedColor = GetColorResource(
            resources,
            MarkdownStyleKeys.MutedTextBrushResource,
            targetTheme,
            resolvedFallback.MutedColor);
        var accentColor = GetColorResource(
            resources,
            MarkdownStyleKeys.AccentBrushResource,
            targetTheme,
            resolvedFallback.LinkColor);
        var borderColor = GetColorResource(
            resources,
            MarkdownStyleKeys.BorderBrushResource,
            targetTheme,
            resolvedFallback.BorderColor);

        return new MarkdownExportStyle(
            resolvedFallback.BodyFontFamily,
            resolvedFallback.MonoFontFamily,
            bodyFontSize,
            GetDoubleResource(resources, MarkdownStyleKeys.Heading1FontSizeResource, targetTheme, resolvedFallback.Heading1FontSize),
            GetDoubleResource(resources, MarkdownStyleKeys.Heading2FontSizeResource, targetTheme, resolvedFallback.Heading2FontSize),
            GetDoubleResource(resources, MarkdownStyleKeys.Heading3FontSizeResource, targetTheme, resolvedFallback.Heading3FontSize),
            GetDoubleResource(resources, MarkdownStyleKeys.Heading4FontSizeResource, targetTheme, resolvedFallback.Heading4FontSize),
            GetDoubleResource(resources, MarkdownStyleKeys.Heading5FontSizeResource, targetTheme, resolvedFallback.Heading5FontSize),
            GetDoubleResource(resources, MarkdownStyleKeys.Heading6FontSizeResource, targetTheme, resolvedFallback.Heading6FontSize),
            GetDoubleResource(resources, MarkdownStyleKeys.CodeBlockFontSizeResource, targetTheme, resolvedFallback.CodeFontSize),
            bodyFontSize,
            lineHeightRatio,
            resolvedFallback.PageBackgroundColor,
            textColor,
            textColor,
            mutedColor,
            borderColor,
            GetColorResource(resources, MarkdownStyleKeys.CodeBackgroundBrushResource, targetTheme, resolvedFallback.CodeBackgroundColor),
            textColor,
            GetColorResource(resources, MarkdownStyleKeys.InlineCodeBackgroundBrushResource, targetTheme, resolvedFallback.InlineCodeBackgroundColor),
            accentColor,
            accentColor,
            GetColorResource(resources, MarkdownStyleKeys.TableHeaderBackgroundBrushResource, targetTheme, resolvedFallback.TableHeaderBackgroundColor),
            borderColor)
        {
            QuoteBackgroundColor = GetColorResource(
                resources,
                MarkdownStyleKeys.QuoteBackgroundBrushResource,
                targetTheme,
                resolvedFallback.QuoteBackgroundColor)
        };
    }

    private static double Scale(double value, double scale)
    {
        return Math.Round(value * scale, 1);
    }

    private static double GetDoubleResource(
        IResourceNode resources,
        string key,
        ThemeVariant themeVariant,
        double fallback)
    {
        return resources.TryGetResource(key, themeVariant, out var value) && TryConvertToDouble(value, out var result)
            ? result
            : fallback;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double number:
                result = number;
                return true;
            case float number:
                result = number;
                return true;
            case int number:
                result = number;
                return true;
            case decimal number:
                result = (double)number;
                return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static string GetColorResource(
        IResourceNode resources,
        string key,
        ThemeVariant themeVariant,
        string fallback)
    {
        return resources.TryGetResource(key, themeVariant, out var value) && TryConvertToColor(value, out var color)
            ? ToCssColor(color)
            : fallback;
    }

    private static bool TryConvertToColor(object? value, out Color color)
    {
        switch (value)
        {
            case Color resolvedColor:
                color = resolvedColor;
                return true;
            case ISolidColorBrush brush:
                color = brush.Color;
                return true;
            case string text when Color.TryParse(text, out var parsed):
                color = parsed;
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static string ToCssColor(Color color)
    {
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static ExportPalette ResolvePalette(string? typographyTheme)
    {
        return typographyTheme?.Trim().ToLowerInvariant() switch
        {
            "inkblack" or "geekblack" => new ExportPalette(
                "#111827",
                "#e5e7eb",
                "#f9fafb",
                "#cbd5e1",
                "#334155",
                "#020617",
                "#e5e7eb",
                "#1f2937",
                "#f3f4f6",
                "#93c5fd",
                "#1e293b",
                "#64748b"),
            "orangeheart" => new ExportPalette(
                "#fffaf5",
                "#2f251e",
                "#ef7060",
                "#6b5a4c",
                "#fed7aa",
                "#2f1d12",
                "#fff7ed",
                "#ffedd5",
                "#7c2d12",
                "#ef7060",
                "#fff7ed",
                "#fb923c"),
            "yamabuki" => new ExportPalette(
                "#fffdf7",
                "#3a3a3a",
                "#515151",
                "#6b6255",
                "#ffe8b3",
                "#2f2615",
                "#fff7e6",
                "#fff7e6",
                "#7a4f00",
                "#ffb11b",
                "#fff7e6",
                "#ffb11b"),
            "tendergreen" => new ExportPalette(
                "#ffffff",
                "#595959",
                "#595959",
                "#6b6b6b",
                "#d9d9d9",
                "#2f2f2f",
                "#f7f7f7",
                "#efefef",
                "#595959",
                "#595959",
                "#f5f5f5",
                "#898989"),
            "verdant" => new ExportPalette(
                "#fbfefc",
                "#1f2f27",
                "#35b378",
                "#4b6356",
                "#bbf7d0",
                "#102018",
                "#f0fdf4",
                "#dcfce7",
                "#14532d",
                "#35b378",
                "#f0fdf4",
                "#4ade80"),
            "cutegreen" => new ExportPalette(
                "#fbfefc",
                "#3f3f3f",
                "#48b378",
                "#4a4a4a",
                "#ccebd8",
                "#102018",
                "#f0fdf4",
                "#dcfce7",
                "#14532d",
                "#48b378",
                "#f0fdf4",
                "#48b378"),
            "redscarlet" => new ExportPalette(
                "#fffafa",
                "#332020",
                "#f83929",
                "#6b4a4a",
                "#fecaca",
                "#2b1212",
                "#fef2f2",
                "#fee2e2",
                "#7f1d1d",
                "#f83929",
                "#fef2f2",
                "#f87171"),
            "colorfulpurple" => new ExportPalette(
                "#fffbff",
                "#595959",
                "#773098",
                "#65556f",
                "#e9d5ff",
                "#211827",
                "#faf5ff",
                "#f3e8ff",
                "#581c87",
                "#773098",
                "#faf5ff",
                "#c084fc"),
            "rosepurple" => new ExportPalette(
                "#fffbff",
                "#595959",
                "#595959",
                "#6b5f72",
                "#eadcff",
                "#211827",
                "#faf5ff",
                "#f3e8ff",
                "#581c87",
                "#b884f6",
                "#faf5ff",
                "#dec6fb"),
            "blueglow" => new ExportPalette(
                "#f8fbff",
                "#333333",
                "#333333",
                "#475569",
                "#bfdbfe",
                "#0f172a",
                "#eff6ff",
                "#dbeafe",
                "#1e3a8a",
                "#5c9dff",
                "#eff6ff",
                "#5c9dff"),
            "technologyblue" => new ExportPalette(
                "#f8fbff",
                "#1f2937",
                "#0e88eb",
                "#475569",
                "#bfdbfe",
                "#0f172a",
                "#eff6ff",
                "#dbeafe",
                "#1e3a8a",
                "#0e88eb",
                "#eff6ff",
                "#60a5fa"),
            "lanqing" => new ExportPalette(
                "#fbfffe",
                "#1f2937",
                "#009688",
                "#475569",
                "#b7ebe5",
                "#0f172a",
                "#eaf7f5",
                "#d6efec",
                "#065f57",
                "#009688",
                "#eaf7f5",
                "#009688"),
            "fullstackblue" => new ExportPalette(
                "#f8fbff",
                "#2b2b2b",
                "#40b8fa",
                "#475569",
                "#bfdbfe",
                "#0f172a",
                "#effaff",
                "#d8f1ff",
                "#075985",
                "#40b8fa",
                "#effaff",
                "#40b8fa"),
            _ => new ExportPalette(
                "#ffffff",
                "#333333",
                "#333333",
                "#4b5563",
                "#dfe2e5",
                "#F6F8FA",
                "#111827",
                "#efefef",
                "#e46918",
                "#3e64ff",
                "#f9fafb",
                "#dfe2e5")
        };
    }

    private readonly record struct ExportPalette(
        string PageBackground,
        string Body,
        string Heading,
        string Muted,
        string Border,
        string CodeBackground,
        string CodeForeground,
        string InlineCodeBackground,
        string InlineCodeForeground,
        string Link,
        string TableHeaderBackground,
        string QuoteBorder)
    {
        public string QuoteBackground => TableHeaderBackground;
    }
}
