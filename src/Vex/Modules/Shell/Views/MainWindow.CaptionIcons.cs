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
            Data = CaptionGeometry(iconKey), StrokeThickness = 1.875,
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

    /// <summary>按几何边界统一视觉尺寸，避免横线和叉号在相同视口中显得过小。</summary>
    private static Geometry CaptionGeometry(string iconKey)
    {
        var geometry = MenuIconCatalog.Get(iconKey);
        var bounds = geometry.Bounds;
        var scale = 18 / Math.Max(bounds.Width, bounds.Height);
        geometry.Transform = new MatrixTransform(new Matrix(scale, 0, 0, scale,
            12 - bounds.Center.X * scale, 12 - bounds.Center.Y * scale));
        return geometry;
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
            _maximizeRestoreIcon.Data = CaptionGeometry(WindowState == WindowState.Maximized ? "window-restore" : "square");
        }
    }
}
