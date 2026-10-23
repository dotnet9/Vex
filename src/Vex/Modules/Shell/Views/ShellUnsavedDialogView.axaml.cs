using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellUnsavedDialogView : UserControl
{
    public ShellUnsavedDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyFlow();
    }

    /// <summary>按流程切换文案与按钮组的可见性（文案全部走 i18n 标记绑定）。</summary>
    private void ApplyFlow()
    {
        if (DataContext is not ShellUnsavedDialogViewModel model) return;
        var exit = model.Flow == ShellUnsavedFlow.Exit;
        ExitIcon.IsVisible = exit; SwitchIcon.IsVisible = !exit;
        ExitTitle.IsVisible = exit; SwitchTitle.IsVisible = !exit;
        ExitHint.IsVisible = exit; SwitchHint.IsVisible = !exit;
        ExitActions.IsVisible = exit; SwitchActions.IsVisible = !exit;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Cancel());

    private void OnDiscardClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Discard());

    private void Complete(Action<ShellUnsavedDialogViewModel> action)
    {
        if (DataContext is ShellUnsavedDialogViewModel viewModel)
        {
            action(viewModel);
        }
    }
}
