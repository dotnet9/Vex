using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class McpSettingsWindow : UrsaWindow
{
    public McpSettingsWindow()
    {
        InitializeComponent();
    }

    public McpSettingsWindow(McpSettingsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
