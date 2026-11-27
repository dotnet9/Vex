using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

using CodeWF.Avalonia.Markdown.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// AOT 门禁 reading-follow 失败的 JIT 复现探针：50 章文档（RuntimeVerification 同款构造），
/// 逐章校验 TryGetSourceLineBounds 的行→块映射（单调递增 + 比例合理），钉死映射错位。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerLineMapTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void SourceLineBounds_PerChapter_MonotonicAndProportional() => platform.Run(() =>
    {
        var source = string.Join("\n", Enumerable.Range(1, 50).Select(index =>
            $"# {(index is 35 or 40 ? "同名章节" : "章节 " + index)}\n\n" +
            string.Concat(Enumerable.Range(1, 8).Select(paragraph => $"正文 {index}.{paragraph}。阅读位置跟随。\n\n"))));
        var viewer = new MarkdownViewer { Markdown = source, Width = 1000 };
        var window = new Window { Width = 1100, Height = 820, Content = viewer };
        window.Show();
        try
        {
            for (var pass = 0; pass < 30; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            // 每章标题行：i-1 章 × 18 行（标题 1 + 空行 1 + 8 段 × (段 1 + 空行 1)）+ 1。
            var dump = new System.Text.StringBuilder();
            var previousTop = -1.0;
            var monotonic = true;
            for (var chapter = 1; chapter <= 50; chapter++)
            {
                var line = (chapter - 1) * 18 + 1;
                var ok = viewer.TryGetSourceLineBounds(line, out var bounds);
                dump.AppendLine($"ch{chapter:00} line={line:000} ok={ok} top={(ok ? bounds.Top.ToString("0") : "-")}");
                if (!ok)
                {
                    continue;
                }

                if (previousTop >= 0 && bounds.Top <= previousTop)
                {
                    monotonic = false;
                }

                previousTop = ok ? bounds.Top : previousTop;
            }

            var mid = viewer.TryGetSourceLineBounds(11 * 18 - 17, out var midBounds);
            var total = viewer.TryGetSourceLineBounds(50 * 18 - 17, out var endBounds);
            var report = $"monotonic={monotonic} ch11={midBounds.Top:0} ch50={endBounds.Top:0} viewerH={viewer.Bounds.Height:0}\n{dump}";
            System.IO.File.WriteAllText(@"E:\github\temp_files\vex-build\line-map-probe.txt", report);
            Assert.True(monotonic, $"行→块映射应单调递增：\n{report}");
            if (mid && total)
            {
                var ratio = midBounds.Top / endBounds.Top;
                Assert.True(ratio is > 0.12 and < 0.32, $"第 11 章位置比例应约 0.2，实际 {ratio:0.00}");
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });
}
