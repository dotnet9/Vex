using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

using global::Markdig;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 表格列对齐探针：渲染三列表格，dump 列定义、各单元格期望/实际边界与内容控件类型，
/// 定位列错位/内容丢失的层级（列宽计算 vs 单元格排布 vs 段控件量测）。报告写文件人工比对。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownTableProbeTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void Probe_TableColumnAlignment() => platform.Run(() =>
    {
        // 对照组：示例文档的 Grid Table 按字符停位（0/10/34/46）对齐后应解析出三列三行。
        var doc = "+---------+-----------------------+-----------+\n"
            + "| 实体      | 说明                    | 基数        |\n"
            + "+=========+=======================+===========+\n"
            + "| Vex     | 桌面文档工作台               | 1         |\n"
            + "+---------+-----------------------+-----------+\n"
            + "| Avalonia| 跨平台 UI 框架             | N         |\n"
            + "+---------+-----------------------+-----------+\n";
        var viewer = new MarkdownViewer { Markdown = doc, Width = 950 };
        // 复现 Vex 真实条件：表格工具条工厂（叠加结构）+ FillAvailableWidth（宿主样式注入）。
        viewer.TableToolsFactory = (_, _) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { new TextBlock { Text = "复制 Markdown" } },
        };
        var window = new Window { Width = 1000, Height = 500, Content = viewer };
        window.Show();
        try
        {
            for (var pass = 0; pass < 30; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var dump = new System.Text.StringBuilder();
            var block = viewer.GetVisualDescendants().OfType<MarkdownTableBlock>().FirstOrDefault();
            dump.AppendLine($"tableBlock={block is not null}");
            var container = viewer.GetVisualDescendants().FirstOrDefault(c => c.Classes.Contains("MdTableContainer"));
            var grid = viewer.GetVisualDescendants().OfType<MarkdownTableGrid>().FirstOrDefault();
            if (grid is not null)
            {
                // 宿主（Vex）通过样式给 Grid.MdTable 注入 FillAvailableWidth=True；headless 无该样式，
                // 这里手工补上并强制重排，模拟真实宿主条件后再 dump。
                MarkdownTableLayout.SetFillAvailableWidth(grid, true);
                for (var pass = 0; pass < 10; pass++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }
                dump.AppendLine($"grid.Bounds={grid.Bounds} fill={MarkdownTableLayout.GetFillAvailableWidth(grid)}");
                dump.AppendLine("columns=[" + string.Join(", ", grid.ColumnDefinitions.Select(c => c.Width.Value.ToString("0"))) + "]");
                foreach (var cell in grid.Children.OfType<Border>())
                {
                    var row = Grid.GetRow(cell);
                    var col = Grid.GetColumn(cell);
                    var content = cell.Child is StackPanel stack && stack.Children.Count > 0 ? stack.Children[0] : cell.Child;
                    var contentX = content is Visual v && cell.Child is Visual _ ? 0 : 0;
                    dump.AppendLine($"cell[r{row}c{col}] desired={cell.DesiredSize.Width.ToString("0")}x{cell.DesiredSize.Height.ToString("0")} "
                        + $"arranged={cell.Bounds.X.ToString("0")},{cell.Bounds.Y.ToString("0")} {cell.Bounds.Width.ToString("0")}x{cell.Bounds.Height.ToString("0")} "
                        + $"content={content?.GetType().Name} contentText={Describe(content)}");
                }
            }

            if (block is not null)
            {
                dump.AppendLine($"block.Bounds={block.Bounds} children=[{string.Join(", ", block.Children.Select(c => c.GetType().Name))}]");
                foreach (var child in block.Children)
                {
                    dump.AppendLine($"  child {child.GetType().Name} visible={child.IsVisible} bounds={child.Bounds}");
                }
            }

            var reportPath = System.IO.Path.Combine(ResolveRepositoryPath(
                System.IO.Path.Combine("artifacts", "table-probe-report.txt")));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath)!);
            System.IO.File.WriteAllText(reportPath, dump.ToString());
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    private static string Describe(Control? content) => content switch
    {
        SelectableTextBlock stb => "STB[" + (stb.Text ?? "") + "]",
        MarkdownTextSegmentView seg => "SEG[text=" + seg.PlainText + "]",
        _ => content?.ToString() ?? "null",
    };

    [Fact]
    public void Probe_MarkdigGridTableStructure() => platform.Run(() =>
    {
        // 直查 Markdig 的 GridTable 切列：ASCII 与 CJK 两种内容分别 dump 每行单元格数。
        var ascii = "+---------+-----------+\n| A       | B         |\n+=========+===========+\n| 1       | 2         |\n+---------+-----------+\n";
        var cjk = "+---------+-----------------------+-----------+\n"
            + "| 实体      | 说明                    | 基数        |\n"
            + "+=========+=======================+===========+\n"
            + "| Vex     | 桌面文档工作台               | 1         |\n"
            + "+---------+-----------------------+-----------+\n";
        var dump = new System.Text.StringBuilder();
        foreach (var (name, source) in new[] { ("ascii", ascii), ("cjk", cjk) })
        {
            var pipeline = new global::Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var parsed = global::Markdig.Markdown.Parse(source, pipeline);
            foreach (var block in parsed.OfType<Markdig.Extensions.Tables.Table>())
            {
                dump.AppendLine($"[{name}] table");
                foreach (var row in block.OfType<Markdig.Extensions.Tables.TableRow>())
                {
                    var cells = row.OfType<Markdig.Extensions.Tables.TableCell>().ToList();
                    var texts = cells.Select(c => string.Join("", c.Select(b =>
                        CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownParser.ExtractPlainText(b).TrimEnd())));
                    dump.AppendLine($"  row header={row.IsHeader} cells={cells.Count} [{string.Join("][", texts)}]");
                }
            }
        }

        System.IO.File.WriteAllText(ProbeDumpPath("gridtable-parse-probe.txt"), dump.ToString());
    });

    /// <summary>诊断转储落仓内 gitignored artifacts——不绑机器私有盘符（旧实现写死 E:\，无该盘的机器必失败）。</summary>
    private static string ProbeDumpPath(string fileName,
        [System.Runtime.CompilerServices.CallerFilePath] string testFile = "")
    {
        var artifacts = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(System.IO.Path.GetDirectoryName(testFile)!, "..", "..", "artifacts"));
        System.IO.Directory.CreateDirectory(artifacts);
        return System.IO.Path.Combine(artifacts, fileName);
    }

    [Fact]
    public void SegmentParagraph_SelectionOffsets_ArePrecise() => platform.Run(() =>
    {
        // 多行段控件（前有垫高段，origin.Y 显著大于 0）：跨行拖选，断言偏移精确——
        // 曾因控制器把文档坐标直传段布局命中（缺文档→本地转换），偏移被错位一个块起点。
        var filler = string.Join("", Enumerable.Range(1, 30).Select(i => $"甲{i:00}乙"));
        var doc = "第一段落文本内容。\n\n" + filler + "\n\n第三段落文本内容。\n";
        var viewer = new MarkdownViewer { Markdown = doc, Width = 500 };
        var window = new Window { Width = 600, Height = 400, Content = viewer };
        window.Show();
        try
        {
            for (var pass = 0; pass < 30; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var target = viewer.GetVisualDescendants().OfType<MarkdownTextSegmentView>()
                .First(seg => seg.PlainText.Length > 50);
            var host = (Visual)typeof(MarkdownViewer).GetField("_documentHost",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(viewer)!;
            var controller = (CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownSelectionController)typeof(MarkdownViewer)
                .GetField("_selectionController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(viewer)!;

            var origin = target.TranslatePoint(new Point(0, 0), host)!.Value;
            Assert.True(controller.Begin(new Point(origin.X + 2, origin.Y + 10)));
            Assert.True(controller.Extend(new Point(origin.X + 60, origin.Y + target.Bounds.Height - 10)));

            var selected = controller.SelectedText;
            // 起点 X=2px 落在第 0 字符（未错位到后续行/块尾），终点在段内未钳到整段末尾。
            Assert.StartsWith(filler[..6], selected, StringComparison.Ordinal);
            Assert.True(selected.Length < filler.Length - 2, $"选区不应钳到段尾：{selected.Length}/{filler.Length}");
            Assert.True(selected.Length > 10);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void TableCrossCellSelection_JoinsByRowAndLine() => platform.Run(() =>
    {
        // 网页式表格拖选：同行单元格 Tab 拼接、跨行换行拼接；单元格子块按（行,列）命中。
        var doc = "| 实体 | 说明 | 基数 |\n| --- | --- | --- |\n| Vex | 桌面文档工作台 | 1 |\n| Avalonia | 跨平台 UI 框架 | N |\n";
        var viewer = new MarkdownViewer { Markdown = doc, Width = 950 };
        viewer.TableToolsFactory = (_, _) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { new TextBlock { Text = "复制 Markdown" } },
        };
        var window = new Window { Width = 1000, Height = 500, Content = viewer };
        window.Show();
        try
        {
            for (var pass = 0; pass < 30; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var host = (Visual)typeof(MarkdownViewer).GetField("_documentHost",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(viewer)!;
            var controller = (CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownSelectionController)typeof(MarkdownViewer)
                .GetField("_selectionController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(viewer)!;

            var grid = viewer.GetVisualDescendants().OfType<MarkdownTableGrid>().First();
            var cells = grid.Children.OfType<Border>()
                .OrderBy(Grid.GetRow).ThenBy(Grid.GetColumn).ToList();
            Assert.Equal(9, cells.Count);

            Point Center(Control c, double dx) => c.TranslatePoint(new Point(0, 0), host)!.Value
                + new Vector(Math.Max(2, dx), c.Bounds.Height / 2);

            // 同行：r1c0 左缘 → r1c2 中部 → 整行 Tab 拼接。
            Assert.True(controller.Begin(Center(cells[3], 1)));
            Assert.True(controller.Extend(Center(cells[5], 30)));
            Assert.Equal("Vex\t桌面文档工作台\t1", controller.SelectedText);

            // 跨行：r1c0 → r2c1 中部 → 跨行换行拼接，末格部分文本（20px 处 ≈ 首字"跨"）。
            Assert.True(controller.Begin(Center(cells[3], 1)));
            Assert.True(controller.Extend(Center(cells[7], 20)));
            Assert.StartsWith("Vex\t桌面文档工作台\t1\nAvalonia\t跨", controller.SelectedText, StringComparison.Ordinal);

            // 表头：r0c0 → r0c1（40px ≈ "说明"两字内）→ 表头两格 Tab 拼接。
            Assert.True(controller.Begin(Center(cells[0], 1)));
            Assert.True(controller.Extend(Center(cells[1], 40)));
            Assert.StartsWith("实体\t说明", controller.SelectedText, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    /// <summary>仓库内路径随测试源码定位解析，不绑定具体盘符/检出位置。</summary>
    private static string ResolveRepositoryPath(string relativePath,
        [System.Runtime.CompilerServices.CallerFilePath] string testFile = "") =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(testFile)!, "..", "..", relativePath));
}
