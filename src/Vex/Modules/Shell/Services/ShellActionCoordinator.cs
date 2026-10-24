using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Mcp.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

namespace Vex.Modules.Shell.Services;

public sealed class ShellActionCoordinator
{
    private readonly MainWindowViewModel _shell;
    private readonly ShellPreferencesViewModel _preferences;
    private readonly McpSettingsViewModel _mcpSettings;
    private readonly IShellOverlayService _overlay;
    private readonly IMcpOperationAuditService _auditService;
    private readonly IAppLocalizer _localizer;

    public ShellActionCoordinator(
        MainWindowViewModel shell,
        ShellPreferencesViewModel preferences,
        McpSettingsViewModel mcpSettings,
        IMcpOperationAuditService auditService,
        IAppLocalizer localizer,
        IShellOverlayService overlay)
    {
        _shell = shell;
        _preferences = preferences;
        _mcpSettings = mcpSettings;
        _auditService = auditService;
        _localizer = localizer;
        _overlay = overlay;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
    }

    [EventHandler]
    public void ApplyShellAction(ShellActionCommand command)
    {
        // 菜单控件只发布用户意图，这里统一转回 Shell 文档流程，避免 View 直接耦合主窗口。
        _ = HandleShellActionAsync(command);
    }

    private async Task HandleShellActionAsync(ShellActionCommand command)
    {
        try
        {
            if (_shell.Workbench.IsGuiding && (_shell.Workbench.GuideStep != 5
                || command.Action is not (ShellActionKind.Save or ShellActionKind.SaveAs or ShellActionKind.Export or ShellActionKind.CopyHtml))) return;
            if (command.Action is ShellActionKind.NewDocument or ShellActionKind.Open or ShellActionKind.OpenFolder
                or ShellActionKind.QuickOpen or ShellActionKind.OpenRecentDocument or ShellActionKind.ReopenWithEncoding
                or ShellActionKind.CloseDocument or ShellActionKind.Delete)
                _shell.Workbench.ExitGuide();
            switch (command.Action)
            {
                case ShellActionKind.NewDocument:
                    await _shell.NewDocument();
                    break;
                case ShellActionKind.NewWindow:
                    Process.Start(ShellProcessLauncher.CreateStartInfo(command.Parameter));
                    break;
                case ShellActionKind.RefreshFiles:
                    await _shell.RefreshFilesAsync();
                    break;
                case ShellActionKind.OpenPathLocation:
                    if (command.Parameter is { Length: > 0 } location) await _shell.OpenPathLocationAsync(location);
                    break;
                case ShellActionKind.Open:
                    await _shell.OpenAsync();
                    break;
                case ShellActionKind.Import:
                    await _shell.ImportAsync(command.Parameter);
                    break;
                case ShellActionKind.OpenFolder:
                    await _shell.OpenFolderAsync();
                    break;
                case ShellActionKind.QuickOpen:
                    await ShowQuickOpenAsync();
                    break;
                case ShellActionKind.OpenRecentDocument:
                    await OpenRecentDocumentAsync(command.Parameter);
                    break;
                case ShellActionKind.ReopenWithEncoding:
                    await _shell.ReopenWithEncodingAsync(command.Parameter);
                    break;
                case ShellActionKind.Save:
                    await _shell.SaveAsync();
                    break;
                case ShellActionKind.SaveAs:
                    await _shell.SaveAsAsync();
                    break;
                case ShellActionKind.SaveAll:
                    await _shell.SaveAllAsync();
                    break;
                case ShellActionKind.ShowProperties:
                    _shell.ShowProperties();
                    break;
                case ShellActionKind.OpenFileLocation:
                    await _shell.OpenFileLocationAsync();
                    break;
                case ShellActionKind.Delete:
                    await _shell.DeleteAsync();
                    break;
                case ShellActionKind.Export:
                    await _shell.Export(command.Parameter);
                    break;
                case ShellActionKind.CopyHtml:
                    await _shell.CopyHtml(command.Parameter);
                    break;
                case ShellActionKind.Print:
                    await _shell.Print();
                    break;
                case ShellActionKind.CloseDocument:
                    await _shell.CloseDocument();
                    break;
                case ShellActionKind.RefreshPreview:
                    _shell.RefreshPreview();
                    break;
                case ShellActionKind.ShowFindPanel:
                    _shell.ShowFindPanel();
                    break;
                case ShellActionKind.ShowReplacePanel:
                    _shell.ShowReplacePanel();
                    break;
                case ShellActionKind.WordCount:
                    _shell.WordCount();
                    break;
                case ShellActionKind.ShowPreferences:
                    await _overlay.ShowPreferencesAsync(_preferences, command.Parameter ?? "general");
                    break;
                case ShellActionKind.ShowMcpSettings:
                    ShowMcpSettings();
                    break;
                case ShellActionKind.ShowMcpAudit:
                    ShowMcpAudit();
                    break;
            }
        }
        catch (Exception exception)
        {
            var messageKey = command.Action == ShellActionKind.NewWindow
                ? VexL.ErrorMessageCannotStartNewWindow
                : VexL.ErrorMessageActionFailed;
            _shell.Dialogs.ShowError(messageKey, exception);
        }
    }

    private Task OpenRecentDocumentAsync(string? indexText)
    {
        return int.TryParse(indexText, out var index)
            ? _shell.OpenRecentDocumentAsync(index)
            : Task.CompletedTask;
    }

    private async Task ShowQuickOpenAsync()
    {
        var viewModel = new ShellQuickOpenViewModel(
            _shell.BuildQuickOpenItems(),
            _localizer.Get(VexL.QuickOpen)) { IsLoading = true };
        using var cancellation = new CancellationTokenSource();
        var populate = PopulateQuickOpenAsync(viewModel, cancellation.Token);
        var path = await _overlay.ShowQuickOpenAsync(viewModel);
        cancellation.Cancel();
        await populate;
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _shell.OpenStartupDocumentAsync([path]);
        }
    }

    private async Task PopulateQuickOpenAsync(ShellQuickOpenViewModel viewModel, CancellationToken cancellationToken)
    {
        try { viewModel.UpdateItems(await _shell.BuildQuickOpenItemsAsync(cancellationToken)); }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!cancellationToken.IsCancellationRequested) viewModel.LoadError = _localizer.Get(VexL.SidebarFilesLoadFailed);
        }
        finally { viewModel.IsLoading = false; }
    }

    private void ShowMcpSettings() => _ = _overlay.ShowPreferencesAsync(_preferences, "mcp");

    private void ShowMcpAudit() => _ = _overlay.ShowMcpAuditAsync(
        _auditService,
        _localizer.Get(VexL.McpAudit),
        _localizer.Get(VexL.McpAuditRefresh),
        _localizer.Get(VexL.McpAuditEmpty));
}
