using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CodeWF.Avalonia.Markdown.Rendering;
using Avalonia.Media;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// 阅读视图表格工具（对应原型 .md-table-toolbar）：表格上方常驻一行
/// 右对齐的纯文字动作（复制 Markdown / 下载 CSV / 预览表格）。数据由库的表格模型提供。
/// </summary>
public static class TableTools
{
    /// <summary>表格工具条上的文字动作：内容绑资源键，语言切换即时跟着换。</summary>
    private static Button TableTool(string labelKey)
    {
        var button = new Button { Classes = { "md-table-tool" } };
        button.ContentTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) =>
            new TextBlock { Text = text, LineHeight = 14.4 });
        if (new CodeWF.Avalonia.Lang.MarkupExtensions.I18nBinding(labelKey).ProvideValue(null!) is Avalonia.Data.BindingBase binding)
            button.Bind(Button.ContentProperty, binding);
        return button;
    }

    public static Control Create(MarkdownTableData data)
    {
        // 工具条是文档渲染时现建的，切语言不会重排正文，所以三个动作钮的文字用资源绑定挂在控件上，
        // 跟着 I18nManager 的文化走（与 TabControlRegionAdapter 的页签标题同一口径）。
        var copy = TableTool(VexL.TableCopyMarkdown);
        var csv = TableTool(VexL.TableDownloadCsv);
        var preview = TableTool(VexL.TablePreview);
        copy.Click += async (_, _) =>
        {
            var markdown = ToMarkdown(data.Rows, data.HasHeader);
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
                await clipboard.SetTextAsync(markdown);
        };
        csv.Click += async (_, _) =>
        {
            var csvText = ToCsv(data.Rows);
            if (TopLevel.GetTopLevel(csv) is { } top)
            {
                var file = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    SuggestedFileName = "table.csv",
                    FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
                });
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    stream.SetLength(0);
                    await using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8);
                    await writer.WriteAsync(csvText);
                }
            }
        };
        preview.Click += (_, _) => _ = TablePreviewView.Show(data.Rows, preview, data.HasHeader);
        // 原型：flex justify-end、间距 2px 的一行裸按钮，无边框底衬。
        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { copy, csv, preview },
        };
    }

    public static string ToMarkdown(IReadOnlyList<IReadOnlyList<string>> rows, bool hasHeader = true)
    {
        if (rows.Count == 0) return string.Empty;
        var width = rows.Max(row => row.Count);
        var padded = rows.Select(row => Enumerable.Range(0, width).Select(i => i < row.Count ? EscapeCell(row[i]) : "").ToArray()).ToArray();
        var lines = new List<string>
        {
            "| " + string.Join(" | ", (hasHeader ? padded[0] : Enumerable.Repeat("", width))) + " |",
            "| " + string.Join(" | ", padded[0].Select(_ => "---")) + " |",
        };
        for (var i = hasHeader ? 1 : 0; i < padded.Length; i++) lines.Add("| " + string.Join(" | ", padded[i]) + " |");
        return string.Join("\n", lines);
    }

    private static string EscapeCell(string text) => text.Replace("\\", "\\\\").Replace("|", "\\|").ReplaceLineEndings("<br>");

    public static string ToCsv(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        return string.Join("\n", rows.Select(row => string.Join(",",
            row.Select(cell => cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') || cell.Contains('\r')
                ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell))));
    }

}
