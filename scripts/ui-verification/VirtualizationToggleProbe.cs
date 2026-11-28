using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

/// <summary>
/// 大文档恒开虚拟化首帧诊断（拖拽顺手门禁）：真实样本文档，阅读视图恒开虚拟化。
/// 记录首帧 extent 爬升曲线（估高失真度——失真越大拖动时缩略条映射漂移越重），
/// 并打印估高委托状态与首帧条目高度样本。断言：首帧与稳定总高在同一可用倍数内，
/// 全程单帧不得触碰 5 秒挂起线（同文档同步整建曾把 UI 冻结到被 Windows 判定挂起）。
/// </summary>
internal static class VirtualizationToggleProbe
{
    private const long HangWindowMs = 5000;

    public static void Run(MainWindow window, MainWindowViewModel vm, string output, string path)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        var worstFrame = 0L;
        void Frame()
        {
            var watch = Stopwatch.StartNew();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            worstFrame = Math.Max(worstFrame, watch.ElapsedMilliseconds);
        }

        var preview = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
        window.Width = 1600; window.Height = 900; window.WindowState = WindowState.Maximized;
        Frame();

        Check(vm.OpenPathAsync(path) is { } open && Wait(() => open.IsCompleted, Frame, 60), "Sample document opens");
        vm.Workbench.SelectLayout("read"); Frame();
        Check(viewer.EnableVirtualization, "Reading virtualization is always on (retired preference stays retired)");

        Check(Wait(() => viewer.CurrentModel.Source == vm.Markdown, Frame, 90), "Document model rendered");

        var panel = viewer.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel>().Single();
        var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;

        // 首帧估高成色：extent 曲线 + 条目高度样本（未量测条目读到的就是估高）。
        var extents = new List<double>();
        double last = -1; var stable = 0;
        for (var frame = 0; frame < 900 && stable < 40; frame++)
        {
            Frame();
            var extent = scroll.Extent.Height;
            extents.Add(extent);
            stable = Math.Abs(extent - last) < 1 ? stable + 1 : 0;
            last = extent;
        }
        var finalExtent = extents[^1];
        var heightSamples = string.Join(',', Enumerable.Range(0, Math.Min(12, panel.ItemCount)).Select(panel.GetItemHeight));
        Console.WriteLine($"ESTIMATE curve=[{string.Join(',', extents.Take(16).Select(v => v.ToString("F0")))}] " +
            $"frames={extents.Count} final={finalExtent:F0} resolverSet={panel.EstimateHeightResolver is not null} " +
            $"heightSamples=[{heightSamples}] realized={panel.RealizedItemCount}/{panel.ItemCount}");
        foreach (var group in viewer.CurrentModel.Blocks.GroupBy(b => b.Kind).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"ESTIMATE kind {group.Key}: count={group.Count()} avgSourceChars={group.Average(b => Math.Max(0, b.SourceSpan.End - b.SourceSpan.Start)):F0}");
        }

        Check(extents[0] > finalExtent / 30,
            $"First-frame extent is within a usable factor of the settled total: first={extents[0]:F0} final={finalExtent:F0}");
        Check(extents[^1] <= extents[0] * 30,
            $"Settled extent does not blow past the first frame: first={extents[0]:F0} final={finalExtent:F0}");
        Check(worstFrame <= HangWindowMs,
            $"No layout frame exceeds the {HangWindowMs}ms hang window (worst={worstFrame}ms)");

        File.WriteAllText(Path.Combine(output, "virtualization-toggle-report.txt"),
            $"extents={string.Join(',', extents)}{Environment.NewLine}finalExtent={finalExtent:F0}{Environment.NewLine}worstFrameMs={worstFrame}{Environment.NewLine}");
        Console.WriteLine("PASS virtualization toggle probe complete");
    }

    private static bool Wait(Func<bool> ready, Action frame, int seconds)
    {
        var deadline = TimeSpan.FromSeconds(seconds);
        var start = DateTime.UtcNow;
        do
        {
            frame();
            if (ready()) return true;
        } while (DateTime.UtcNow - start < deadline);
        return false;
    }
}
