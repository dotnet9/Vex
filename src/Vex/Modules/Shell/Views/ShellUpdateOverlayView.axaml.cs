using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellUpdateOverlayView : UserControl
{
    public ShellUpdateOverlayView() => InitializeComponent();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Ursa 只在宿主变尺寸时重新定位，更新卡片切换阶段后也需要居中。
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindAncestorOfType<CustomDialogControl>() is { Parent: OverlayDialogHost host } dialog)
            {
                var availableHeight = host.Bounds.Height - host.SafePadding.Top - host.SafePadding.Bottom;
                dialog.MaxHeight = Math.Max(0, availableHeight - 40);
                Canvas.SetTop(dialog, host.SafePadding.Top + Math.Max(0, (availableHeight - dialog.Bounds.Height) / 2));
            }
        }, DispatcherPriority.Loaded);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs args) => (DataContext as ShellUpdateViewModel)?.Close();
}
