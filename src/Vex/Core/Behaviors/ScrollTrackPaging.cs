using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Vex.Core.Behaviors;

/// <summary>
/// 原型浏览器行为：点滑块以外的轨道朝该方向翻一页。Avalonia 12 的 Track 只把翻页钮按期望尺寸居中摆放，
/// 铺不满轨道区域，故在 ScrollBar 层接指针按压自己翻页；落在滑块上的按压不拦截，拖拽仍归 Thumb。
/// </summary>
public static class ScrollTrackPaging
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollBar, bool>("IsEnabled", typeof(ScrollTrackPaging));

    public static bool GetIsEnabled(ScrollBar bar) => bar.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ScrollBar bar, bool value) => bar.SetValue(IsEnabledProperty, value);

    static ScrollTrackPaging()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollBar>((bar, e) =>
        {
            if (e.NewValue is true) bar.AddHandler(InputElement.PointerPressedEvent, OnPressed);
            else bar.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        });
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ScrollBar bar || !e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed) return;
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
        var scroll = bar.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (thumb is null || scroll is null) return;
        var vertical = bar.Orientation == Orientation.Vertical;
        var point = e.GetPosition(bar);
        var along = vertical ? point.Y : point.X;
        var start = vertical ? thumb.Bounds.Y : thumb.Bounds.X;
        var end = vertical ? thumb.Bounds.Bottom : thumb.Bounds.Right;
        if (along >= start && along <= end) return;
        var page = vertical ? scroll.Viewport.Height : scroll.Viewport.Width;
        var delta = along < start ? -page : page;
        scroll.Offset = vertical
            ? new Vector(scroll.Offset.X, scroll.Offset.Y + delta)
            : new Vector(scroll.Offset.X + delta, scroll.Offset.Y);
        e.Handled = true;
    }
}