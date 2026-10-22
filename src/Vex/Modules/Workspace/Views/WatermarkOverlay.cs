using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vex.Modules.Workspace.Views;

/// <summary>正文水印的产品默认值和主题绑定，绘制由基础控件负责；字号/颜色/字重来自偏好。</summary>
public sealed class WatermarkOverlay : CodeWF.Avalonia.Controls.Controls.WatermarkOverlay
{
    public static readonly StyledProperty<string?> HexInkProperty = AvaloniaProperty.Register<WatermarkOverlay, string?>(nameof(HexInk));
    public static readonly StyledProperty<int> WeightValueProperty = AvaloniaProperty.Register<WatermarkOverlay, int>(nameof(WeightValue), 400);

    public WatermarkOverlay()
    {
        Text = "维刻 Vex";
        this.Bind(InkProperty, this.GetResourceObservable("VexAccentBrush"));
    }

    /// <summary>#RRGGBB 前景色；未设置时保持主题强调色。</summary>
    public string? HexInk { get => GetValue(HexInkProperty); set => SetValue(HexInkProperty, value); }
    /// <summary>字重数值（300/400/700），换算为基础控件的 FontWeight。</summary>
    public int WeightValue { get => GetValue(WeightValueProperty); set => SetValue(WeightValueProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HexInkProperty)
        {
            if (HexInk is { } hex)
            {
                try { Ink = new SolidColorBrush(Color.Parse(hex)); } catch { /* 非法颜色保持主题色 */ }
            }
        }
        else if (change.Property == WeightValueProperty)
        {
            FontWeight = (FontWeight)Math.Clamp(WeightValue, 100, 900);
        }
    }
}
