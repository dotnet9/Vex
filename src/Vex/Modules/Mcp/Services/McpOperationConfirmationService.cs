using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Vex.Core.Services;
using Vex.Modules.Mcp.Views;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Mcp.Services;

public sealed class McpOperationConfirmationService : IMcpOperationConfirmationService
{
    private readonly IAppLocalizer _localizer;
    private readonly IShellStatusPublisher _statusPublisher;

    public McpOperationConfirmationService(IAppLocalizer localizer, IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
        _statusPublisher = statusPublisher;
    }

    public async Task<bool> ConfirmAsync(string toolName, string target, string summary)
    {
        var owner = GetMainWindow();
        var window = new McpOperationConfirmationWindow(
            _localizer.Get(VexL.McpOperationConfirmationTitle),
            _localizer.Get(VexL.McpOperationConfirmationMessage),
            toolName,
            target,
            summary,
            _localizer.Get(VexL.Cancel),
            _localizer.Get(VexL.McpOperationConfirm));

        if (owner is null)
        {
            window.Show();
            _statusPublisher.PublishResource(VexL.McpOperationRejected);
            return false;
        }

        var confirmed = await window.ShowDialog<bool>(owner);
        if (!confirmed)
        {
            _statusPublisher.PublishResource(VexL.McpOperationRejected);
        }

        return confirmed;
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
