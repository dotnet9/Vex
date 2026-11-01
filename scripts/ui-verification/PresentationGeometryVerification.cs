using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Core.Messaging;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

internal static class PresentationGeometryVerification
{
    public static void Run(MainWindow window)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var view = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = view.FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
        var transform = view.FindControl<LayoutTransformControl>("DocumentTransform")!;
        var scroll = view.FindControl<ScrollViewer>("PreviewScrollViewer")!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            LayoutSettle.Run();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
        void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        window.Width = 1440; window.Height = 900;
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectTypographyByKey("Simple");
        vm.Markdown = VisualParityCapture.SampleDocument;
        vm.Workbench.SelectLayout("read"); Render();
        var normalSize = viewer.ParagraphFontSize;
        for (var round = 0; round < 2; round++)
        {
            vm.Appearance.SelectThemeByKey(round == 0 ? "light" : "dark");
            vm.Workbench.StartPresentation(); Render();
            foreach (var scale in new[] { 1d, 1.5, 2.5 })
            {
                vm.Workbench.DeckScale = scale; Render();
                var heading = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().Single(c => c.Classes.Contains("MdH1"));
                var frame = viewer.GetVisualDescendants().OfType<Border>().Single(c => c.Classes.Contains("MdH1Border"));
                var paragraph = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().Single(c => c.Classes.Contains("MdParagraph"));
                var origin = frame.TranslatePoint(default, window)!.Value;
                var paragraphOrigin = paragraph.TranslatePoint(default, window)!.Value;
                Check(transform.LayoutTransform is null && viewer.Margin == new Thickness(52, 32, 52, 64)
                    && Math.Abs(viewer.Bounds.Width - Math.Min(1016, scroll.Bounds.Width - 104)) < .1,
                    $"Presentation {scale:0.0} keeps its fixed screen padding and 1120px frame");
                Check(Math.Abs(heading.FontSize - 30 * scale) < .01
                    && Math.Abs(heading.LineHeight - 43.5 * scale) < .01
                    && frame.BorderThickness == default && frame.Padding == default,
                    $"Presentation {scale:0.0} uses original article heading metrics without reading decorations");
                Check(Math.Abs(paragraph.FontSize - 15 * scale) < .01
                    && Math.Abs(paragraph.LineHeight - 28.5 * scale) < .01
                    && Math.Abs(paragraphOrigin.Y - origin.Y - 60 * scale) < .1,
                    $"Presentation {scale:0.0} keeps paragraph baselines and collapsed heading margins");
                if (scale == 1.5)
                    Check(Math.Abs(origin.X - 72) < .1 && Math.Abs(origin.Y - 65) < .1
                        && Math.Abs(paragraphOrigin.Y - 155) < .1,
                        $"1440px presentation matches prototype x72/y65 heading and y155 paragraph: heading={origin}, paragraph={paragraphOrigin}, window={window.Bounds.Size}");
            }
            vm.Workbench.StopPresentation(); Render();
            Check(Math.Abs(viewer.ParagraphFontSize - normalSize) < .01
                && viewer.GetVisualDescendants().OfType<Border>().Single(c => c.Classes.Contains("MdH1Border")).BorderThickness.Bottom == 1,
                "Exiting presentation restores the ordinary reading typography");
        }
    }
}
