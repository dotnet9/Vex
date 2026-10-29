using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vex.Modules.Shell.Views;

/// <summary>焦点外环专用 Border。AdornerLayer 会把每个 adorner 的 Clip 设成被饰控件的盒子，
/// 而原型 outline 画在盒外 offset 2/3，负 margin 的环会被裁得只剩左上角；这里把该裁剪强制为空，
/// 让 2px 环完整落在控件盒外。只给 FocusAdorner 模板用，别处仍吃框架裁剪。</summary>
public sealed class FocusRingBorder : Border
{
    static FocusRingBorder() => ClipProperty.OverrideMetadata<FocusRingBorder>(
        new StyledPropertyMetadata<Geometry?>(coerce: (_, _) => null));
    protected override Type StyleKeyOverride => typeof(Border);
}