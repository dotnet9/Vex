using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views.Behaviors;

/// <summary>只连接窗口几何与生命周期，恢复策略和设置持久化由布局 ViewModel 处理。</summary>
public static class ShellWindowPlacement
{
    public static readonly AttachedProperty<ShellWindowLayoutViewModel?> LayoutProperty =
        AvaloniaProperty.RegisterAttached<Window, ShellWindowLayoutViewModel?>("Layout", typeof(ShellWindowPlacement));

    static ShellWindowPlacement() => LayoutProperty.Changed.AddClassHandler<Window, ShellWindowLayoutViewModel?>((window, args) =>
    {
        window.SizeChanged -= OnSizeChanged;
        window.Closed -= OnClosed;
        if (args.NewValue.Value is not { } layout) return;
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        var workArea = screen is null ? new Size(1280, 820)
            : new Size(screen.WorkingArea.Width / screen.Scaling, screen.WorkingArea.Height / screen.Scaling);
        var size = layout.RestoreWindowPlacement(workArea, new Size(window.MinWidth, window.MinHeight));
        window.Width = size.Width;
        window.Height = size.Height;
        window.SizeChanged += OnSizeChanged;
        window.Closed += OnClosed;
    });

    public static ShellWindowLayoutViewModel? GetLayout(Window window) => window.GetValue(LayoutProperty);
    public static void SetLayout(Window window, ShellWindowLayoutViewModel? value) => window.SetValue(LayoutProperty, value);

    private static void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        var window = (Window)sender!;
        // Win32 先通知尺寸再通知状态；等状态稳定后再判断，避免记录最大化/最小化/演示尺寸。
        Dispatcher.UIThread.Post(() =>
        {
            if (window.IsVisible && window.WindowState == WindowState.Normal)
                GetLayout(window)?.TrackNormalWindowSize(window.Bounds.Size);
        }, DispatcherPriority.Background);
    }

    private static void OnClosed(object? sender, EventArgs args)
    {
        var window = (Window)sender!;
        if (window.WindowState == WindowState.Normal)
            GetLayout(window)?.TrackNormalWindowSize(window.Bounds.Size);
        GetLayout(window)?.SaveWindowPlacement();
        window.SizeChanged -= OnSizeChanged;
        window.Closed -= OnClosed;
    }
}
