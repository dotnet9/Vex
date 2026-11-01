using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ursa.Controls;

namespace Vex.Controls;

internal static class OverlayDialogLayout
{
    // Ursa 先按旧内容尺寸定位；等新尺寸完成布局后再居中。
    public static void CenterAfterLayout(Control content) => Dispatcher.UIThread.Post(() =>
    {
        if (content.GetVisualAncestors().OfType<DialogControlBase>().FirstOrDefault() is not { } dialog
            || dialog.Parent is not OverlayDialogHost host) return;
        var safe = host.SafePadding;
        Canvas.SetLeft(dialog, safe.Left + Math.Max(0, (host.Bounds.Width - safe.Left - safe.Right - dialog.Bounds.Width) / 2));
        Canvas.SetTop(dialog, safe.Top + Math.Max(0, (host.Bounds.Height - safe.Top - safe.Bottom - dialog.Bounds.Height) / 2));
    }, DispatcherPriority.Loaded);
}
