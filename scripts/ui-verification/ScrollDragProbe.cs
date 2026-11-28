using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Workspace.Views;

/// <summary>
/// 大文档阅读视图「滚动条拖拽」探针（快拖卡死门禁）：真实样本文档，虚拟化开/关各一轮，
/// 按住 Thumb 以远超 2 视口高的大步拖到底再拖回顶，逐步墙钟计时并记录物化规模。
/// 钉死两条不变量：拖动全程物化集合保持窗口化（快拖骨架残骸曾把 Measure 的虚拟化
/// 判定算成 false，任何一步都可能触发全量物化卡死）；单步耗时不得超过冻结天花板。
/// </summary>
internal static class ScrollDragProbe
{
    /// <summary>单步天花板：正常窗口物化为毫秒级，整篇物化（约 1600 块 Deferred 全建）是秒级到分钟级。</summary>
    private const long FreezeStepCeilingMs = 5000;

    private const int StepsPerDirection = 10;

    public static void Run(MainWindow window, MainWindowViewModel vm, string output, string path)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Frame()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        bool WaitReady(Func<bool> ready, string stage)
        {
            var deadline = Stopwatch.StartNew();
            do
            {
                Frame();
                if (ready()) return true;
                if (deadline.Elapsed.TotalSeconds > 120) return false;
                Thread.Sleep(5);
            } while (true);
        }

        if (!File.Exists(path)) throw new FileNotFoundException("Pass a large Markdown document as the third argument.", path);

        var preview = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
        var previewVm = Vex.App.Services.GetRequiredService<MarkdownPreviewViewModel>();
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        window.Width = screen is null ? 1600 : screen.WorkingArea.Width / screen.Scaling;
        window.Height = screen is null ? 900 : screen.WorkingArea.Height / screen.Scaling;
        window.WindowState = WindowState.Maximized;

        Frame();
        Check(vm.OpenPathAsync(path) is { } openTask && WaitReady(() => openTask.IsCompleted, "open"), "Sample document opens");
        vm.Workbench.SelectLayout("read"); Frame();

