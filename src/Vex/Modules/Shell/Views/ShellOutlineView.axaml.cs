using Avalonia.Controls;
using Avalonia.Input;
using Vex.Core.Models;

using Microsoft.Extensions.DependencyInjection;
using Vex.Modules.Shell.ViewModels;
namespace Vex.Modules.Shell.Views;

public partial class ShellOutlineView : UserControl
{
    private void ClosePanelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow window || window.DataContext is not MainWindowViewModel shell) return;
        shell.Layout.ToggleOutline();
        window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("RailOutlineToggle")?.Focus();
    }

    public ShellOutlineView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<ShellOutlineViewModel>();
    }

    private void OutlineKeyDown(object? sender, KeyEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is not OutlineItem { HasChildren: true } item) return;
        if (e.Key is not (Key.Left or Key.Right)) return;
        item.IsExpanded = e.Key == Key.Right;
        e.Handled = true;
    }
}
