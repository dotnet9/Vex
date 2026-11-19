using System.Text.RegularExpressions;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// HTML 块渲染器：不可见锚点、受限的 style span（对齐/颜色）、
/// 多图幻灯片回落，其余降级为弱化文本。
/// </summary>
internal sealed class HtmlBlockRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not HtmlBlock htmlBlock)
        {
            return null;
        }

        return CreateHtmlBlock(htmlBlock.Lines.ToString(), context);
    }

    private static Control CreateHtmlBlock(string html, IMarkdownRenderContext context)
    {
        var trimmed = html.Trim();
        if (Regex.IsMatch(trimmed, @"^<a\s+[^>]*id\s*=\s*[""'][^""']+[""'][^>]*>\s*</a>$", RegexOptions.IgnoreCase))
        {
            return new Border { Height = 0, IsHitTestVisible = false };
        }

        if (TryCreateStyledSpan(trimmed, context) is { } spanBlock)
        {
            return spanBlock;
        }

        if (SpecialBlockRenderer.TryCreateSlideBlock(trimmed, context) is { } slideBlock)
        {
            return slideBlock;
        }

        return context.CreateFallbackText(html, MarkdownStyleKeys.HtmlBlock);
    }

    private static Control? TryCreateStyledSpan(string html, IMarkdownRenderContext context)
    {
        var match = Regex.Match(
            html,
            @"^<span\s+[^>]*style\s*=\s*[""'](?<style>[^""']*)[""'][^>]*>(?<text>.*?)</span>$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        var textBlock = context.CreateSelectableText(MarkdownStyleKeys.HtmlBlock);
        textBlock.Text = Regex.Replace(match.Groups["text"].Value, "<.*?>", string.Empty);
        context.BindTheme(textBlock, SelectableTextBlock.ForegroundProperty, MarkdownViewer.TextBrushProperty);
        context.BindTheme(textBlock, SelectableTextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        context.BindTheme(textBlock, SelectableTextBlock.FontSizeProperty, MarkdownViewer.ParagraphFontSizeProperty);
        context.BindTheme(textBlock, SelectableTextBlock.LineHeightProperty, MarkdownViewer.ParagraphLineHeightProperty);

        var style = match.Groups["style"].Value;
        if (style.Contains("text-align:center", StringComparison.OrdinalIgnoreCase))
        {
            textBlock.TextAlignment = TextAlignment.Center;
        }
        else if (style.Contains("text-align:right", StringComparison.OrdinalIgnoreCase))
        {
            textBlock.TextAlignment = TextAlignment.Right;
        }

        var colorMatch = Regex.Match(style, @"color\s*:\s*(?<color>#[0-9a-fA-F]{3,8}|[a-zA-Z]+)");
        if (colorMatch.Success && Color.TryParse(colorMatch.Groups["color"].Value, out var color))
        {
            textBlock.Foreground = new SolidColorBrush(color);
        }

        return textBlock;
    }
}
