using Avalonia;
using Avalonia.Controls;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellWindowLayoutViewModel
{
    private Size _normalWindowSize = new(1280, 820);
    private bool _wasWindowMaximized;

    public Size RestoreWindowPlacement(Size workArea, Size minimum)
    {
        var settings = _settingsStore.Current;
        var width = Usable(settings.WindowWidth) ? settings.WindowWidth!.Value : 1280;
        var height = Usable(settings.WindowHeight) ? settings.WindowHeight!.Value : 820;
        // 旧版把最大化尺寸保存成普通尺寸；仅迁移没有状态标记且两个方向都占满工作区的配置。
        if (settings.IsWindowMaximized is null && width >= workArea.Width - 16 && height >= workArea.Height - 16)
        {
            width = 1280;
            height = 820;
            CurrentWindowState = WindowState.Maximized;
        }
        _normalWindowSize = new Size(Clamp(width, minimum.Width, workArea.Width), Clamp(height, minimum.Height, workArea.Height));
        return _normalWindowSize;
    }

    public void TrackNormalWindowSize(Size size)
    {
        if (CurrentWindowState == WindowState.Normal && Usable(size.Width) && Usable(size.Height))
            _normalWindowSize = size;
    }

    public void SaveWindowPlacement() => _settingsStore.Update(settings => settings with
    {
        WindowWidth = _normalWindowSize.Width,
        WindowHeight = _normalWindowSize.Height,
        IsWindowMaximized = _wasWindowMaximized
    });

    private static bool Usable(double? value) => value is > 0 && double.IsFinite(value.Value);
    private static double Clamp(double value, double minimum, double maximum) => Math.Clamp(value, minimum, Math.Max(minimum, maximum));
}
