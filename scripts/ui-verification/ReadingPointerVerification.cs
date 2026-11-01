using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class ReadingPointerVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1280; window.Height = 820;
        vm.Workbench.SelectLayout("read");
        vm.Markdown = "# Pointer verification\n\nVex - 写下想法，看见脉络。\n\n```mermaid\nflowchart LR\n A[Start] --> B[End]\n```\n\nAfter diagram.\n";
        var viewer = window.Workspace.FindControl<Control>("PreviewGuideTarget")!
            .FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        Render();
        foreach (var scaling in new[] { 1d, 1.25, 1.5, 2 })
        foreach (var theme in new[] { "light", "dark" })
        foreach (var culture in new[] { "zh-CN", "en-US", "ja-JP", "zh-Hant" })
        {
            window.SetRenderScaling(scaling);
            vm.Appearance.SelectThemeByKey(theme); vm.Appearance.SelectLanguageByCulture(culture); Render();
            var text = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
                .First(t => (t.Inlines?.Text ?? t.Text ?? "").StartsWith("Vex -"));
            var menu = text.ContextMenu!;
            var point = text.TranslatePoint(new Point(30, 10), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right);
            for (var i = 0; i < 8; i++) Render();
            Check(menu.IsOpen, "Reading text opens its menu through the actual right pointer button");
            var scroll = menu.GetVisualDescendants().OfType<ScrollViewer>().First();
            var rows = menu.Items.OfType<MenuItem>().ToArray();
            foreach (var label in menu.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
                Check(Math.Abs(label.LineHeight - 22.4) < .01, "Reading menu resets inherited document line height");
            Console.WriteLine($"READING MENU {scaling}/{theme}/{culture} bounds={menu.Bounds}, viewport={scroll.Viewport}, extent={scroll.Extent}");
            if (scaling == 1) window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"reading-menu-{theme}-{culture}.png"));
            Check(rows.Length == 2 && scroll.Extent.Height <= scroll.Viewport.Height + .5,
                "Two reading actions fit without scrolling: " + theme + "/" + culture);
            foreach (var row in rows)
            {
                var origin = row.TranslatePoint(default, scroll)!.Value;
                Check(origin.Y >= 0 && origin.Y + row.Bounds.Height <= scroll.Bounds.Height + .5,
                    "Reading menu row is fully visible: " + row.Header);
            }
            menu.Close(); Render();
        }
        window.SetRenderScaling(1);
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        var deadline = DateTime.UtcNow.AddSeconds(12);
        Image? image;
        do
        {
            Render();
            image = viewer.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("MdMermaid"))?
                .GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Source is not null);
            if (image is not null) break;
            Thread.Sleep(10);
        } while (DateTime.UtcNow < deadline);
        Check(image is not null, "Mermaid renders before pointer interaction");
        var diagramPoint = image!.TranslatePoint(new Point(image.Bounds.Width / 2, image.Bounds.Height / 2), window)!.Value;
        window.MouseMove(diagramPoint);
        window.MouseDown(diagramPoint, MouseButton.Left); window.MouseUp(diagramPoint, MouseButton.Left); Render();
        Check(!viewer.HasSelection, "Clicking a diagram leaves no phantom text selection");
        Check(vm.Markdown.Contains("A[Start] --> B[End]"), "Diagram click preserves source");
    }
}
