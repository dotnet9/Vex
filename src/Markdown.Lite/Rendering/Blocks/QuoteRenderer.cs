using global::Avalonia.Controls;
using global::Avalonia.Data;
using global::Avalonia.Layout;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Themes;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 引用块渲染器：左边框 + 底色容器，内容递归走完整管线。
/// </summary>
internal sealed class QuoteRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not QuoteBlock quote)
        {
            return null;
        }

        var border = new Border();
        context.AddMarkdownClass(border, MarkdownStyleKeys.Quote);
        context.BindTheme(border, Border.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty, BindingPriority.Style);
        context.BindTheme(border, Border.BackgroundProperty, MarkdownViewer.QuoteBackgroundBrushProperty);
        var article = context is MarkdownViewer viewer ? ArticleTypographyResolver.Find(viewer.TypographyTheme) : null;
        if (article is not null) border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("CodeWFMarkdownQuoteBorderBrush"));

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        context.AddMarkdownClass(stack, MarkdownStyleKeys.QuoteContent);
        foreach (var child in quote)
        {
            var control = context.ConvertBlock(child, null);
            if (control is not null)
            {
                if (article is not null && control is SelectableTextBlock text)
                {
                    text.Margin = default;
                    text.TextAlignment = Enum.Parse<global::Avalonia.Media.TextAlignment>(article.Quote.Alignment);
                    text.FontSize = context.ParagraphFontSize * article.Quote.FontScale;
                    text.FontStyle = article.Quote.Italic ? global::Avalonia.Media.FontStyle.Italic : global::Avalonia.Media.FontStyle.Normal;
                    text.Bind(SelectableTextBlock.ForegroundProperty, text.GetResourceObservable("CodeWFMarkdownQuoteForeground"));
                }
                stack.Children.Add(control);
            }
        }

        border.Child = stack;
        return border;
    }
}