        var report = new List<string>();
        var rounds = new List<object>();
        foreach (var virtualized in new[] { true, false })
        {
            previewVm.Preferences.ReadingVirtualization = virtualized;
            Check(WaitReady(
                () => viewer.CurrentModel.Source == vm.Markdown
                    && viewer.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel>().SingleOrDefault()
                        is { } p && p.IsVirtualizing == virtualized && p.ItemCount > 0,
                $"round virtualized={virtualized}"),
                $"Virtualization switch settles at {virtualized}");

            var panel = viewer.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel>().Single();
            var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
            Check(WaitReady(
                () => scroll.Extent.Height > scroll.Viewport.Height * 2
                    && ViewportCovered(panel, scroll),
                $"round virtualized={virtualized} first viewport"),
                $"First viewport fully covered before dragging (virtualized={virtualized})");

            // 头部块画像：定位振荡诊断里反复出现的低序号块是什么类型。
            for (var i = 20; i <= 28 && i < viewer.CurrentModel.Blocks.Count; i++)
            {
                var model = viewer.CurrentModel.Blocks[i];
                Console.WriteLine($"SCROLLDRAG block{i} kind={model.Kind} heavy={model.IsHeavy} " +
                    $"src={new string(model.SourceText.Trim().Take(56).ToArray())}");
            }

            var bar = scroll.GetVisualDescendants().OfType<ScrollBar>()
                .First(b => b.Orientation == Orientation.Vertical && b.IsEffectivelyVisible);
            var thumb = bar.GetVisualDescendants().OfType<Thumb>().First();
            scroll.Offset = default; Frame();

            var steps = new List<double>();
            var realized = new List<int>();
            var extents = new List<double>();
            long maxStepMs = 0;
            var watch = new Stopwatch();

            void DragStep(Point to)
            {
                watch.Restart();
                window.MouseMove(to, RawInputModifiers.LeftMouseButton);
                var moveMs = watch.ElapsedMilliseconds;
                watch.Restart();
                Dispatcher.UIThread.RunJobs();
                var jobsMs = watch.ElapsedMilliseconds;
                watch.Restart();
                window.UpdateLayout();
                var layoutMs = watch.ElapsedMilliseconds;
                watch.Restart();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var renderMs = watch.ElapsedMilliseconds;
                var total = moveMs + jobsMs + layoutMs + renderMs;
                steps.Add(total);
                maxStepMs = Math.Max(maxStepMs, total);
                realized.Add(panel.RealizedItemCount);
                extents.Add(scroll.Extent.Height);
                Console.WriteLine($"SCROLLDRAG step move={moveMs} jobs={jobsMs} layout={layoutMs} render={renderMs} " +
                    $"comp={panel.AnchorCompensationCount} realized={panel.RealizedItemCount}");
            }

            var from = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            var trackTop = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, 2), window)!.Value;
            var trackBottom = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, bar.Bounds.Height - 2), window)!.Value;

            window.MouseDown(from, MouseButton.Left); Frame();
            // 拖到底：每步都是远超 2 视口高的大跳（45 万 px 文档一步约 4.5 万 px）。
            for (var step = 1; step <= StepsPerDirection; step++)
            {
                DragStep(from + (trackBottom - from) * (step / (double)StepsPerDirection));
            }

            Check(panel.IsVirtualizing == virtualized, $"Virtualization state holds mid-drag (virtualized={virtualized})");
            if (virtualized)
            {
                Check(realized[^1] < panel.ItemCount,
                    $"Fast dragging keeps the realized set windowed: {realized[^1]}/{panel.ItemCount}");
            }

            // 拖回顶：反向同样 10 大步（回滚方向的高度振荡原样复现）。
            var bottom = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            for (var step = 1; step <= StepsPerDirection; step++)
            {
                DragStep(bottom + (trackTop - bottom) * (step / (double)StepsPerDirection));
            }

            window.MouseUp(trackTop, MouseButton.Left); Frame();
            Check(WaitReady(() => ViewportCovered(panel, scroll), $"round virtualized={virtualized} final"),
                $"Viewport fully covered after fast drag (virtualized={virtualized})");
            Check(maxStepMs <= FreezeStepCeilingMs,
                $"No drag step hits the freeze ceiling {FreezeStepCeilingMs}ms (max={maxStepMs}ms, virtualized={virtualized})");

            var ordered = steps.OrderBy(ms => ms).ToList();
            var p95 = ordered[(int)Math.Floor((ordered.Count - 1) * .95)];
            Console.WriteLine($"SCROLLDRAG virtualized={virtualized} steps={steps.Count} max={maxStepMs}ms p95={p95:F0}ms " +
                $"realizedEnd={realized[^1]}/{panel.ItemCount} extentStart={extents[0]:F0} extentEnd={extents[^1]:F0}");
            report.Add($"[virtualized={virtualized}] max={maxStepMs}ms p95={p95:F0}ms realized={string.Join(',', realized)}");
            report.Add($"    stepsMs={string.Join(',', steps.Select(ms => ms.ToString("F1")))}");
            report.Add($"    extents={string.Join(',', extents.Select(v => v.ToString("F0")))}");
            rounds.Add(new { virtualized, steps, realized, extents });
        }

        previewVm.Preferences.ReadingVirtualization = true;
        File.WriteAllText(Path.Combine(output, "scroll-drag-report.txt"),
            "ScrollDragProbe\n" + string.Join("\n", report) + "\n");
        Console.WriteLine("PASS scroll drag probe complete");
    }

    /// <summary>与视口相交的每个条目都已物化且延迟块内容就绪（同库测试 AssertGaps 口径）。</summary>
    private static bool ViewportCovered(CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel panel, ScrollViewer scroll)
    {
        if (scroll.Viewport.Height <= 0 || panel.ItemCount == 0) return false;
        var viewportTop = scroll.Offset.Y;
        var viewportBottom = viewportTop + scroll.Viewport.Height;
        for (var index = 0; index < panel.ItemCount; index++)
        {
            if (!panel.TryGetItemTop(index, out var top)) return false;
            if (top + panel.GetItemHeight(index) <= viewportTop || top >= viewportBottom) continue;
            var realized = panel.GetRealizedItem(index);
            if (realized is null) return false;
            if (realized is Decorator { Child: null }) return false;
        }

        return true;
    }
}
