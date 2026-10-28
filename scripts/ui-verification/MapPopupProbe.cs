using CodeWF.Avalonia.Controls.Controls;
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

/// <summary>临时探针：打开导图样式弹层截图，供与 design 原型逐项目视对照（结构与风格两排同屏）。</summary>
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
        Capture("map-popup-both-rows");
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        static Size Miniature(Control host, string name) => host.GetVisualDescendants().OfType<Button>()
            .First(b => b.Name == name).GetVisualDescendants().OfType<Canvas>().First().Bounds.Size;
        var steps = content.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Classes.Contains("map-step-label")).ToArray();
        Check(steps.Length == 2, $"弹层内两排同屏，只剩两个步骤标签（实得 {steps.Length}）");
        var stepY = steps.Select(s => s.TranslatePoint(default, presenter)!.Value.Y).ToArray();
        Check(stepY[1] > stepY[0], $"①结构在上、②风格在下（y={stepY[0]:0.##} → {stepY[1]:0.##}）");
        Check(!content.GetVisualDescendants().OfType<SegmentedPanel>().Any(), "分段页签已删除，两排不再互相隐藏");
        var choices = content.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("map-choice")).ToArray();
        Check(choices.Length == 12 && choices.All(b => b.IsVisible), $"12 张卡全部同屏可见（实得 {choices.Length}）");
        Check(content.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Classes.Contains("map-choice-purpose")) == 6,
            "六张风格卡各带一行说明");
        var before = ShellWorkbenchViewModel.MindMapStyleKeys.Select(key => Miniature(content, "MapChoiceStyle_" + key)).ToArray();
        // 改选「中心放射」：风格排的线稿要跟着换形（双向联动），并把「② 风格」点亮引路。
        content.GetVisualDescendants().OfType<Button>().First(b => b.Name == "MapChoiceLayout_radial")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Render();
        var after = ShellWorkbenchViewModel.MindMapStyleKeys.Select(key => Miniature(content, "MapChoiceStyle_" + key)).ToArray();
        var radial = Miniature(content, "MapChoiceLayout_radial");
        Check(after.All(size => size == radial) && !after.SequenceEqual(before), "风格线稿随所选结构整体重绘");
        // 点卡片会重建两排步骤标签，旧实例已不在树上，只能重新按位置取。
        var litSteps = content.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Classes.Contains("map-step-label"))
            .Select(p => (p, p.TranslatePoint(default, presenter)!.Value.Y)).ToArray();
        Check(litSteps.Length == 2 && litSteps.Single(s => s.p.Classes.Contains("map-step-nudge")).Y == litSteps.Max(s => s.Y),
            "选完结构后点亮的是下面那排「风格」");
        Capture("map-popup-after-radial");
        trigger.Flyout.Hide();
    }
}
