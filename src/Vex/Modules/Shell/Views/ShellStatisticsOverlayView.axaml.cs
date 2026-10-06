using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.Views;

public partial class ShellStatisticsOverlayView : UserControl
{
    public ShellStatisticsOverlayView()
    {
        InitializeComponent();
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) => ShellOverlayHost.Dismiss(this);
}
