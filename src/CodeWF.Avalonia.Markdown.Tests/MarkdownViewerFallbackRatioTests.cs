using CodeWF.Avalonia.Markdown.Controls;

using global::Avalonia.Controls;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 预览滚动块级兜底（<see cref="MarkdownViewer.GetSourceLineFallbackRatio"/>）：
/// 目标块未物化时，滚动比例按「块索引 + 块内字符进度 ÷ 总块数」估计，替代旧行数比例
/// ——块高差异大时行数比例偏差可达数屏。已物化块仍走 TryGetSourceLineBounds 精确落点。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerFallbackRatioTests(AvaloniaPlatformFixture platform)
{
    private const string Document =
        "# 标题\n" +
        "\n" +
        "短段落。\n" +
        "\n" +
        "- 甲\n- 乙\n- 丙\n" +
        "\n" +
        "```csharp\n" +
        "var a = 1;\nvar b = 2;\nvar c = 3;\n" +
        "```\n" +
        "\n" +
        "收尾段落。\n";

    [Fact]
    public void FallbackRatio_IsBlockGrainedAndMonotonic()
    {
        platform.Run(() =>
        {
            var viewer = new MarkdownViewer();
            var window = new Window { Width = 600, Height = 400, Content = viewer };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                viewer.Markdown = Document;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.True(viewer.CurrentModel.Blocks.Count >= 5, "文档应解析出多个块");

                var totalLines = Document.Count('\n') + 1;
                double previous = -1;
                for (var line = 1; line <= totalLines; line++)
                {
                    var ratio = viewer.GetSourceLineFallbackRatio(line);
                    Assert.InRange(ratio, 0d, 1d);
                    Assert.True(ratio >= previous - 1e-9, $"比例应随行号单调不减：行 {line}");
                    previous = ratio;
                }

                Assert.Equal(0d, viewer.GetSourceLineFallbackRatio(1), 9);
                Assert.True(viewer.GetSourceLineFallbackRatio(totalLines) > 0.9,
                    "末行应落在最后一个块（接近 1）");
                // 行内字符进度有区分度：标题行与标题行后的短段落行不同块，比例应有差。
                Assert.True(viewer.GetSourceLineFallbackRatio(1) < viewer.GetSourceLineFallbackRatio(3),
                    "第一块之后应严格前进");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
