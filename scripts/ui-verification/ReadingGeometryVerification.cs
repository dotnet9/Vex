using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

internal static class ReadingGeometryVerification
{
    public static void Run(MainWindow window)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var preview = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
        var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
        var instant = window.Workspace.FindControl<MarkdownInstantView>("InstantGuideTarget")!.Editor;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); LayoutSettle.Run(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        var source = vm.Markdown;
        var readingModel = (Vex.Modules.Workspace.ViewModels.MarkdownPreviewViewModel)preview.DataContext!;
        foreach (var width in new[] { 800, 1024, 1440, 1920 })
        {
            window.Width = width; window.Height = 900;
            foreach (var layout in new[] { "read", "source-render" })
            foreach (var zoom in new[] { 50, 100, 200 })
            {
                vm.Workbench.SelectLayout(layout); vm.Workbench.Preferences.ContentZoom = zoom; Render();
                var scale = zoom / 100d;
                var expected = layout == "read"
                    ? width <= 900 ? new Thickness(24, 20, 24, 60) : width <= 1100 ? new Thickness(36, 24, 36, 70) : new Thickness(52, 30, 52, 88)
                    : new Thickness(Math.Clamp(scroll.Viewport.Width / scale * .08, 14, 32), 24, Math.Clamp(scroll.Viewport.Width / scale * .08, 14, 32), 56);
                var maximum = layout == "read" ? readingModel.MaximumReadingWidth : 820 - viewer.Margin.Left - viewer.Margin.Right;
                Check(Math.Abs(viewer.Margin.Left - expected.Left) < .1 && viewer.Margin.Top == expected.Top
                    && viewer.Margin.Bottom == expected.Bottom && Math.Abs(viewer.MaxWidth - maximum) < .1,
                    $"Reading measure follows the display and responsive insets at {width}/{layout}/{zoom}: {viewer.Margin}, max={viewer.MaxWidth}");
                Check(viewer.Bounds.Width <= viewer.MaxWidth + 1
                    && (viewer.Bounds.Width + viewer.Margin.Left + viewer.Margin.Right) * scale <= scroll.Viewport.Width + 2,
                    $"Zoomed document stays within the reading viewport at {width}/{layout}/{zoom}");
            }
            foreach (var layout in new[] { "instant", "instant-map" })
            {
                vm.Workbench.SelectLayout(layout); Render();
                var combined = layout == "instant-map";
                var side = combined ? Math.Clamp(instant.Bounds.Width * .07, 14, 30) : width <= 850 ? 28 : Math.Clamp((width + 48) * .06, 28, 88);
                Check(instant.DocumentPadding == new Thickness(side, combined ? 22 : width <= 850 ? 28 : 36, side, combined ? 56 : width <= 850 ? 28 : 80)
                    && double.IsPositiveInfinity(instant.DocumentMaxWidth), $"Instant editing uses its pane width and has no artificial 1280px cap at {width}/{layout}");
            }
        }
        Check(vm.Markdown == source, "Changing reading geometry never modifies the source document");
        const string shortDocument = "# 短文\n\n短段落。\n";
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(shortDocument, 1, 1, 4));
        foreach (var width in new[] { 800, 1024, 1440, 1920 })
        foreach (var layout in new[] { "read", "source-render" })
        foreach (var zoom in new[] { 50, 100, 200 })
        {
            window.Width = width; vm.Workbench.SelectLayout(layout);
            vm.Workbench.Preferences.ContentZoom = zoom; Render();
            var available = Math.Max(0, scroll.Viewport.Width - scroll.Padding.Left - scroll.Padding.Right) / (zoom / 100d);
            var maximum = layout == "read" ? readingModel.MaximumReadingWidth : 820 - viewer.Margin.Left - viewer.Margin.Right;
            var expected = Math.Max(0, Math.Min(maximum, available - viewer.Margin.Left - viewer.Margin.Right));
            Check(Math.Abs(viewer.Bounds.Width - expected) < 1,
                $"Short documents fill the same reading column at {width}/{layout}/{zoom}: actual={viewer.Bounds.Width:0.##}, expected={expected:0.##}");
        }
        Check(vm.Markdown == shortDocument, "Reading width and zoom preserve short document text");
        window.Width = 3000; vm.Workbench.SelectLayout("read");
        foreach (var display in new[] { (Width: 1920d, Scaling: 1d), (Width: 2560d, Scaling: 1d), (Width: 3840d, Scaling: 1.5d), (Width: 3840d, Scaling: 2d) })
        foreach (var zoom in new[] { 50, 100, 200 })
        {
            readingModel.UpdateDisplayMetrics(display.Width, display.Scaling);
            vm.Workbench.Preferences.ContentZoom = zoom; Render();
            var expected = display.Width / display.Scaling / 2;
            Check(Math.Abs(viewer.Bounds.Width * zoom / 100d - expected) < 1,
                $"Single reading content occupies half the display at {display.Width}/{display.Scaling}/{zoom}: {viewer.Bounds.Width:0.##}");
        }
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(source, 1, 1, source.Count(c => c == '\n') + 1));
    }
}
