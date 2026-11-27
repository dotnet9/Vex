using CodeWF.Avalonia.Markdown.Tests;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Headless;
using global::Avalonia.Threading;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 视口覆盖不变量：任何时刻，「与滚动视口相交的条目」必须已物化，否则阅读视图就是
/// 一段不随滚动消失的空白。量测会把上方块量高/量矮（延迟建块、图片、图表），真实位置
/// 随之整体平移，旧缓存尺寸算出的窗口判定会漏掉视口内的条目——这正是「正文下方大片
/// 空白、永远物化的代码块却画在更远处」的病根。量测后的窗口重放 + 补物化把这一类
/// 漂移闭环在单次量测内（ReadVerifyLayout 探针也在真机整窗上断言同一不变量）。
/// <para>
/// 此断言只跑 markdown 侧本仓副本；Controls 仓的互为副本同步该修复后方可并入共用 theory。
/// </para>
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class WindowedStackPanelViewportCoverageTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void JumpScrollKeepsEveryViewportBandRealizedWhileSizesAreStillConverging() => platform.Run(() =>
    {
        var panel = new WindowedStackPanel();
        // 首测量谎报 24、次测量才报 600：复刻延迟建块/图片加载把量测拖到物化之后。
        for (var i = 0; i < 120; i++)
        {
            panel.AddItem(new GrowingBlock(600));
        }

        var presenter = new ScrollContentPresenter { Content = panel };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 300, Height = 100, Content = scroll };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            // 跳滚到远端：视口落在只有估高（且估高远小于真实高度）的区域。
            var extent = scroll.Extent.Height;
            scroll.Offset = new Vector(0, Math.Min(9000, Math.Max(0, extent - scroll.Viewport.Height)));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            AssertViewportFullyRealized(panel, scroll);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void OneMeasureConvergesWhenMultipleBandsCollapseInSuccession() => platform.Run(() =>
    {
        var presenter = new ScrollContentPresenter { Content = new Border { Height = 50_000 } };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 300, Height = 100, Content = scroll };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var panel = new WindowedStackPanel
            {
                ScrollHost = scroll,
                EstimatedItemHeight = 42,
                OverscanViewports = 0
            };
            for (var i = 0; i < 120; i++)
            {
                panel.AddItem(new CollapsingBlock());
            }

            panel.Measure(new Size(300, double.PositiveInfinity));

            AssertViewportFullyRealized(panel, scroll);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void LongDocumentWithDeferredTablesKeepsViewportRealizedAfterJumpAndResize() => platform.Run(() =>
    {
        var blocks = Enumerable.Range(0, 220).Select(index => index % 20 == 0
            ? $"## Table {index}\n\n| 列一 | 列二 | 列三 |\n| --- | --- | --- |\n" +
              string.Join('\n', Enumerable.Range(0, 24).Select(row =>
                  $"| {index}-{row} | 较长单元格内容用于触发换行与重新量测 | value-{row} |"))
            : $"Paragraph {index} with enough text to keep the document virtualized and force deferred block creation.");
        var viewer = new MarkdownViewer
        {
            Markdown = string.Join("\n\n", blocks),
            EnableVirtualization = true
        };
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
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            Assert.True(panel.IsVirtualizing);
            Assert.True(panel.ItemCount >= 200);

            foreach (var ratio in new[] { 0d, .25d, .5d, .75d, 1d })
            {
                var maxOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                scroll.Offset = new Vector(0, maxOffset * ratio);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                // 快拖骨架冻结期（连续大跳 <150ms）内视口暂不物化——断言前等待退出骨架。
                Thread.Sleep(200);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                AssertViewportFullyRealized(panel, scroll);
                AssertViewportContentReady(panel, scroll);
            }

            window.Width = 420;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AssertViewportFullyRealized(panel, scroll);
            AssertViewportContentReady(panel, scroll);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void DefaultViewerKeepsEveryParagraphRealizedAcrossLongDocumentJumpAndResize() => platform.Run(() =>
    {
        // 默认全量布局语义（对齐 StreamMark）：块轻量化后全量量测，extent 永远精确，
        // 滚动条与内容严格同步；长文档跳转+缩放全程视口全覆盖。
        var blocks = Enumerable.Range(0, 450)
            .Select(index => $"Paragraph {index} is ordinary supported Markdown content.");
        var viewer = new MarkdownViewer { Markdown = string.Join("\n\n", blocks) };
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
            for (var pass = 0; pass < 20; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            Assert.False(viewer.EnableVirtualization);
            Assert.False(panel.IsVirtualizing);
            Assert.Equal(450, panel.ItemCount);
            Assert.Equal(panel.ItemCount, panel.RealizedItemCount);

            var paragraph = panel.GetRealizedItem(370) switch
            {
                MarkdownTextSegmentView segment => (Control)segment,
                SelectableTextBlock textBlock => textBlock,
                _ => throw new Xunit.Sdk.XunitException($"块 370 应为段控件或可选文本块，实际 {panel.GetRealizedItem(370)?.GetType().Name}")
            };
            Assert.Equal("Paragraph 370 is ordinary supported Markdown content.",
                paragraph is MarkdownTextSegmentView seg ? seg.PlainText
                    : ((SelectableTextBlock)paragraph).Inlines?.OfType<Run>().Single().Text);
            Assert.True(panel.TryGetItemTop(370, out var paragraphTop));
            scroll.Offset = new Vector(0, paragraphTop);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.True(paragraphTop >= scroll.Offset.Y);
            Assert.True(paragraphTop + paragraph.DesiredSize.Height <= scroll.Offset.Y + scroll.Viewport.Height);
            Assert.Equal(panel.ItemCount, panel.RealizedItemCount);

            window.Width = 420;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(panel.ItemCount, panel.RealizedItemCount);
            AssertViewportFullyRealized(panel, scroll);
            AssertViewportContentReady(panel, scroll);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void ChangingVirtualizationAfterRenderRebuildsBlocksWithoutLosingContent() => platform.Run(() =>
    {
        var blocks = Enumerable.Range(0, 120).Select(index => $"Paragraph {index}");
        var viewer = new MarkdownViewer { Markdown = string.Join("\n\n", blocks), EnableVirtualization = false };
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
            for (var pass = 0; pass < 20; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            Assert.False(viewer.EnableVirtualization); // 本测试从显式关闭起跑（验证的是切换行为，不是默认值）
            Assert.Equal(panel.ItemCount, panel.RealizedItemCount);

            viewer.EnableVirtualization = true;
            for (var pass = 0; pass < 20; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            Assert.True(panel.IsVirtualizing);
            Assert.True(panel.RealizedItemCount < panel.ItemCount);
            AssertViewportFullyRealized(panel, scroll);
            AssertViewportContentReady(panel, scroll);

            viewer.EnableVirtualization = false;
            for (var pass = 0; pass < 20; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            Assert.False(panel.IsVirtualizing);
            Assert.Equal(panel.ItemCount, panel.RealizedItemCount);
            switch (panel.GetRealizedItem(119))
            {
                case MarkdownTextSegmentView segment:
                    Assert.Equal("Paragraph 119", segment.PlainText);
                    break;
                case SelectableTextBlock textBlock:
                    Assert.Equal("Paragraph 119", textBlock.Inlines?.OfType<Run>().Single().Text);
                    break;
                default:
                    Assert.Fail($"块 119 应为段控件或可选文本块，实际 {panel.GetRealizedItem(119)?.GetType().Name}");
                    break;
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void ShrinkingUpperBlocksKeepsLowerViewportBandsRealized() => platform.Run(() =>
    {
        var panel = new WindowedStackPanel();
        for (var i = 0; i < 80; i++)
        {
            panel.AddItem(new Border { Height = 500 });
        }

        var presenter = new ScrollContentPresenter { Content = panel };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 300, Height = 100, Content = scroll };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            scroll.Offset = new Vector(0, 12000);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            // 上方块收缩（量测变矮），视口下方的真实位置整体上移：窗口判定必须跟着重放。
            for (var i = 0; i < 10; i++)
            {
                if (panel.GetRealizedItem(i) is Border border) border.Height = 40;
            }
            panel.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            AssertViewportFullyRealized(panel, scroll);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    /// <summary>探针同款断言：与视口相交的每个条目都必须已物化。</summary>
    private static void AssertViewportFullyRealized(WindowedStackPanel panel, ScrollViewer scroll)
    {
        var viewportTop = scroll.Offset.Y;
        var viewportBottom = scroll.Offset.Y + scroll.Viewport.Height;
        var missed = new List<int>();
        for (var index = 0; index < panel.ItemCount; index++)
        {
            if (!panel.TryGetItemTop(index, out var top)) continue;
            var bandHeight = panel.GetItemHeight(index);
            if (top + bandHeight <= viewportTop || top >= viewportBottom) continue;
            if (panel.GetRealizedItem(index) is null) missed.Add(index);
        }

        Assert.True(missed.Count == 0,
            $"viewport [{viewportTop:0},{viewportBottom:0}] has unrealized bands: {string.Join(",", missed)}");
    }

    private sealed class CollapsingBlock : Control
    {
        private bool _measuredOnce;
        protected override Size MeasureOverride(Size availableSize)
        {
            var height = _measuredOnce ? 0 : 24;
            _measuredOnce = true;
            InvalidateMeasure();
            return new Size(availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize) => finalSize;
    }

    private static void AssertViewportContentReady(WindowedStackPanel panel, ScrollViewer scroll)
    {
        var viewportTop = scroll.Offset.Y;
        var viewportBottom = viewportTop + scroll.Viewport.Height;
        var empty = new List<int>();
        for (var index = 0; index < panel.ItemCount; index++)
        {
            if (!panel.TryGetItemTop(index, out var top)) continue;
            var bandHeight = panel.GetItemHeight(index);
            if (top + bandHeight <= viewportTop || top >= viewportBottom) continue;
            if (panel.GetRealizedItem(index) is Decorator { Child: null }) empty.Add(index);
        }

        Assert.True(empty.Count == 0, $"viewport has realized blocks without content: {string.Join(",", empty)}");
    }

    /// <summary>首次量测报矮、之后报真实高度的控制：模拟延迟建块与异步出图的量测时序。</summary>
    private sealed class GrowingBlock(double realHeight) : Control
    {
        private bool _lied;
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!_lied)
            {
                _lied = true;
                return new Size(availableSize.Width, 24);
            }

            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 100, realHeight);
        }

        protected override Size ArrangeOverride(Size finalSize) => finalSize;
    }
}
