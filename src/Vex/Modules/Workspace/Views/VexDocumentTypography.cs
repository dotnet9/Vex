using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CodeWF.Avalonia.Markdown;
using CodeWF.Avalonia.Markdown.Themes;

namespace Vex.Modules.Workspace.Views;

/// <summary>Desktop document polish from professional.css, layered over the reusable article definitions.</summary>
internal static class VexDocumentTypography
{
    public static void Register()
    {
        foreach (var article in ArticleTypographyCatalog.All)
            MarkdownTypographyThemeRegistry.Register(article.Key, () => Create(article));
        // 颜色画刷已绑定应用主题；明暗切换无需重建字号、间距和所有正文控件的排版字典。
    }

    private static ResourceDictionary Create(ArticleTypography article)
    {
        var resources = article.CreateResources();
        resources["CodeWFMarkdownQuoteCornerRadius"] = new CornerRadius(0);
        resources["CodeWFMarkdownListSpacing"] = article.BodySize * .34;
        resources["CodeWFMarkdownListItemMargin"] = new Thickness(article.BodySize * .18, 0, 0, 0);
        resources["CodeWFMarkdownListMargin"] = new Thickness(0, 0, 0,
            article.BodySize * 1.15 - Math.Max(0, Thickness.Parse(article.Quote.Margin).Top - 8));
        resources["CodeWFMarkdownTableCellPadding"] = new Thickness(13, 10);
        resources["CodeWFMarkdownTableCellFontSize"] = article.BodySize * .75;
        resources["CodeWFMarkdownTableCellLineHeight"] = article.BodySize * article.Leading * .75;
        resources["CodeWFMarkdownTableCornerRadius"] = new CornerRadius(10);
        resources["VexTableInnerMargin"] = new Thickness(0, article.BodySize * .75 * 1.35);
        resources["VexTableBlockMargin"] = new Thickness(0,
            Math.Max(0, article.BodySize - article.Headings[1].Size * .62), 0, article.BodySize * .85);
        resources["VexLiveListItemMargin"] = new Thickness(22 - article.BodySize * 1.55, 8, 0, 8);
        var quoteMargin = Thickness.Parse(article.Quote.Margin);
        resources["VexLiveQuoteMargin"] = quoteMargin;
        resources["VexLiveQuoteBeforeHeadingMargin"] = new Thickness(quoteMargin.Left, quoteMargin.Top, quoteMargin.Right, 0);
        resources["VexLiveTableMargin"] = new Thickness(0, 14, 0, 14);
        resources["VexLiveTableBeforeHeadingMargin"] = new Thickness(0, 14, 0, 0);
        for (var level = 0; level < article.Headings.Length; level++)
        {
            var heading = article.Headings[level];
            var margin = Thickness.Parse(heading.Margin);
            if (article.Key is "Simple" or "Basic" or "SoftSage")
                margin = level switch
                {
                    1 => new Thickness(0, heading.Size * 1.8, 0, heading.Size * .62),
                    2 => new Thickness(0, heading.Size * 1.35, 0, heading.Size * .35),
                    _ => margin
                };
            resources[$"VexLiveHeading{level + 1}Margin"] = margin;
            resources[$"VexLiveHeading{level + 1}BeforeHeadingMargin"] = new Thickness(margin.Left, margin.Top, margin.Right, 0);
        }
        if (article.Key is "Simple" or "Basic" or "SoftSage")
        {
            resources["CodeWFMarkdownHeading1TextAlignment"] = TextAlignment.Left;
            resources["CodeWFMarkdownHeading1BorderThickness"] = new Thickness(0, 0, 0, 1);
            resources["CodeWFMarkdownHeading1Padding"] = new Thickness(0, 0, 0, 14);
            resources["CodeWFMarkdownHeading2TextAlignment"] = TextAlignment.Left;
            resources["CodeWFMarkdownHeading2BorderThickness"] = new Thickness(3, 0, 0, 0);
            resources["CodeWFMarkdownHeading2Padding"] = new Thickness(14, 2, 0, 2);
            resources["CodeWFMarkdownHeading2CornerRadius"] = new CornerRadius(0);
            resources["CodeWFMarkdownHeading2Margin"] = new Thickness(0, Math.Max(0, article.Headings[1].Size * 1.8 - article.BodySize * .85), 0, article.Headings[1].Size * .62);
            resources["CodeWFMarkdownHeading3Margin"] = new Thickness(0, Math.Max(0, article.Headings[2].Size * 1.35 - article.BodySize * .85), 0, article.Headings[2].Size * .35);
            resources["CodeWFMarkdownHeading3FontWeight"] = (FontWeight)650;
        }
        for (var level = 0; level < article.Headings.Length; level++)
        {
            var margin = (Thickness)resources[$"CodeWFMarkdownHeading{level + 1}Margin"]!;
            var originalTop = level switch
            {
                1 when article.Key is "Simple" or "Basic" or "SoftSage" => article.Headings[level].Size * 1.8,
                2 when article.Key is "Simple" or "Basic" or "SoftSage" => article.Headings[level].Size * 1.35,
                _ => Thickness.Parse(article.Headings[level].Margin).Top
            };
            resources[$"VexReadHeading{level + 1}AfterQuoteMargin"] =
                new Thickness(margin.Left, Math.Max(0, originalTop - quoteMargin.Bottom), margin.Right, margin.Bottom);
        }
        foreach (var dictionary in resources.ThemeDictionaries.Values.OfType<ResourceDictionary>())
        {
            var foreground = Brush("VexEditorForegroundBrush", Brushes.Black);
            var muted = Brush("VexMutedBrush", Brushes.Gray);
            dictionary[MarkdownStyleKeys.TextBrushResource] = foreground;
            dictionary[MarkdownStyleKeys.MutedTextBrushResource] = muted;
            if (article.Quote.Foreground.Contains("--muted)", StringComparison.Ordinal))
                dictionary["CodeWFMarkdownQuoteForeground"] = muted;
            for (var level = 0; level < article.Headings.Length; level++)
                if (article.Headings[level].Tint != 1
                    && article.Headings[level].Foreground.Contains("--fg)", StringComparison.Ordinal))
                    dictionary[$"CodeWFMarkdownHeading{level + 1}Foreground"] = foreground;
            dictionary[MarkdownStyleKeys.QuoteBackgroundBrushResource] = Brushes.Transparent;
            if (article.Key is not ("Simple" or "Basic" or "SoftSage")) continue;
            dictionary["CodeWFMarkdownHeading1BorderBrush"] = Brush("VexPanelBorderBrush", Brushes.Gray);
            dictionary["CodeWFMarkdownHeading2BorderBrush"] = Brush("VexAccentBrush", Brushes.Gray);
            dictionary["CodeWFMarkdownHeading2Background"] = Brushes.Transparent;
            dictionary["CodeWFMarkdownHeading2Foreground"] = foreground;
        }
        return resources;
    }

    private static IBrush Brush(string key, IBrush fallback) =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true
            && value is IBrush brush ? brush : fallback;
}
