using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using CodeWF.Avalonia.Markdown.Controls;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 水平分割线渲染器。
/// </summary>
internal sealed class ThematicBreakRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not ThematicBreakBlock)
        {
            return null;
        }

        var border = new Border();
        context.AddMarkdownClass(border, MarkdownStyleKeys.ThematicBreak);
        context.BindTheme(border, Border.BackgroundProperty, MarkdownViewer.BorderLineBrushProperty);
        return border;
    }
}
