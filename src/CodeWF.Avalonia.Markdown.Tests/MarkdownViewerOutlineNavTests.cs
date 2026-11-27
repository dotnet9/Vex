using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 虚拟化阅读的大纲导航落点回归（AOT 门禁 RuntimeVerification.VerifyOutline 的 JIT 复现）：
/// 50 章长文（≈500 块）虚拟化开启，导航到第 40 章后落点必须收敛到视口顶 14px±2。
/// 曾因逐类估高污染未物化块的前缀和，滚动目标按估位计算、物化修正后无人重算，落点永不成立。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerOutlineNavTests(AvaloniaPlatformFixture platform)
{
    private static string MakeDocument()
    {
        var source = string.Join("\n", Enumerable.Range(1, 50).Select(index =>
            $"# 章节 {index}\n\n" +
            string.Concat(Enumerable.Range(1, 8).Select(paragraph => $"正文 {index}.{paragraph}。阅读位置跟随。\n\n"))));
        return source;
    }

    [Fact]
    public void OutlineNavigate_LandsWithinTolerance_UnderVirtualization() => platform.Run(() =>
    {
        var viewer = new MarkdownViewer
        {
            EnableVirtualization = true,
            VirtualizationThreshold = 1,
        };
        // headless ScrollViewer 必须自带模板注册 presenter，否则 Extent 恒 0。
        var presenter = new ScrollContentPresenter { Content = viewer };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            }),
        };
        var window = new Window { Width = 1000, Height = 700, Content = scroll };
        window.Show();
        try
        {
            var source = MakeDocument();
            viewer.Markdown = source;

            // 等首屏与若干分帧建块完成。
            var settleDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < settleDeadline)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Thread.Sleep(10);
            }

            var skeletonEngaged = false;

            // 模拟产品导航定时器节奏：50ms/跳（<150ms 骨架判据），验证收敛不被快拖骨架劫持。
            var targetLine = 39 * 18 + 1;
            var deadline = Environment.TickCount64 + 8000;
            while (Environment.TickCount64 < deadline)
            {
                skeletonEngaged |= IsFastDragging(viewer);
                if (!viewer.TryGetSourceLineBounds(targetLine, out var bounds)) continue;
                var target = viewer.TranslatePoint(new Point(0, bounds.Top), scroll)!.Value;
                var limit = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                var offset = Math.Clamp(scroll.Offset.Y + target.Y - 14, 0, limit);
                if (Math.Abs(offset - scroll.Offset.Y) < 1 && Math.Abs(target.Y - 14) < 2)
                {
                    break;
                }

                scroll.Offset = new Vector(scroll.Offset.X, offset);

                var tick = Environment.TickCount64 + 50;
                while (Environment.TickCount64 < tick)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Thread.Sleep(5);
                }
            }

            Assert.True(viewer.TryGetSourceLineBounds(targetLine, out var finalBounds), "收敛后目标行仍应有边界");
            var finalTop = viewer.TranslatePoint(new Point(0, finalBounds.Top), scroll)!.Value.Y;
            Assert.True(Math.Abs(finalTop - 14) < 2,
                $"落点应收敛到 14px±2，实际 {finalTop:0.0}（extent={scroll.Extent.Height:0} offset={scroll.Offset.Y:0} skeleton={skeletonEngaged} fastDragging={IsFastDragging(viewer)}）");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    private static bool IsFastDragging(MarkdownViewer viewer)
    {
        var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().FirstOrDefault();
        return panel is not null && panel.IsFastDragging;
    }
}
