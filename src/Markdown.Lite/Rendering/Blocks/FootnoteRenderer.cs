using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 脚注渲染器：分组容器（分割线 + 按序脚注）与单条脚注（序号标记 + 内容）。
/// </summary>
internal sealed class FootnoteRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        return block switch
        {
            FootnoteGroup footnotes => CreateFootnoteGroup(footnotes, context),
            Footnote footnote => CreateFootnote(footnote, context),
            _ => null
        };
    }

    private static Control CreateFootnoteGroup(FootnoteGroup footnotes, IMarkdownRenderContext context)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Margin = new Thickness(0, 18, 0, 8)
        };
        context.AddMarkdownClass(panel, MarkdownStyleKeys.List);

        panel.Children.Add(CreateThematicBreak(context));
        foreach (var footnote in footnotes.OfType<Footnote>().OrderBy(note => note.Order))
        {
            panel.Children.Add(CreateFootnote(footnote, context));
        }

        return panel;
    }

    private static Control CreateThematicBreak(IMarkdownRenderContext context)
    {
        var border = new Border();
        context.AddMarkdownClass(border, MarkdownStyleKeys.ThematicBreak);
        context.BindTheme(border, Border.BackgroundProperty, MarkdownViewer.BorderLineBrushProperty);
        return border;
    }

    private static Control CreateFootnote(Footnote footnote, IMarkdownRenderContext context)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            }
        };
        context.AddMarkdownClass(grid, MarkdownStyleKeys.ListItem);

        var marker = context.CreateSelectableText(MarkdownStyleKeys.ListMarker);
        marker.Text = $"[{Math.Max(1, footnote.Order)}]";
        marker.MinWidth = context.OrderedListMarkerMinWidth;
        marker.TextAlignment = TextAlignment.Right;
        marker.VerticalAlignment = VerticalAlignment.Top;
        context.BindTheme(marker, SelectableTextBlock.ForegroundProperty, MarkdownViewer.AccentBrushProperty);
        context.BindTheme(marker, SelectableTextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        context.BindTheme(marker, SelectableTextBlock.FontSizeProperty, MarkdownViewer.ParagraphFontSizeProperty);
        context.BindTheme(marker, SelectableTextBlock.LineHeightProperty, MarkdownViewer.ParagraphLineHeightProperty);
        Grid.SetColumn(marker, 0);
        grid.Children.Add(marker);

        var content = new StackPanel { Orientation = Orientation.Vertical };
        context.AddMarkdownClass(content, MarkdownStyleKeys.ListItemContent);
        var firstParagraph = true;
        foreach (var block in footnote)
        {
            var child = block is ParagraphBlock paragraph
                ? context.CreateParagraph(
                    paragraph,
                    false,
                    firstParagraph ? context.ListFirstParagraphMargin : context.ListNestedParagraphMargin)
                : context.ConvertBlock(block, null);
            firstParagraph = false;

            if (child is not null)
            {
                content.Children.Add(child);
            }
        }

        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }
}
