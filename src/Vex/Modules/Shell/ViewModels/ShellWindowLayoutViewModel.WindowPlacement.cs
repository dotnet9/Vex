using Avalonia;
using Avalonia.Controls;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellWindowLayoutViewModel
{
    private Size _normalWindowSize = new(1280, 820);
    private PixelPoint? _normalWindowPosition;
    private bool _wasWindowMaximized;

    public Size RestoreWindowPlacement(Size workArea, Size minimum)
    {
        var settings = _settingsStore.Current;
        var width = Usable(settings.WindowWidth) ? settings.WindowWidth!.Value : 1280;
        var height = Usable(settings.WindowHeight) ? settings.WindowHeight!.Value : 820;
        // 旧版把最大化尺寸保存成普通尺寸；仅迁移没有状态标记且两个方向都占满工作区的配置。
        if (settings.IsWindowMaximized is null && Usable(settings.WindowWidth) && Usable(settings.WindowHeight)
            && width >= workArea.Width - 16 && height >= workArea.Height - 16)
        {
            width = 1280;
            height = 820;
            CurrentWindowState = WindowState.Maximized;
        }
        _normalWindowSize = new Size(Clamp(width, minimum.Width, workArea.Width), Clamp(height, minimum.Height, workArea.Height));
        return _normalWindowSize;
    }

    /// <summary>
    /// 恢复上次正常态位置（设备像素）：找回点所在的屏幕并把左上角夹进其工作区
    /// （跨屏/换分辨率后不至于开在不可见处）。无可恢复位置返回 false（沿用 CenterScreen）。
    /// </summary>
    public bool RestoreWindowPosition(Window window)
    {
        var settings = _settingsStore.Current;
        if (!Usable(settings.WindowX) || !Usable(settings.WindowY)) return false;
        var saved = new PixelPoint((int)settings.WindowX!.Value, (int)settings.WindowY!.Value);
        var screen = window.Screens.ScreenFromPoint(saved) ?? window.Screens.Primary ?? window.Screens.All.FirstOrDefault();
        if (screen is null) return false;
        var work = screen.WorkingArea;
        // 标题栏保持可达：左上角最多探入到距右/下边界一段余量。
        var x = Math.Clamp(saved.X, work.X + 8, Math.Max(work.X + 8, work.Right - 160));
        var y = Math.Clamp(saved.Y, work.Y, Math.Max(work.Y, work.Bottom - 40));
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(x, y);
        _normalWindowPosition = window.Position;
        return true;
    }

    public void TrackNormalWindowSize(Size size)
    {
        if (CurrentWindowState == WindowState.Normal && Usable(size.Width) && Usable(size.Height))
            _normalWindowSize = size;
    }

    /// <summary>正常态下跟踪窗口位置（最大化/最小化/演示态的位移不记录）。</summary>
    public void TrackNormalWindowPosition(PixelPoint position)
    {
        if (CurrentWindowState == WindowState.Normal) _normalWindowPosition = position;
    }

    public void SaveWindowPlacement() => _settingsStore.Update(settings => settings with
    {
        WindowWidth = _normalWindowSize.Width,
        WindowHeight = _normalWindowSize.Height,
        WindowX = _normalWindowPosition?.X,
        WindowY = _normalWindowPosition?.Y,
        IsWindowMaximized = _wasWindowMaximized
    });

    private static bool Usable(double? value) => value is > 0 && double.IsFinite(value.Value);
    private static double Clamp(double value, double minimum, double maximum) => Math.Clamp(value, minimum, Math.Max(minimum, maximum));
}
