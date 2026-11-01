using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CodeWF.Avalonia.Markdown;
using CodeWF.Avalonia.Markdown.Themes;
using Vex.Core.Theme;

namespace Vex.Modules.Shell.Views;

/// <summary>文章目录的三行预览；使用原始排版定义，不继承桌面正文装饰和渲染密度。</summary>
internal sealed class TypographyCardPreview : UserControl
{
    private readonly ArticleTypography _article;
    private readonly string _name, _purpose, _quote;

    public TypographyCardPreview(string key, string name, string purpose, string quote)
    {
        _article = ArticleTypographyResolver.Find(key) ?? ArticleTypographyCatalog.All.First();
        (_name, _purpose, _quote) = (name, purpose, quote);
        Classes.Add("typography-preview");
        Tag = key;
        IsHitTestVisible = false;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
        if (_article.Font != "system") FontFamily = ArticleTypography.ResolveFont(_article.Font);
        ActualThemeVariantChanged += (_, _) => Build();
        Build();
    }

    private void Build()
    {
        var heading = _article.Headings[1];
        var scale = 13 / _article.BodySize;
        var resources = _article.CreateResources();
        var colors = (ResourceDictionary)resources.ThemeDictionaries[
            ElevationResources.IsDarkVariant(ActualThemeVariant) ? ThemeVariant.Dark : ThemeVariant.Light];
        var title = new TextBlock
        {
            Classes = { "typography-preview-heading" },
            Text = _name, FontSize = heading.Size * scale, LineHeight = heading.LineHeight * scale,
            FontWeight = (FontWeight)heading.Weight, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = Enum.Parse<TextAlignment>(heading.Alignment),
            Foreground = (IBrush)colors["CodeWFMarkdownHeading2Foreground"]!
        };
        if (heading.Foreground.Contains("--fg)", StringComparison.Ordinal))
            title.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexValueBrush"));
        var font = heading.Font == "system" ? _article.Font : heading.Font;
        if (font != "system") title.FontFamily = ArticleTypography.ResolveFont(font);
        var padding = Thickness.Parse(heading.Padding);
        var titleFrame = new Border
        {
            Child = title, Margin = new Thickness(0, 0, 0, 5),
            Padding = new Thickness(padding.Left * scale, padding.Top * scale, padding.Right * scale, padding.Bottom * scale),
            BorderThickness = Thickness.Parse(heading.Border), CornerRadius = new CornerRadius(heading.Radius),
            BorderBrush = (IBrush)colors["CodeWFMarkdownHeading2BorderBrush"]!,
            Background = (IBrush)colors["CodeWFMarkdownHeading2Background"]!
        };
        var purpose = Label(_purpose, 1);
        purpose.Classes.Add("typography-preview-purpose");
        var quote = Label(_quote, 2);
        quote.Classes.Add("typography-preview-quote");
        var quoteFrame = new Border
        {
            Child = quote, Padding = new Thickness(9, 6),
            BorderThickness = Thickness.Parse(_article.Quote.Border),
            CornerRadius = new CornerRadius(0, _article.Quote.Radius, _article.Quote.Radius, 0),
            BorderBrush = (IBrush)colors["CodeWFMarkdownQuoteBorderBrush"]!,
            Background = (IBrush)colors[MarkdownStyleKeys.QuoteBackgroundBrushResource]!
        };
        Content = new Border
        {
            Padding = new Thickness(11, 8),
            Child = new StackPanel { Children = { titleFrame, purpose, quoteFrame } }
        };
    }

    private TextBlock Label(string text, int lines)
    {
        var label = new TextBlock
        {
            Text = text, FontSize = 12, LineHeight = 18, MaxLines = lines,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 7)
        };
        label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexMutedBrush"));
        return label;
    }
}
