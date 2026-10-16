using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vex.Modules.Shell.Views;

public partial class ShellTitleMenuView : UserControl
{
    public ShellTitleMenuView()
    {
        InitializeComponent();
    }

    public event EventHandler? BeginGuideRequested;

    public MenuItem FileMenuTarget => FileMenuItem;

    private void BeginGuideMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginGuideRequested?.Invoke(this, EventArgs.Empty);
    }
}
