using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Data;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Syntax;
using CodeWF.Avalonia.Markdown.Themes;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 标题渲染器：下边框强调 + 按级别取字号，内容走内联转换与链接交互。
/// </summary>
internal sealed class HeadingRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not HeadingBlock heading)
        {
            return null;
        }

        var border = new Border();
        context.AddMarkdownClass(
            border,
            MarkdownStyleKeys.HeadingBorder,
            MarkdownStyleKeys.GetHeadingBorderClass(heading.Level));
        context.BindTheme(border, Border.BorderBrushProperty, MarkdownViewer.AccentBrushProperty, BindingPriority.Style);
        context.BindTheme(border, Layoutable.MarginProperty, MarkdownViewer.HeadingMarginProperty, BindingPriority.Style);

        var textBlock = context.CreateSelectableText(MarkdownStyleKeys.Heading, MarkdownStyleKeys.GetHeadingClass(heading.Level));
        textBlock.FontWeight = FontWeight.Bold;
        // 读屏按标题级别导航（Avalonia 12 无 TextPattern，级别是最有用的语义信息）。
        global::Avalonia.Automation.AutomationProperties.SetHeadingLevel(textBlock, heading.Level);
        context.BindTheme(textBlock, SelectableTextBlock.ForegroundProperty, MarkdownViewer.TextBrushProperty, BindingPriority.Style);
        context.BindTheme(textBlock, SelectableTextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        context.BindTheme(textBlock, SelectableTextBlock.FontSizeProperty, GetHeadingFontSizeProperty(heading.Level));

        foreach (var inline in context.ConvertInlines(heading.Inline, false))
        {
            textBlock.Inlines?.Add(inline);
        }

        context.AttachLinkInteraction(textBlock, heading.Inline, false);

        if (context is MarkdownViewer viewer && ArticleTypographyResolver.Find(viewer.TypographyTheme) is { } article)
        {
            var prefix = "CodeWFMarkdownHeading" + heading.Level;
            border.Bind(Layoutable.MarginProperty, border.GetResourceObservable(prefix + "Margin"), BindingPriority.Style);
            border.Bind(Border.BorderThicknessProperty, border.GetResourceObservable(prefix + "BorderThickness"));
            border.Bind(Border.PaddingProperty, border.GetResourceObservable(prefix + "Padding"));
            border.Bind(Border.CornerRadiusProperty, border.GetResourceObservable(prefix + "CornerRadius"));
            border.Bind(Border.BackgroundProperty, border.GetResourceObservable(prefix + "Background"));
            if (!article.Headings[heading.Level - 1].DashedBorder)
                border.Bind(Border.BorderBrushProperty, border.GetResourceObservable(prefix + "BorderBrush"));
            textBlock.Bind(SelectableTextBlock.ForegroundProperty, textBlock.GetResourceObservable(prefix + "Foreground"));
            textBlock.Bind(SelectableTextBlock.FontWeightProperty, textBlock.GetResourceObservable(prefix + "FontWeight"));
            textBlock.Bind(SelectableTextBlock.TextAlignmentProperty, textBlock.GetResourceObservable(prefix + "TextAlignment"));
            textBlock.Bind(SelectableTextBlock.LineHeightProperty, textBlock.GetResourceObservable(prefix + "LineHeight"));
            var font = article.Headings[heading.Level - 1].Font == "system" ? article.Font : article.Headings[heading.Level - 1].Font;
            if (font != "system") textBlock.FontFamily = ArticleTypography.ResolveFont(font);
            if (article.Headings[heading.Level - 1].DashedBorder)
            {
                var decoration = new DashedHeadingDecoration { IsHitTestVisible = false };
                decoration.Bind(Layoutable.MarginProperty, border.GetObservable(Layoutable.MarginProperty));
                decoration.Bind(DashedHeadingDecoration.StrokeProperty, border.GetResourceObservable(prefix + "BorderBrush"));
                decoration.Bind(DashedHeadingDecoration.ThicknessProperty, border.GetResourceObservable(prefix + "BorderThickness"));
                border.BorderBrush = Brushes.Transparent;
                border.Child = textBlock;
                return new Panel { Children = { border, decoration } };
            }
        }

        border.Child = textBlock;
        return border;
    }

    private sealed class DashedHeadingDecoration : Control
    {
        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<DashedHeadingDecoration, IBrush?>("Stroke");
        public static readonly StyledProperty<Thickness> ThicknessProperty =
            AvaloniaProperty.Register<DashedHeadingDecoration, Thickness>("Thickness");

        private static readonly IDashStyle HeadingDash = new DashStyle(new double[] { 3, 2 }, 0);

        static DashedHeadingDecoration() => AffectsRender<DashedHeadingDecoration>(StrokeProperty, ThicknessProperty);

        public override void Render(DrawingContext context)
        {
            var border = GetValue(ThicknessProperty);
            var size = Bounds.Size;
            DrawSide(border.Left, new Point(border.Left / 2, 0), new Point(border.Left / 2, size.Height));
            DrawSide(border.Top, new Point(0, border.Top / 2), new Point(size.Width, border.Top / 2));
            DrawSide(border.Right, new Point(size.Width - border.Right / 2, 0), new Point(size.Width - border.Right / 2, size.Height));
            DrawSide(border.Bottom, new Point(0, size.Height - border.Bottom / 2), new Point(size.Width, size.Height - border.Bottom / 2));

            void DrawSide(double thickness, Point start, Point end)
            {
                if (thickness > 0 && GetValue(StrokeProperty) is { } brush)
                    context.DrawLine(new Pen(brush, thickness, HeadingDash), start, end);
            }
        }
    }

    private static StyledProperty<double> GetHeadingFontSizeProperty(int level)
    {
        return level switch
        {
            1 => MarkdownViewer.Heading1FontSizeProperty,
            2 => MarkdownViewer.Heading2FontSizeProperty,
            3 => MarkdownViewer.Heading3FontSizeProperty,
            4 => MarkdownViewer.Heading4FontSizeProperty,
            5 => MarkdownViewer.Heading5FontSizeProperty,
            _ => MarkdownViewer.Heading6FontSizeProperty
        };
    }
}
