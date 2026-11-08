using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Styling;
using CodeWF.Avalonia.Markdown;
using CodeWF.Avalonia.Markdown.Themes;

namespace Vex.Modules.Shell.Views;

/// <summary>文章目录的三行预览；使用原始排版定义，不继承桌面正文装饰和渲染密度。</summary>
internal sealed class TypographyCardPreview : UserControl
{
    private readonly ArticleTypography _article;
    private readonly string _name, _purpose, _quote;

    public TypographyCardPreview(string? key, string name, string purpose, string quote)
    {
        _article = ArticleTypographyResolver.Find(key) ?? ArticleTypographyCatalog.All.First();
        (_name, _purpose, _quote) = (name, purpose, quote);
        Classes.Add("typography-preview");
        Tag = key;
        IsHitTestVisible = false;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
        if (_article.Font != "system") FontFamily = ArticleTypography.ResolveFont(_article.Font);
        var resources = _article.CreateResources();
        foreach (var (variant, dark) in new[] { (ThemeVariant.Light, false), (ThemeVariant.Dark, true) })
        {
            var colors = (ResourceDictionary)resources.ThemeDictionaries[variant];
            colors["PreviewHeadingBackground"] = Tint(_article.Headings[1].BackgroundColor, _article.Headings[1].Tint, dark);
            colors["PreviewQuoteBackground"] = Tint("var(--type-decoration)", _article.Quote.Tint, dark);
        }
        Resources.MergedDictionaries.Add(resources);
        Build();
    }

    private void Build()
    {
        var heading = _article.Headings[1];
        var scale = 13 / _article.BodySize;
        var title = new TextBlock
        {
            Classes = { "typography-preview-heading" },
            Text = _name, FontSize = heading.Size * scale, LineHeight = heading.LineHeight * scale,
            FontWeight = (FontWeight)heading.Weight, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = Enum.Parse<TextAlignment>(heading.Alignment)
        };
        title.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(heading.Foreground.Contains("--fg)", StringComparison.Ordinal)
            ? "VexValueBrush" : "CodeWFMarkdownHeading2Foreground"));
        // 卡片的 --mono 未定义时由 CSS 继承文章字体，不能强行切到系统等宽字体。
        var padding = Thickness.Parse(heading.Padding);
        var titleFrame = new Border
        {
            Child = title, Margin = new Thickness(0, 0, 0, 5),
            Padding = new Thickness(padding.Left * scale, padding.Top * scale, padding.Right * scale, padding.Bottom * scale),
            BorderThickness = Thickness.Parse(heading.Border), CornerRadius = heading.Corners ?? new CornerRadius(heading.Radius)
        };
        var headingBorderKey = heading.BorderColor.Contains("--border)", StringComparison.Ordinal)
            ? "VexPanelBorderBrush" : "CodeWFMarkdownHeading2BorderBrush";
        titleFrame.Bind(Border.BackgroundProperty, this.GetResourceObservable("PreviewHeadingBackground"));
        if (!heading.DashedBorder)
            titleFrame.Bind(Border.BorderBrushProperty, this.GetResourceObservable(headingBorderKey));
        Control titleBlock = titleFrame;
        if (heading.DashedBorder)
        {
            var stroke = titleFrame.BorderThickness.Bottom;
            var underline = new Line
            {
                StartPoint = default, EndPoint = new Point(1, 0), Stretch = Stretch.Fill,
                StrokeThickness = stroke,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 2 },
                Height = stroke, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, stroke / 2), IsHitTestVisible = false,
                Classes = { "typography-preview-dash" }
            };
            underline.Bind(Shape.StrokeProperty, this.GetResourceObservable(headingBorderKey));
            titleFrame.BorderBrush = Brushes.Transparent;
            titleFrame.Margin = default;
            titleBlock = new Panel { Children = { titleFrame, underline }, Margin = new Thickness(0, 0, 0, 5) };
        }
        var purpose = Label(_purpose, 1);
        purpose.Margin = new Thickness(_article.ParagraphIndent * 12 / _article.BodySize, 0, 0, 7);
        purpose.Classes.Add("typography-preview-purpose");
        var quote = Label(_quote, 2);
        quote.TextAlignment = Enum.Parse<TextAlignment>(_article.ParagraphAlignment);
        quote.Classes.Add("typography-preview-quote");
        var quoteFrame = new Border
        {
            Child = quote, Padding = new Thickness(9, 6),
            BorderThickness = Thickness.Parse(_article.Quote.Border),
            CornerRadius = _article.Quote.Corners ?? new CornerRadius(0, _article.Quote.Radius, _article.Quote.Radius, 0)
        };
        quoteFrame.Bind(Border.BorderBrushProperty, this.GetResourceObservable("CodeWFMarkdownQuoteBorderBrush"));
        quoteFrame.Bind(Border.BackgroundProperty, this.GetResourceObservable("PreviewQuoteBackground"));
        Content = new Border
        {
            Padding = new Thickness(11, 8),
            Child = new StackPanel { Children = { titleBlock, purpose, quoteFrame } }
        };
    }

    private IBrush Tint(string source, double amount, bool dark)
    {
        if (amount == 0) return Brushes.Transparent;
        var color = Color.Parse(source.Contains("--typography-accent)", StringComparison.Ordinal)
            ? dark ? _article.DarkText : _article.LightText
            : dark ? _article.DarkDecoration : _article.LightDecoration);
        return new SolidColorBrush(color, amount);
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
