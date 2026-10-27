using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Vex.Core.Models;

namespace Vex.Modules.Shell.Views;

public partial class ShellOutlineView : UserControl
{
    public ShellOutlineView()
    {
        InitializeComponent();
    }

    public ShellOutlineView(ViewModels.ShellOutlineViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void ToggleSection(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is OutlineItem item) item.IsExpanded = !item.IsExpanded;
        e.Handled = true;
    }

    private void OutlineKeyDown(object? sender, KeyEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is not OutlineItem { HasChildren: true } item) return;
        if (e.Key is not (Key.Left or Key.Right)) return;
        item.IsExpanded = e.Key == Key.Right;
        e.Handled = true;
    }
}
