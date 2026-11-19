using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 表格渲染器：按内容分配列宽，含表头底色与行/列分隔线。
/// </summary>
internal sealed class TableRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not Table table)
        {
            return null;
        }

        var rows = table.OfType<TableRow>().ToList();
        var columnCount = rows.Select(row => row.Count).DefaultIfEmpty(0).Max();
        var grid = new MarkdownTableGrid();
        context.AddMarkdownClass(grid, MarkdownStyleKeys.Table);

        for (var i = 0; i < columnCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        var bodyRow = 0;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var row = rows[rowIndex];
            var alternate = !row.IsHeader && bodyRow++ % 2 == 1;
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (row[columnIndex] is not TableCell cell)
                {
                    continue;
                }

                var cellBorder = CreateTableCell(cell, row.IsHeader, context);
                if (alternate) cellBorder.Classes.Add("MdTableAlternateCell");
                Grid.SetRow(cellBorder, rowIndex);
                Grid.SetColumn(cellBorder, columnIndex);
                grid.Children.Add(cellBorder);
            }
        }

        var container = new Border
        {
            Child = grid,
            ClipToBounds = true
        };
        container.SetValue(Control.HorizontalAlignmentProperty, HorizontalAlignment.Left, global::Avalonia.Data.BindingPriority.Style);
        context.AddMarkdownClass(container, MarkdownStyleKeys.TableContainer);
        context.BindTheme(container, Border.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty, global::Avalonia.Data.BindingPriority.Style);
        var data = new MarkdownTableData(rows.Select(row => row.OfType<TableCell>()
            .Select(cell => string.Join("\n", cell.Select(child => MarkdownParser.ExtractPlainText(child).TrimEnd())))),
            rows.FirstOrDefault()?.IsHeader == true);
        return context.DecorateTable(container, data);
    }

    private static Border CreateTableCell(TableCell cell, bool isHeader, IMarkdownRenderContext context)
    {
        var border = new Border();
        border.Bind(Border.BorderThicknessProperty, border.GetResourceObservable("CodeWFMarkdownTableCellBorderThickness"), global::Avalonia.Data.BindingPriority.Style);
        context.AddMarkdownClass(border, isHeader ? MarkdownStyleKeys.TableHeaderCell : MarkdownStyleKeys.TableCell);
        context.BindTheme(border, Border.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty, global::Avalonia.Data.BindingPriority.Style);
        if (isHeader)
        {
            context.BindTheme(border, Border.BackgroundProperty, MarkdownViewer.TableHeaderBackgroundBrushProperty);
        }

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        context.AddMarkdownClass(stack, MarkdownStyleKeys.TableCellContent);
        foreach (var block in cell)
        {
            var child = CreateTableCellBlock(block, isHeader, context);
            if (child is not null)
            {
                stack.Children.Add(child);
            }
        }

        border.Child = stack;
        return border;
    }

    private static Control? CreateTableCellBlock(Block block, bool isHeader, IMarkdownRenderContext context)
    {
        // 图片段落也必须经过块管线，才能正常挂载模板并按单元格宽度量测。
        var child = context.ConvertBlock(block, null);

        if (child is SelectableTextBlock textBlock)
        {
            if (block is ParagraphBlock) textBlock.Margin = new Thickness(0);
            // 单元格正文字号与段落同源：直接跟随 MarkdownViewer 的正文排版尺寸/行高，
            // 主题与排版档位（TypographyTheme/TypographySize）变化时与正文一致缩放；
            // 表头仅保留加粗与底色差异，不再整体缩小。HTML 显式设置的行内字号在行内渲染层落本地值，不受此处影响。
            context.BindTheme(textBlock, SelectableTextBlock.FontSizeProperty, MarkdownViewer.ParagraphFontSizeProperty);
            context.BindTheme(textBlock, SelectableTextBlock.LineHeightProperty, MarkdownViewer.ParagraphLineHeightProperty);
            if (isHeader)
            {
                textBlock.FontWeight = FontWeight.SemiBold;
            }
        }

        return child;
    }
}
