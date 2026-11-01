using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

/// <summary>真实长文档的绘制、八布局切换和逐屏图表呈现；首帧不等同于整篇就绪。</summary>
internal static class DocumentPerformanceProbe
{
    public static void Run(MainWindow window, MainWindowViewModel vm, string output, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Pass a large Markdown document as the third argument.", path);
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        using var process = Process.GetCurrentProcess();
        long peakPrivate = 0, peakWorkingSet = 0;
        void SampleMemory()
        {
            lock (process)
            {
                process.Refresh();
                peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
                peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
            }
        }
        using var sampler = new Timer(_ => SampleMemory(), null, 0, 20);
        void Frame()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            SampleMemory();
        }
        void Wait(Func<bool> ready)
        {
            var deadline = Stopwatch.StartNew();
            do
            {
                Frame();
                if (ready()) return;
                if (deadline.Elapsed.TotalSeconds > 30) throw new TimeoutException("Document or visible diagram did not finish rendering.");
                Thread.Sleep(10);
            } while (true);
        }
        void Save(string name)
        {
            Frame();
            using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered document frame.");
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
        var preview = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
        var instant = window.Workspace.FindControl<MarkdownInstantView>("InstantGuideTarget")!;
        bool DocumentReady() => (!preview.IsEffectivelyVisible || viewer.CurrentModel.Source == vm.Markdown)
            && (!instant.IsEffectivelyVisible || instant.Editor.Text == vm.Markdown)
            && vm.Workbench.Root is not null;
        Border[] Diagrams(Control host) => host.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains("MdMermaid") && border.IsEffectivelyVisible).ToArray();
        bool DiagramsReady(Control host) => Diagrams(host).All(border => border.GetVisualDescendants()
            .OfType<Image>().Any(image => image.Source is not null));

        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        window.Width = screen is null ? 1920 : screen.WorkingArea.Width / screen.Scaling;
        window.Height = screen is null ? 1080 : screen.WorkingArea.Height / screen.Scaling;
        window.WindowState = WindowState.Maximized;
        vm.Workbench.SelectLayout("source"); Frame();
        var watch = Stopwatch.StartNew();
        var open = vm.OpenPathAsync(path);
        Wait(() => open.IsCompleted);
        open.GetAwaiter().GetResult();
        Frame();
        using var firstFrame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered source frame.");
        var firstFrameMs = watch.Elapsed.TotalMilliseconds;
        firstFrame.Save(Path.Combine(output, "first-source-frame.png"));
        Wait(DocumentReady);
        Console.WriteLine($"PERFORMANCE source frame={firstFrameMs:F1}ms document model={watch.Elapsed.TotalMilliseconds:F1}ms");
        var layouts = new List<object>();
        foreach (var option in WorkbenchLayoutOption.All)
        {
            var frames = new List<double>();
            var ready = new List<double>();
            for (var repeat = 0; repeat < 3; repeat++)
            {
                vm.Workbench.SelectLayout(option.Id == "source" ? "read" : "source"); Frame();
                watch.Restart(); vm.Workbench.SelectLayout(option.Id); Frame();
                frames.Add(watch.Elapsed.TotalMilliseconds);
                Wait(() => DocumentReady() && DiagramsReady(window.Workspace));
                Frame(); ready.Add(watch.Elapsed.TotalMilliseconds);
            }
            layouts.Add(new { layout = option.Id, frameMs = frames, visibleContentReadyMs = ready });
            Console.WriteLine($"PERFORMANCE {option.Id}: frames={string.Join(',', frames.Select(ms => ms.ToString("F1")))}ms ready={string.Join(',', ready.Select(ms => ms.ToString("F1")))}ms");
        }

        var sweeps = new List<object>();
        foreach (var layout in new[] { "read", "instant" })
        {
            vm.Workbench.SelectLayout(layout); Wait(DocumentReady);
            Control host = layout == "read" ? preview : instant;
            var scroll = layout == "read" ? preview.FindControl<ScrollViewer>("PreviewScrollViewer")!
                : instant.Editor.GetVisualDescendants().OfType<ScrollViewer>()
                    .Where(view => view.IsEffectivelyVisible && view.Viewport.Height > 0)
                    .MaxBy(view => view.Viewport.Width * view.Viewport.Height)!;
            scroll.Offset = default; Frame();
            var pages = 0; var diagramExposures = 0; var fallbackExposures = 0;
            watch.Restart();
            while (true)
            {
                Wait(() => DiagramsReady(host)); Frame();
                pages++; diagramExposures += Diagrams(host).Length;
                fallbackExposures += host.GetVisualDescendants().OfType<Control>().Count(control => control.Classes.Contains("MdMermaidFallback"));
                var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                if (scroll.Offset.Y >= maximum - 1) break;
                if (pages > 2000) throw new InvalidOperationException("Document scroll did not reach its end.");
                scroll.Offset = new Vector(0, Math.Min(maximum, scroll.Offset.Y + Math.Max(1, scroll.Viewport.Height * .8)));
                Frame();
            }
            Save(layout + "-end");
            sweeps.Add(new { layout, pages, diagramExposures, fallbackExposures, elapsedMs = watch.Elapsed.TotalMilliseconds });
            Console.WriteLine($"PERFORMANCE {layout} full scroll: {pages} pages, {diagramExposures} diagram exposures, {fallbackExposures} fallback exposures, {watch.Elapsed.TotalSeconds:F1}s");
        }
        SampleMemory();
        var unchanged = hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)));
        var report = new { bytes = new FileInfo(path).Length, characters = vm.Markdown.Length, sha256 = Convert.ToHexString(hash), unchanged,
            width = window.Bounds.Width, height = window.Bounds.Height, scaling = window.RenderScaling, firstFrameMs,
            peakPrivateMiB = peakPrivate / 1048576d, peakWorkingSetMiB = peakWorkingSet / 1048576d, layouts, sweeps,
            runtime = Environment.Version.ToString(),
            environment = "Windows Release, Avalonia.Headless + Skia; 20ms memory sampling, actual rendered frames and full document scrolling" };
        File.WriteAllText(Path.Combine(output, "document-performance.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (!unchanged) throw new InvalidOperationException("Performance sample was modified.");
        Console.WriteLine($"PASS performance measurements complete; private peak={report.peakPrivateMiB:F1}MiB working set peak={report.peakWorkingSetMiB:F1}MiB; sample unchanged. This is a measurement, not a performance-budget pass.");
    }
}
