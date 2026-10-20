using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vex.Modules.Shell.Views;

public partial class ShellWorkbenchToolbarView : UserControl
{
    public event EventHandler? BeginGuideRequested;
    public Control ViewTarget => LayoutButton;
    public Control LayoutTarget => LayoutButton;
    public ShellWorkbenchToolbarView() { InitializeComponent(); }
    private void QuickStartClick(object? sender, RoutedEventArgs e) => BeginGuideRequested?.Invoke(this, EventArgs.Empty);
}
