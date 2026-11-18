using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 列表渲染器：有序/无序列表、任务列表勾选框与标记列宽计算。
/// </summary>
internal sealed class ListRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not ListBlock list)
        {
            return null;
        }

        var items = list.OfType<ListItemBlock>().ToList();
        var startIndex = GetOrderedStart(list);
        var markerWidth = CalculateListMarkerWidth(list.IsOrdered, startIndex, items.Count, context);
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        context.AddMarkdownClass(panel, MarkdownStyleKeys.List);

        for (var i = 0; i < items.Count; i++)
        {
            panel.Children.Add(CreateListItem(items[i], list.IsOrdered, startIndex + i, markerWidth, context));
        }

        return panel;
    }

    private static Control CreateListItem(ListItemBlock item, bool ordered, int index, double markerWidth, IMarkdownRenderContext context)
    {
        var itemGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            }
        };
        context.AddMarkdownClass(itemGrid, MarkdownStyleKeys.ListItem);

        var isTask = MarkdownTaskListHelper.TryReadTaskState(item, out var isChecked);
        Control marker = isTask
            ? CreateTaskMarker(isChecked, markerWidth, item, context)
            : CreateListMarker(ordered ? $"{index}." : "•", markerWidth, context);

        Grid.SetColumn(marker, 0);
        itemGrid.Children.Add(marker);

        var content = new StackPanel { Orientation = Orientation.Vertical };
        context.AddMarkdownClass(content, MarkdownStyleKeys.ListItemContent);
        var firstParagraph = true;
        foreach (var block in item)
        {
            var child = block is ParagraphBlock paragraph
                ? context.CreateParagraph(paragraph, isTask && firstParagraph, GetListParagraphMargin(firstParagraph, context))
                : context.ConvertBlock(block, null);
            firstParagraph = false;

            if (child is not null)
            {
                content.Children.Add(child);
            }
        }

        Grid.SetColumn(content, 1);
        itemGrid.Children.Add(content);
        return itemGrid;
    }

    private static Control CreateTaskMarker(
        bool isChecked,
        double markerWidth,
        ListItemBlock item,
        IMarkdownRenderContext context)
    {
        var interactive = context.TaskMarkersInteractive;
        var checkBox = new CheckBox
        {
            IsChecked = isChecked,
            IsHitTestVisible = interactive,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        context.AddMarkdownClass(checkBox, MarkdownStyleKeys.TaskMarkerBox);
        if (interactive && item.Span.Start >= 0)
        {
            context.AttachTaskMarkerInteraction(checkBox, item.Span.Start);
        }

        // A task checkbox replaces the list bullet, as in GFM task lists.
        var marker = new Border
        {
            Child = checkBox,
            MinWidth = 15,
            VerticalAlignment = VerticalAlignment.Top
        };
        context.AddMarkdownClass(marker, MarkdownStyleKeys.ListMarker);
        return marker;
    }

    private static SelectableTextBlock CreateListMarker(string text, double markerWidth, IMarkdownRenderContext context)
    {
        var marker = context.CreateSelectableText(MarkdownStyleKeys.ListMarker);
        marker.Text = text;
        marker.FontWeight = FontWeight.Bold;
        marker.MinWidth = markerWidth;
        marker.TextAlignment = TextAlignment.Right;
        marker.VerticalAlignment = VerticalAlignment.Top;
        context.BindTheme(marker, SelectableTextBlock.ForegroundProperty, MarkdownViewer.AccentBrushProperty);
        context.BindTheme(marker, SelectableTextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        context.BindTheme(marker, SelectableTextBlock.FontSizeProperty, MarkdownViewer.ParagraphFontSizeProperty);
        context.BindTheme(marker, SelectableTextBlock.LineHeightProperty, MarkdownViewer.ParagraphLineHeightProperty);
        return marker;
    }

    private static int GetOrderedStart(ListBlock list)
    {
        return list.IsOrdered
               && int.TryParse(list.OrderedStart, out var start)
               && start > 0
            ? start
            : 1;
    }

    private static double CalculateListMarkerWidth(bool ordered, int startIndex, int itemCount, IMarkdownRenderContext context)
    {
        if (!ordered)
        {
            return context.UnorderedListMarkerWidth;
        }

        var lastMarkerLength = $"{Math.Max(startIndex, startIndex + itemCount - 1)}.".Length;
        return Math.Max(
            context.OrderedListMarkerMinWidth,
            lastMarkerLength * context.OrderedListMarkerCharacterWidth + context.OrderedListMarkerExtraWidth);
    }

    private static Thickness GetListParagraphMargin(bool firstParagraph, IMarkdownRenderContext context)
    {
        return firstParagraph ? context.ListFirstParagraphMargin : context.ListNestedParagraphMargin;
    }
}
