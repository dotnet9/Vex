using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellRenameDialogView : UserControl
{
    public ShellRenameDialogView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        NameTextBox.Focus();
        if (DataContext is not ShellRenameDialogViewModel viewModel)
        {
            return;
        }

        // 选中文件名主干，扩展名不进选区，直接键入即可整体替换。
        var dot = viewModel.FileName.LastIndexOf('.');
        NameTextBox.SelectionStart = 0;
        NameTextBox.SelectionEnd = dot > 0 ? dot : viewModel.FileName.Length;
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Submit();

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Cancel();

    private void NameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submit();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
    }

    private void Submit()
    {
        if (DataContext is ShellRenameDialogViewModel viewModel)
        {
            viewModel.Confirm();
        }
    }

    private void Cancel()
    {
        if (DataContext is ShellRenameDialogViewModel viewModel)
        {
            viewModel.Cancel();
        }
    }
}
