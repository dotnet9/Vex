using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Vex.Modules.Templates.ViewModels;

namespace Vex.Modules.Templates.Views;

public partial class TemplateCenterOverlayView : UserControl
{
    public TemplateCenterOverlayView() => InitializeComponent();

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // 分组 / Tag 单行溢出时，鼠标滚轮直接横向滚动（对齐原型交互）。
        AttachWheelToHorizontal(GroupsList);
        AttachWheelToHorizontal(TagsList);
    }

    private static void AttachWheelToHorizontal(ListBox list)
    {
        list.PointerWheelChanged += (_, e) =>
        {
            var scrollViewer = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer is null || scrollViewer.Extent.Width <= scrollViewer.Viewport.Width) return;
            var max = scrollViewer.Extent.Width - scrollViewer.Viewport.Width;
            if ((e.Delta.Y > 0 && scrollViewer.Offset.X >= max) || (e.Delta.Y < 0 && scrollViewer.Offset.X <= 0)) return;
            e.Handled = true;
            scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Offset.X + e.Delta.Y, scrollViewer.Offset.Y);
        };
    }

    private void Close_OnClick(object? sender, RoutedEventArgs args) => (DataContext as TemplateCenterViewModel)?.Close();
}
