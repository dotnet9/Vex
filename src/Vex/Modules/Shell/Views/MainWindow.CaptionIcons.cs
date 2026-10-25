using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vex.Controls;
using CaptionPath = Avalonia.Controls.Shapes.Path;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow
{
    private CaptionPath? _maximizeRestoreIcon;

    /// <summary>只替换图标内容，保留窗口模板中的按钮、系统角色及事件。</summary>
    private void InstallCaptionIcons(INameScope nameScope)
    {
        InstallCaptionIcon(nameScope, "PART_MinimizeButton", "minus");
        _maximizeRestoreIcon = InstallCaptionIcon(nameScope, "PART_MaximizeRestoreButton", "square");
        InstallCaptionIcon(nameScope, "PART_CloseButton", "x");
        UpdateMaximizeRestoreIcon();
    }

    private CaptionPath? InstallCaptionIcon(INameScope nameScope, string buttonName, string iconKey)
    {
        if (nameScope.Find<Button>(buttonName) is not { } button)
        {
            return null;
        }

        var icon = new CaptionPath
        {
            Width = 24, Height = 24, Stretch = Stretch.None,
            Data = Geometry.Parse(MenuIconGeometry.Data[iconKey]), StrokeThickness = 2,
            StrokeJoin = PenLineJoin.Round, StrokeLineCap = PenLineCap.Round
        };
        icon.Classes.Add("window-caption-icon");
        icon.Bind(CaptionPath.StrokeProperty, button.GetObservable(Button.ForegroundProperty));
        var viewport = new Viewbox { Child = icon };
        viewport.Classes.Add("window-caption-viewport");
        viewport.Bind(WidthProperty, this.GetObservable(CaptionGlyphSizeProperty));
        viewport.Bind(HeightProperty, this.GetObservable(CaptionGlyphSizeProperty));
        button.Content = viewport;
        return icon;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateMaximizeRestoreIcon();
        }
    }

    private void UpdateMaximizeRestoreIcon()
    {
        if (_maximizeRestoreIcon is not null)
        {
            // 原型 svg 视口 24，图标按 Lucide 原生边界呈现，不再归一化到固定尺寸。
            _maximizeRestoreIcon.Data = Geometry.Parse(MenuIconGeometry.Data[WindowState == WindowState.Maximized ? "window-restore" : "square"]);
        }
    }
}
