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
                Check(Math.Abs(viewer.Margin.Left - expected.Left) < .1 && viewer.Margin.Top == expected.Top
                    && viewer.Margin.Bottom == expected.Bottom && viewer.MaxWidth + viewer.Margin.Left + viewer.Margin.Right == 820,
                    $"Reading measure and responsive insets match the prototype at {width}/{layout}/{zoom}: {viewer.Margin}, max={viewer.MaxWidth}");
                Check(viewer.Bounds.Width <= viewer.MaxWidth + 1
                    && (viewer.Bounds.Width + viewer.Margin.Left + viewer.Margin.Right) * scale <= scroll.Viewport.Width + 2,
                    $"Zoomed document stays within the reading viewport at {width}/{layout}/{zoom}");
            }
            foreach (var layout in new[] { "instant", "instant-map" })
            {
                vm.Workbench.SelectLayout(layout); Render();
                var combined = layout == "instant-map";
                var side = combined ? Math.Clamp(instant.Bounds.Width * .07, 14, 30) : width <= 850 ? 28 : Math.Clamp(width * .06, 28, 88);
                Check(instant.DocumentPadding == new Thickness(side, combined ? 22 : width <= 850 ? 28 : 36, side, combined ? 56 : width <= 850 ? 28 : 80)
                    && double.IsPositiveInfinity(instant.DocumentMaxWidth), $"Instant editing uses its pane width and has no artificial 1280px cap at {width}/{layout}");
            }
        }
        Check(vm.Markdown == source, "Changing reading geometry never modifies the source document");
    }
}
