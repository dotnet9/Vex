using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellAboutOverlayView : UserControl
{
    public ShellAboutOverlayView() => InitializeComponent();
    private void Close_OnClick(object? sender, RoutedEventArgs args) => (DataContext as ShellAboutViewModel)?.Close();
}
