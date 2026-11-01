using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// 阅读视图表格工具（对应原型 .md-table-toolbar）：表格上方常驻一行
/// 右对齐的纯文字动作（复制 Markdown / 下载 CSV / 预览表格）。数据从渲染后的表格提取。
/// </summary>
public static class TableTools
{
    /// <summary>给阅读视图的每张表格上方插入常驻工具条；重复调用安全。</summary>
    public static void Attach(Control root)
    {
        // 最后一张表格移除时也要清理工具条，不能等到下一张表格出现才清理。
        foreach (var orphan in root.GetVisualDescendants().OfType<Control>()
                     .Where(child => child.GetValue(ToolbarForProperty) is Border owner
                         && !ReferenceEquals(owner.Parent, child.Parent)).ToList())
            if (orphan.Parent is Panel parent) parent.Children.Remove(orphan);
        foreach (var container in root.GetVisualDescendants().OfType<Border>()
                     .Where(border => border.Classes.Contains(CodeWF.Avalonia.Markdown.MarkdownStyleKeys.TableContainer))
                     .ToList())
        {
            if (container.Parent is not Panel panel) continue;
            if (container.GetValue(AttachedProperty)) continue;
            container.SetValue(AttachedProperty, true);
            // 原型 .md-table-block 中工具条与表格同组：块顶距归工具条，表格只留底距。
            var toolbar = BuildToolbar(container);
            toolbar.Margin = new Thickness(0, container.Margin.Top, 0, 4);
            container.Margin = new Thickness(0, 0, 0, container.Margin.Bottom);
            // 工具条只挂在表格前面，不占文档条目位：MarkdownViewer 的渲染块表与面板条目是按
            // 下标对齐的，登记成条目会让其后每一块错位一位，按块号增量更新就落到别的块上。
            // 虚拟化面板会把这类未登记子项算作紧随条目的附加高度参与排位（见 WindowedStackPanel）。
            toolbar.SetValue(ToolbarForProperty, container);
            panel.Children.Insert(panel.Children.IndexOf(container), toolbar);
        }
    }

    /// <summary>表格工具条上的文字动作：内容绑资源键，语言切换即时跟着换。</summary>
    private static Button TableTool(string labelKey)
    {
        var button = new Button { Classes = { "md-table-tool" } };
        if (new CodeWF.Avalonia.Lang.MarkupExtensions.I18nBinding(labelKey).ProvideValue(null!) is Avalonia.Data.BindingBase binding)
            button.Bind(Button.ContentProperty, binding);
        return button;
    }

    private static readonly AttachedProperty<bool> AttachedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("TableToolsAttached", typeof(TableTools));

    private static readonly AttachedProperty<Border?> ToolbarForProperty =
        AvaloniaProperty.RegisterAttached<Control, Border?>("TableToolsToolbarFor", typeof(TableTools));

    private static Control BuildToolbar(Border container)
    {
        // 工具条是文档渲染时现建的，切语言不会重排正文，所以三个动作钮的文字用资源绑定挂在控件上，
        // 跟着 I18nManager 的文化走（与 TabControlRegionAdapter 的页签标题同一口径）。
        var copy = TableTool(VexL.TableCopyMarkdown);
        var csv = TableTool(VexL.TableDownloadCsv);
        var preview = TableTool(VexL.TablePreview);
        copy.Click += async (_, _) =>
        {
            var markdown = ToMarkdown(Extract(container));
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
                await clipboard.SetTextAsync(markdown);
        };
        csv.Click += async (_, _) =>
        {
            var csvText = ToCsv(Extract(container));
            if (TopLevel.GetTopLevel(container) is { } top)
            {
                var file = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    SuggestedFileName = "table.csv",
                    FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
                });
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8);
                    await writer.WriteAsync(csvText);
                }
            }
        };
        preview.Click += (_, _) => OpenPreview(container);
        // 原型：flex justify-end、间距 2px 的一行裸按钮，无边框底衬。
        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { copy, csv, preview },
        };
    }

    /// <summary>按表格的行列索引提取文本，不依赖尚未完成布局时的控件坐标。</summary>
    public static string[][] Extract(Border container)
    {
        var grid = container.GetVisualDescendants().OfType<Grid>()
            .FirstOrDefault(candidate => candidate.Classes.Contains(CodeWF.Avalonia.Markdown.MarkdownStyleKeys.Table));
        if (grid is null) return Array.Empty<string[]>();
        return grid.Children.OfType<Border>()
            .Where(cell => cell.Classes.Contains(CodeWF.Avalonia.Markdown.MarkdownStyleKeys.TableCell)
                || cell.Classes.Contains(CodeWF.Avalonia.Markdown.MarkdownStyleKeys.TableHeaderCell))
            .GroupBy(Grid.GetRow).OrderBy(row => row.Key)
            .Select(row => row.OrderBy(Grid.GetColumn)
                .Select(cell => string.Concat(cell.GetVisualDescendants().Select(CellText)).Replace("\n", " ").Trim())
                .ToArray()).ToArray();
    }

    /// <summary>单元格文本：Markdown 文本块的文字放在 Inlines 里，Text 属性可能为空。</summary>
    public static string CellText(Avalonia.Visual control)
    {
        return control switch
        {
            Avalonia.Controls.SelectableTextBlock selectable => InlineText(selectable.Inlines),
            TextBlock block => InlineText(block.Inlines),
            _ => string.Empty,
        };
        static string InlineText(global::Avalonia.Controls.Documents.InlineCollection? inlines)
        {
            if (inlines is null) return string.Empty;
            var builder = new System.Text.StringBuilder();
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case global::Avalonia.Controls.Documents.Run run: builder.Append(run.Text); break;
                    case global::Avalonia.Controls.Documents.Span span: builder.Append(InlineText(span.Inlines)); break;
                }
            }
            return builder.ToString();
        }
    }

    public static string ToMarkdown(string[][] rows)
    {
        if (rows.Length == 0) return string.Empty;
        var width = rows.Max(row => row.Length);
        var padded = rows.Select(row => Enumerable.Range(0, width).Select(i => i < row.Length ? row[i] : "").ToArray()).ToArray();
        var lines = new List<string>
        {
            "| " + string.Join(" | ", padded[0]) + " |",
            "| " + string.Join(" | ", padded[0].Select(_ => "---")) + " |",
        };
        for (var i = 1; i < padded.Length; i++) lines.Add("| " + string.Join(" | ", padded[i]) + " |");
        return string.Join("\n", lines);
    }

    public static string ToCsv(string[][] rows)
    {
        return string.Join("\n", rows.Select(row => string.Join(",",
            row.Select(cell => cell.Contains(',') || cell.Contains('"') || cell.Contains('\n')
                ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell))));
    }

    private static void OpenPreview(Border container)
    {
        _ = TablePreviewView.Show(Extract(container), container);
    }

}
