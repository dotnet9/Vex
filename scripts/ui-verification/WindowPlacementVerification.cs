using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using CodeWF.Avalonia.Controls.Controls;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views.Behaviors;

internal static class WindowPlacementVerification
{
    public static void Run(VerificationVexApp app, string output)
    {
        var checks = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message); Console.WriteLine("PASS " + message);
        }
        var store = new MemorySettings();
        store.Update(s => s with { WindowWidth = 1100, WindowHeight = 720, IsWindowMaximized = false });
        ShellWindowLayoutViewModel Model(IAppSettingsStore settings) => new(settings, app.Resolve<IShellStatusPublisher>());
        CodeWFWindow Open(ShellWindowLayoutViewModel model)
        {
            var window = new CodeWFWindow { MinWidth = 800, MinHeight = 480 };
            window.Bind(Window.WindowStateProperty, new Binding(nameof(model.CurrentWindowState)) { Source = model, Mode = BindingMode.TwoWay });
            ShellWindowPlacement.SetLayout(window, model);
            window.Show(); Pump();
            return window;
        }
        var model = Model(store);
        var window = Open(model);
        window.Width = 1060; window.Height = 700; Pump();
        window.Close();
        Check(store.Current is { WindowWidth: 1060, WindowHeight: 700, IsWindowMaximized: false }, "Normal resize survives close");
        model = Model(store); window = Open(model);
        Check(window.Width == 1060 && window.Height == 700 && window.WindowState == WindowState.Normal, "Reopen restores normal dimensions and state");
        // 模拟 Win32：尺寸通知先于最大化状态通知，同一个消息循环内不能污染普通尺寸。
        window.Width = 1920; window.Height = 1040; window.UpdateLayout();
        window.WindowState = WindowState.Maximized; Pump(); window.Close();
        Check(store.Current is { WindowWidth: 1060, WindowHeight: 700, IsWindowMaximized: true }, "Maximized resize event order does not overwrite normal dimensions");
        model = Model(store); window = Open(model);
        Check(window.WindowState == WindowState.Maximized && window.Width == 1060 && window.Height == 700, "Reopen is genuinely maximized with separate restore dimensions");
        window.WindowState = WindowState.Normal; Pump(); window.Close();
        Check(store.Current is { WindowWidth: 1060, WindowHeight: 700, IsWindowMaximized: false }, "Restoring and closing remembers normal state");
        foreach (var previous in new[] { WindowState.Normal, WindowState.Maximized })
        foreach (var transient in new[] { WindowState.Minimized, WindowState.FullScreen })
        {
            model = Model(store); window = Open(model);
            window.WindowState = previous; Pump();
            window.Width = 1920; window.Height = 1080; window.UpdateLayout();
            window.WindowState = transient; Pump(); window.Close();
            Check(store.Current.WindowWidth == 1060 && store.Current.WindowHeight == 700
                && store.Current.IsWindowMaximized == (previous == WindowState.Maximized), $"Closing {transient} preserves restore size and prior {previous} state");
        }
        store.Update(s => s with { WindowWidth = 3440, WindowHeight = 1392, IsWindowMaximized = null });
        model = Model(store);
        Check(model.RestoreWindowPlacement(new Size(3440, 1392), new Size(800, 480)) == new Size(1280, 820)
            && model.CurrentWindowState == WindowState.Maximized, "Legacy screen-sized normal configuration migrates to real maximized state");
        model.SaveWindowPlacement();
        Check(store.Current.IsWindowMaximized == true && store.Current.WindowWidth == 1280, "Migration writes explicit maximized marker and normal dimensions");
        store.Update(s => s with { WindowWidth = 3440, WindowHeight = 1392, IsWindowMaximized = false });
        model = Model(store);
        Check(model.RestoreWindowPlacement(new Size(3440, 1392), new Size(800, 480)) == new Size(3440, 1392)
            && model.CurrentWindowState == WindowState.Normal, "Explicit large normal window is not mistaken for legacy corruption");
        store.Update(s => s with { WindowWidth = double.NaN, WindowHeight = double.PositiveInfinity, IsWindowMaximized = null });
        model = Model(store);
        Check(model.RestoreWindowPlacement(new Size(1536, 864), new Size(800, 480)) == new Size(1280, 820), "Invalid settings fall back to defaults in logical screen units");
        Check(model.RestoreWindowPlacement(new Size(1024, 700), new Size(800, 480)) == new Size(1024, 700)
            && model.CurrentWindowState == WindowState.Normal, "Invalid dimensions on a small display do not imply legacy maximization");
        store.Update(s => s with { WindowWidth = null, WindowHeight = null });
        model = Model(store);
        Check(model.RestoreWindowPlacement(new Size(1024, 700), new Size(800, 480)) == new Size(1024, 700)
            && model.CurrentWindowState == WindowState.Normal, "Missing dimensions use clamped defaults without guessing maximized state");
        store.Update(s => s with { WindowWidth = 1600, WindowHeight = 1000, IsWindowMaximized = false });
        model = Model(store);
        Check(model.RestoreWindowPlacement(new Size(1024, 700), new Size(800, 480)) == new Size(1024, 700), "Moving to a smaller display clamps normal dimensions to the working area");
        foreach (var maximized in new[] { true, false })
        {
            var disk = new AppSettingsStore();
            disk.Update(s => s with { WindowWidth = 1060, WindowHeight = 700, IsWindowMaximized = maximized });
            var restored = new AppSettingsStore().Current;
            Check(restored.WindowWidth == 1060 && restored.WindowHeight == 700 && restored.IsWindowMaximized == maximized,
                $"Real configuration roundtrip preserves normal dimensions and maximized={maximized}");
        }
        File.WriteAllLines(Path.Combine(output, "window-placement.txt"), checks);
    }

    private static void Pump() { Dispatcher.UIThread.RunJobs(); }
}
