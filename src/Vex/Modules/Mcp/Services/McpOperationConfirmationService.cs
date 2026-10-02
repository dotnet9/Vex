using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Vex.Core.Services;
using Vex.Modules.Mcp.Views;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Mcp.Services;

public sealed class McpOperationConfirmationService : IMcpOperationConfirmationService
{
    private readonly Lock _syncRoot = new();
    private readonly HashSet<string> _rememberedAllowed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rememberedRejected = new(StringComparer.Ordinal);
    private readonly IAppLocalizer _localizer;
    private readonly IShellStatusPublisher _statusPublisher;

    public McpOperationConfirmationService(IAppLocalizer localizer, IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
        _statusPublisher = statusPublisher;
    }

    public async Task<bool> ConfirmAsync(string toolName, string target, string summary)
    {
        lock (_syncRoot)
        {
            if (_rememberedAllowed.Contains(toolName))
            {
                return true;
            }

            if (_rememberedRejected.Contains(toolName))
            {
                _statusPublisher.PublishResource(VexL.McpOperationRejected);
                return false;
            }
        }

        var owner = GetMainWindow();
        var window = new McpOperationConfirmationWindow(
            _localizer.Get(VexL.McpOperationConfirmationTitle),
            _localizer.Get(VexL.McpOperationConfirmationMessage),
            toolName,
            target,
            summary,
            _localizer.Get(VexL.Cancel),
            _localizer.Get(VexL.McpOperationConfirm),
            _localizer.Get(VexL.McpOperationRememberChoice));

        if (owner is null)
        {
            window.Show();
            _statusPublisher.PublishResource(VexL.McpOperationRejected);
            return false;
        }

        await window.ShowDialog(owner);
        var confirmed = window.Confirmed;
        if (window.RememberChoice)
        {
            lock (_syncRoot)
            {
                var set = confirmed ? _rememberedAllowed : _rememberedRejected;
                set.Add(toolName);
            }
        }

        if (!confirmed)
        {
            _statusPublisher.PublishResource(VexL.McpOperationRejected);
        }

        return confirmed;
    }

    public void ResetRememberedChoices()
    {
        lock (_syncRoot)
        {
            _rememberedAllowed.Clear();
            _rememberedRejected.Clear();
        }
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
