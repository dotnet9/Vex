using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace Vex.Modules.Shell.Services;

/// <summary>
/// 浮层关闭辅助：浮层视图的「关闭」按钮通过它关闭自身，
/// 优先走 <see cref="IDialogContext.Close"/>，否则回退到 Ursa 对话框控件。
/// </summary>
public static class ShellOverlayHost
{
    public static void Dismiss(Control? control)
    {
        if (control?.DataContext is IDialogContext context)
        {
            context.Close();
            return;
        }

        for (Visual? current = control; current is not null; current = current.GetVisualParent())
        {
            if (current is DialogControlBase dialog)
            {
                dialog.Close();
                return;
            }
        }
    }
}
