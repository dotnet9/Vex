using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Styling;

namespace CodeWF.Avalonia.Markdown.Themes;

public sealed record ArticleHeadingStyle(double Size, int Weight, string Alignment, string Border, string Padding,
    double Tint, string Foreground, double Radius, string Font, double LineHeight, string Margin)
{
    public string BorderColor { get; init; } = "var(--type-decoration)";
    public string BackgroundColor { get; init; } = "var(--type-decoration)";
    public bool DashedBorder { get; init; }
    public CornerRadius? Corners { get; init; }
}

public sealed record ArticleQuoteStyle(string Border, string Padding, string Margin, double Radius,
    double Tint, string Alignment, string Foreground, double FontScale, bool Italic)
{
    public string BorderColor { get; init; } = "var(--type-decoration)";
    public CornerRadius? Corners { get; init; }
}

/// <summary>正文、卡片和导出共用的文章排版定义；标题装饰与文字强调分别取色。</summary>
public sealed record ArticleTypography(string Key, string Name, string EnglishName, string Purpose, bool Recommended,
    double BodySize, double Leading, string LightText, string DarkText, string LightDecoration, string DarkDecoration,
    string Font, ArticleHeadingStyle[] Headings, string ExportCss, string CompactExportCss,
    ArticleQuoteStyle Quote, string ParagraphAlignment)
{
    public double ParagraphIndent { get; init; }

    public static FontFamily ResolveFont(string font) => new(font switch
    {
        "serif" => "Noto Serif SC, Songti SC, SimSun, serif",
        "mono" => "Cascadia Mono, Consolas, Menlo, monospace",
        _ => "Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans SC"
    });
    public ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary
        {
            [MarkdownStyleKeys.ParagraphFontSizeResource] = BodySize,
            [MarkdownStyleKeys.ParagraphLineHeightResource] = BodySize * Leading,
            ["CodeWFMarkdownBlockSpacing"] = 0d,
            ["CodeWFMarkdownParagraphMargin"] = new Thickness(0, 0, 0, BodySize * .85),
            // 目录 margin 是 CSS 折叠后语义，Avalonia 相邻 margin 相加：标题/引用顶边距扣除前块下边距，
            // 表格下边距按段落块语义补齐，保证跨引擎的块节奏一致。
            ["CodeWFMarkdownHeadingMargin"] = new Thickness(0, Math.Max(0, BodySize * 1.4 - BodySize * .85), 0, BodySize * .45),
            ["CodeWFMarkdownQuotePadding"] = Thickness.Parse(Quote.Padding),
            ["CodeWFMarkdownQuoteMargin"] = WithReducedTop(Thickness.Parse(Quote.Margin), 8),
            ["CodeWFMarkdownQuoteBorderThickness"] = Thickness.Parse(Quote.Border),
            ["CodeWFMarkdownQuoteCornerRadius"] = Quote.Corners ?? new CornerRadius(Quote.Radius),
            ["CodeWFMarkdownTableMargin"] = new Thickness(0, 0, 0, BodySize * .85),
        };
        for (var i = 0; i < Headings.Length; i++)
        {
            var h = Headings[i]; var prefix = "CodeWFMarkdownHeading" + (i + 1);
            resources[prefix + "FontSize"] = h.Size;
            resources[prefix + "LineHeight"] = h.LineHeight;
            resources[prefix + "Margin"] = WithReducedTop(Thickness.Parse(h.Margin), BodySize * .85);
            resources[prefix + "FontWeight"] = (FontWeight)h.Weight;
            resources[prefix + "TextAlignment"] = Enum.Parse<TextAlignment>(h.Alignment);
            resources[prefix + "BorderThickness"] = Thickness.Parse(h.Border);
            resources[prefix + "Padding"] = Thickness.Parse(h.Padding);
            resources[prefix + "CornerRadius"] = h.Corners ?? new CornerRadius(h.Radius);
            resources[prefix + "DashedBorder"] = h.DashedBorder;
        }
        resources.ThemeDictionaries[ThemeVariant.Light] = Colors(false);
        resources.ThemeDictionaries[ThemeVariant.Dark] = Colors(true);
        return resources;
    }

    /// <summary>扣除前块折叠掉的下边距，顶边距不足时归零。</summary>
    private static Thickness WithReducedTop(Thickness margin, double reduction) =>
        new(margin.Left, Math.Max(0, margin.Top - reduction), margin.Right, margin.Bottom);

    private ResourceDictionary Colors(bool dark)
    {
        var foreground = Brush.Parse(dark ? "#E4E5E7" : "#30343B");
        var accent = Brush.Parse(dark ? DarkText : LightText);
        var decoration = Color.Parse(dark ? DarkDecoration : LightDecoration);
        var paper = Color.Parse(dark ? "#202124" : "#FFFFFF");
        var border = Brush.Parse(dark ? "#484C53" : "#DDE1E5");
        IBrush ColorBrush(string expression) => expression.Contains("--border)", StringComparison.Ordinal) ? border
            : expression.Contains("--typography-accent)", StringComparison.Ordinal) ? accent
            : expression.Contains("--fg)", StringComparison.Ordinal) ? foreground
            : new SolidColorBrush(decoration);
        var resources = new ResourceDictionary
        {
            [MarkdownStyleKeys.TextBrushResource] = foreground,
            [MarkdownStyleKeys.AccentBrushResource] = accent,
            [MarkdownStyleKeys.MutedTextBrushResource] = Brush.Parse(dark ? "#AFB4BB" : "#606770"),
            [MarkdownStyleKeys.BorderBrushResource] = Brush.Parse(dark ? "#484C53" : "#DDE1E5"),
            [MarkdownStyleKeys.QuoteBackgroundBrushResource] = Quote.Tint == 0 ? Brushes.Transparent : new SolidColorBrush(Mix(paper, decoration, Quote.Tint)),
            ["CodeWFMarkdownQuoteBorderBrush"] = ColorBrush(Quote.BorderColor),
            ["CodeWFMarkdownQuoteForeground"] = Quote.Foreground.Contains("--muted)", StringComparison.Ordinal) ? Brush.Parse(dark ? "#AFB4BB" : "#606770") : accent,
            [MarkdownStyleKeys.CodeBackgroundBrushResource] = Brush.Parse(dark ? "#292B2F" : "#F6F7F8"),
            [MarkdownStyleKeys.InlineCodeBackgroundBrushResource] = Brush.Parse(dark ? "#292B2F" : "#F6F7F8"),
            [MarkdownStyleKeys.TableHeaderBackgroundBrushResource] = Brush.Parse(dark ? "#292B2F" : "#F6F7F8"),
        };
        for (var i = 0; i < Headings.Length; i++)
        {
            var h = Headings[i]; var prefix = "CodeWFMarkdownHeading" + (i + 1);
            resources[prefix + "Foreground"] = h.Tint == 1 ? Brush.Parse(OnSolid(decoration))
                : h.Foreground.Contains("--fg)", StringComparison.Ordinal) ? foreground : accent;
            resources[prefix + "Background"] = h.Tint == 0 ? Brushes.Transparent
                : new SolidColorBrush(Mix(paper, ((ISolidColorBrush)ColorBrush(h.BackgroundColor)).Color, h.Tint));
            resources[prefix + "BorderBrush"] = ColorBrush(h.BorderColor);
        }
        return resources;
    }

    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)Math.Round(a.R * (1 - amount) + b.R * amount), (byte)Math.Round(a.G * (1 - amount) + b.G * amount),
        (byte)Math.Round(a.B * (1 - amount) + b.B * amount));

    private static string OnSolid(Color color)
    {
        static double Linear(byte v) => v / 255d <= .04045 ? v / 255d / 12.92 : Math.Pow((v / 255d + .055) / 1.055, 2.4);
        var luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        return (1.05 / (luminance + .05)) >= ((luminance + .05) / .065) ? "#FFFFFF" : "#17211C";
    }
}
