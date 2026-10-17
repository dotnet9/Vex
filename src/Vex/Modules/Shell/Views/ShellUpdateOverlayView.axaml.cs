using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellUpdateOverlayView : UserControl
{
    public ShellUpdateOverlayView() => InitializeComponent();
    private void Close_OnClick(object? sender, RoutedEventArgs args) => (DataContext as ShellUpdateViewModel)?.Close();
}
