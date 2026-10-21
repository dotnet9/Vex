using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Controls.Animations;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class MotionVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Tick()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        vm.Layout.EnableMotion = true;
        var pending = app.Resolve<IShellOverlayService>().ShowPreferencesAsync(app.Resolve<ShellPreferencesViewModel>());
        Tick();
        var view = window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Single();
        var card = view.GetVisualDescendants().OfType<Border>().Single(x => x.Classes.Contains("dialog-card"));
        Check(EntranceMotion.GetIsEnabled(card), "Real preferences dialog uses prototype entrance motion");
        var bounds = card.Bounds;
        var intermediate = false;
        var stableBounds = true;
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 320)
        {
            Thread.Sleep(8); Tick();
            intermediate |= card.Opacity > .01 && card.Opacity < .99;
            stableBounds &= card.Bounds == bounds;
        }
        Check(stableBounds, "Entrance keeps layout bounds stable across all animation frames");
        Check(intermediate && Math.Abs(card.Opacity - 1) < .001, $"Dialog renders intermediate frames and reaches full opacity (intermediate={intermediate}, final={card.Opacity})");
        using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "motion-complete.png"));
        view.IsVisible = false; Tick(); view.IsVisible = true; Tick();
        vm.Layout.EnableMotion = false; Tick();
        Check(Math.Abs(card.Opacity - 1) < .001 && (card.RenderTransform is null || card.RenderTransform.Value.IsIdentity), "Disabling motion cancels the active animation and restores base values");
        view.IsVisible = false; Tick(); view.IsVisible = true; Tick();
        Check(Math.Abs(card.Opacity - 1) < .001, "Disabled motion shows reopened dialogs immediately");
        vm.Layout.EnableMotion = true;
        for (var i = 0; i < 20; i++) { view.IsVisible = false; Tick(); view.IsVisible = true; Tick(); }
        app.Resolve<ShellPreferencesViewModel>().Close(); Tick();
        Check(pending.IsCompleted, "Rapid visibility changes and closing do not delay dialog completion");
        pending.GetAwaiter().GetResult();
        vm.Layout.EnableMotion = false;
        Check(window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Count() == 0, "Closing removes the animated dialog from the visual tree");
        vm.Layout.EnableMotion = true;
    }
}
