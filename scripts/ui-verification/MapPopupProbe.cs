using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>临时探针：打开导图样式弹层，按原型两个页签各截一张，供与 design 原型逐项目视对照。</summary>
internal static class MapPopupProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        Directory.CreateDirectory(output);
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            LayoutSettle.Run();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        window.Width = 1440; window.Height = 900; Render();
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectPaletteByKey("indigo");
        const char newline = (char)10;
        var sample = VisualParityCapture.SampleDocument;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(sample, 1, 1, sample.Count(ch => ch == newline) + 1));
        Render();
        Thread.Sleep(900);
        vm.Workbench.SelectLayout("map");
        Render();

        var trigger = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Classes.Contains("map-tool") && b.Flyout is not null);
        Console.WriteLine($"TRIGGER_FOUND={trigger != null}");
        if (trigger is null) return;
        trigger.Flyout!.ShowAt(trigger);
        Render();
        var content = (Control)((Flyout)trigger.Flyout).Content!;
        var presenter = content.GetVisualAncestors().OfType<FlyoutPresenter>().First();
        void Capture(string name)
        {
            Render();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)presenter.Bounds.Width, (int)presenter.Bounds.Height));
            bitmap.Render(presenter);
            bitmap.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine($"CAPTURE {name}: {presenter.Bounds}");
        }
        Capture("map-popup-structure");
        var styleTab = content.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("map-appearance-tab")).ElementAt(1);
        styleTab.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Capture("map-popup-style");
        trigger.Flyout.Hide();
    }
}
