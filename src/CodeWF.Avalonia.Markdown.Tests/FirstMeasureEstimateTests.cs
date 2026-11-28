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
/// 首帧估高探针：巨文档虚拟化开时，首次量测的 extent 必须接近真实总高——
/// 否则拖动滚动条全程缩略条映射漂移（估高失真 24 倍实测）。诊断输出打印
/// 首帧条目高度样本与解析器状态，供定位估高未生效的环节。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class FirstMeasureEstimateTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void FirstMeasureExtent_IsWithinAnOrderOfMagnitudeOfRealTotal() => platform.Run(() =>
    {
        // 40 个段落 + 5 个大代码块：段落约 44px、代码块约 160px，真实总高约 2600px。
        var source = string.Join("\n\n", Enumerable.Range(1, 40)
            .Select(i => $"第 {i} 段。这一段有足够多的正文内容，用来撑出可观的量测高度。"))
            + "\n\n" + string.Join("\n\n", Enumerable.Range(1, 5).Select(i =>
            "```csharp\n" + string.Join("\n", Enumerable.Range(1, 8).Select(l => $"Console.WriteLine(\"{i}-{l}\");")) + "\n```"));
        var viewer = new MarkdownViewer { EnableVirtualization = true, Markdown = source };
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
            // 只跑一轮布局：首帧估高的成色。
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            var firstExtent = scroll.Extent.Height;
            var samples = string.Join(',', Enumerable.Range(0, Math.Min(10, panel.ItemCount))
                .Select(panel.GetItemHeight));
            var fullExtent = MeasureFullExtent(panel, scroll, window);
            Console.WriteLine($"ESTIMATE firstExtent={firstExtent:F0} itemCount={panel.ItemCount} " +
                $"heightSamples=[{samples}] resolverSet={panel.EstimateHeightResolver is not null} fullExtent={fullExtent:F0}");

            Assert.True(panel.EstimateHeightResolver is not null, "面板估高委托已接线");
            Assert.True(firstExtent > fullExtent / 10 && firstExtent < fullExtent * 10,
                $"首帧 extent 应在一个数量级内逼近真实总高: first={firstExtent:F0} full={fullExtent:F0}");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    /// <summary>逐屏滚动到末尾，让全部条目量测出真实高度后的总高。</summary>
    private static double MeasureFullExtent(WindowedStackPanel panel, ScrollViewer scroll, Window window)
    {
        for (var guard = 0; guard < 500; guard++)
        {
            for (var pass = 0; pass < 4; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            if (scroll.Offset.Y >= maximum - 1)
            {
                for (var pass = 0; pass < 8; pass++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                return scroll.Extent.Height;
            }

            scroll.Offset = new Vector(0, scroll.Offset.Y + scroll.Viewport.Height);
        }

        return scroll.Extent.Height;
    }
}
