using Avalonia.Controls;
using Avalonia.Input;

using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellFindBarView : UserControl
{
    public ShellFindBarView()
    {
        InitializeComponent();
    }

    // 原型 workbench.js 的 find-input：Enter 下一个、Shift+Enter 上一个。
    private void OnFindTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ShellFindBarViewModel model)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            model.FindPrev();
        }
        else
        {
            model.FindNext();
        }

        e.Handled = true;
    }
}
