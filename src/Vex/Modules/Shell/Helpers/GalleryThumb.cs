using Avalonia;
using Avalonia.Controls;

namespace Vex.Modules.Shell.Helpers;

/// <summary>Mermaid 图鉴卡的缩略图键：附加在卡片 Image 上，供飞出层打开时按键渲染填充。</summary>
public static class GalleryThumb
{
    public static readonly AttachedProperty<string?> ThumbKeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ThumbKey", typeof(GalleryThumb));

    public static string? GetThumbKey(Control control) => control.GetValue(ThumbKeyProperty);

    public static void SetThumbKey(Control control, string? value) => control.SetValue(ThumbKeyProperty, value);
}
