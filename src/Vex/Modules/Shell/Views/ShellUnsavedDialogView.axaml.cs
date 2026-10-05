using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellUnsavedDialogView : UserControl
{
    public ShellUnsavedDialogView()
    {
        InitializeComponent();
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Save());

    private void OnDiscardClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Discard());

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Cancel());

    private void Complete(Action<ShellUnsavedDialogViewModel> action)
    {
        if (DataContext is ShellUnsavedDialogViewModel viewModel)
        {
            action(viewModel);
        }
    }
}
