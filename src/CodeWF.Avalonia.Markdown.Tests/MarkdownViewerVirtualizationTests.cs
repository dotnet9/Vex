using CodeWF.Avalonia.Markdown.Controls;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 阅读视图虚拟化（EnableVirtualization=true；应用偏好 ReadingVirtualization 当前默认关，
/// 大文档按需开启）的功能不变量：远端重块（表格/代码/图表）延迟创建、只物化视口附近
/// 条目，且与视口相交的每一块都已物化、内容就绪——滚动不得露出未建块的空白。
/// 配置开关关闭（全量布局）的行为由 WindowedStackPanelViewportCoverageTests 钉住。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerVirtualizationTests(AvaloniaPlatformFixture platform)
{
    private static readonly string Document = """
        # 虚拟化回归

        """ + string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"第一段落 {i}，足够多的正文让文档产生可观的滚动高度。")) + """

        ```csharp
        public static void Heavy() { Console.WriteLine("code"); }
        ```

        | 列一 | 列二 | 列三 |
        | --- | --- | --- |
        | A | B | C |
        | D | E | F |

        """ + string.Join("\n\n", Enumerable.Range(41, 80).Select(i => $"第二段落 {i}，继续填充滚动高度，让远端块在首屏之外。"));

    [Fact]
    public void VirtualizedViewerDefersHeavyBlocksAndKeepsViewportRealized() => platform.Run(() =>
    {
        var viewer = new MarkdownViewer { EnableVirtualization = true, Markdown = Document };
        var presenter = new ScrollContentPresenter { Content = viewer };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 640, Height = 360, Content = scroll };
        window.Show();
        try
        {
            for (var pass = 0; pass < 24; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            Assert.True(panel.IsVirtualizing);
            Assert.True(panel.ItemCount > 100, $"document should parse into many blocks, got {panel.ItemCount}");
            Assert.True(panel.RealizedItemCount < panel.ItemCount,
                $"virtualization must keep distant blocks unrealized: {panel.RealizedItemCount}/{panel.ItemCount}");
            AssertGaps(viewer, scroll, window, "initial viewport");

            // 滚到中段（重块附近）：视口覆盖保持；延迟块量出真实高度后 extent 允许调整（估高收敛）。
            var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            scroll.Offset = new Vector(0, maximum / 2);
            Pump(24, window);
            AssertGaps(viewer, scroll, window, "middle viewport");
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "document stays scrollable across virtualized scrolling");

            // 滚到末端再回顶部：三次位置都要全覆盖。
            scroll.Offset = new Vector(0, maximum);
            Pump(24, window);
            AssertGaps(viewer, scroll, window, "end viewport");
            scroll.Offset = default;
            Pump(24, window);
            AssertGaps(viewer, scroll, window, "returned-to-top viewport");
            Assert.True(panel.RealizedItemCount < panel.ItemCount,
                $"returning to top releases distant blocks again: {panel.RealizedItemCount}/{panel.ItemCount}");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void RapidLargeJumpsKeepRealizedSetWindowedAndCovered() => platform.Run(() =>
    {
        // 快拖骨架残骸回归钉：残骸曾把冻结期的虚拟化判定算成 false，窗口判定退化为
        // 全文档——任何一次外部触发的量测（锚点 Pin/Unpin 等）就会同步物化整篇巨文档，
        // 表现为拖动滚动条快拖直接卡死。连续大步（远超 2 视口高）往返后，物化集合必须
        // 始终保持窗口化，且落点视口全覆盖。
        var viewer = new MarkdownViewer { EnableVirtualization = true, Markdown = Document };
        var presenter = new ScrollContentPresenter { Content = viewer };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 640, Height = 360, Content = scroll };
        window.Show();
        try
        {
            Pump(24, window);
            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            foreach (var offset in new[] { maximum * .9, maximum * .2, maximum * .8, 0d, maximum, maximum * .5, 0d })
            {
                scroll.Offset = new Vector(0, offset);
                Pump(6, window);
                Assert.True(panel.IsVirtualizing, "rapid large jumps must not disable virtualization");
                Assert.True(panel.RealizedItemCount < panel.ItemCount,
                    $"large jumps keep the realized set windowed: {panel.RealizedItemCount}/{panel.ItemCount} at {offset:0}");
            }

            AssertGaps(viewer, scroll, window, "after rapid large jumps");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    private static void Pump(int passes, Window window)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private static void AssertGaps(MarkdownViewer viewer, ScrollViewer scroll, Window window, string stage)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            scroll.UpdateLayout();
        }

        var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
        var viewportTop = scroll.Offset.Y;
        var viewportBottom = viewportTop + scroll.Viewport.Height;
        List<int> unrealized = [], empty = [];
        for (var index = 0; index < panel.ItemCount; index++)
        {
            if (!panel.TryGetItemTop(index, out var top)) continue;
            if (top + panel.GetItemHeight(index) <= viewportTop || top >= viewportBottom) continue;
            var realized = panel.GetRealizedItem(index);
            if (realized is null) unrealized.Add(index);
            else if (realized is Decorator { Child: null }) empty.Add(index);
        }

        Assert.True(unrealized.Count == 0,
            $"{stage} [{viewportTop:0},{viewportBottom:0}] has unrealized bands: {string.Join(',', unrealized)}");
        Assert.True(empty.Count == 0,
            $"{stage} has realized deferred blocks without content: {string.Join(",", empty)}");
    }
}
