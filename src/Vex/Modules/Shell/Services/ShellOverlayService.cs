using Avalonia.Controls;
using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Mcp.Services;
using Vex.Core.Services;
using CodeWF.Toolkit.Core.UpdateChecking;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Diagnostics;

namespace Vex.Modules.Shell.Services;

/// <summary>
/// 统一把各功能以宿主窗口内的 Ursa 浮层呈现（不再创建独立窗口）。
/// 更新浮层复用单一会话并去重，其余浮层由 Ursa 的模态栈管理。
/// </summary>
public sealed class ShellOverlayService(IUpdateService updates, IAppLocalizer localizer) : IShellOverlayService, IDisposable
{
    public Task<Vex.Modules.Workspace.Services.TableDimensions?> ShowInsertTableAsync() =>
        OverlayDialog.ShowCustomAsync<Vex.Modules.Workspace.Services.TableDimensions>(
            new ShellInsertTableOverlayView(), new ShellInsertTableModel(), options: CreateOptions());

    /// <summary>信息型浮层尺寸约束（与宿主窗口尺寸联动，超长内容内部滚动）。</summary>
    private static OverlayDialogOptions CreateOptions(bool fullScreen = false) => new()
    {
        FullScreen = fullScreen,
        CanLightDismiss = true,
        CanDragMove = false,
        CanResize = false,
        IsCloseButtonVisible = false
    };

    public async Task<bool> ConfirmDeleteAsync(string confirmationText, string warningText, string deletePath)
    {
        var model = new ShellDeleteConfirmationModel(confirmationText, warningText, deletePath);
        var confirmed = await OverlayDialog.ShowCustomAsync<bool>(
            new ShellDeleteConfirmationOverlayView(),
            model,
            options: CreateOptions());
        return confirmed;
    }

    public Task ShowPropertiesAsync(object dataContext) =>
        ShowAsync(new ShellPropertiesOverlayView(), dataContext);

    public Task ShowStatisticsAsync(object dataContext) =>
        ShowAsync(new ShellStatisticsOverlayView(), dataContext);

    public async Task ShowAboutAsync()
    {
        using var model = new ShellAboutViewModel(localizer, OpenUrl);
        await ShowAsync(new ShellAboutOverlayView(), model);
        if (model.WantsUpdates) await ShowUpdatesAsync();
        else if (model.WantsChangelog)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "docs", "UpdateLog.md");
            if (!File.Exists(path)) path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "docs", "UpdateLog.md"));
            await ShowDocumentAsync(localizer.Get(VexL.Changelog), await File.ReadAllTextAsync(path), path, "Simple", "Normal");
        }
    }

    private ShellUpdateViewModel? _updateModel;
    private Task? _updateDialog;

    // 单例持有更新会话；关闭浮层后再次进入，继续显示下载进度和已验证的安装包。
    public Task ShowUpdatesAsync() => _updateDialog ??= ShowUpdatesCoreAsync();

    private async Task ShowUpdatesCoreAsync()
    {
        _updateModel ??= new ShellUpdateViewModel(updates, localizer, OpenUrl,
            () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.TryShutdown());
        try
        {
            var dialog = ShowAsync(new ShellUpdateOverlayView(), _updateModel);
            _ = _updateModel.EnsureCheckedAsync();
            await dialog;
        }
        finally { _updateDialog = null; }
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    public void Dispose() => _updateModel?.Dispose();

    public Task ShowDocumentAsync(
        string title,
        string markdown,
        string? imageBasePath,
        string? typographyTheme,
        string typographySize) =>
        ShowAsync(
            new MarkdownDocumentOverlayView(title, markdown, imageBasePath, typographyTheme, typographySize),
            null);

    public Task ShowMcpSettingsAsync(McpSettingsViewModel viewModel) =>
        ShowAsync(new McpSettingsOverlayView(), viewModel);

    public Task ShowMcpAuditAsync(IMcpOperationAuditService auditService, string title, string refreshText, string emptyText) =>
        ShowAsync(new McpAuditOverlayView(auditService, title, refreshText, emptyText), null);

    public Task<string?> ShowQuickOpenAsync(ShellQuickOpenViewModel viewModel) =>
        OverlayDialog.ShowCustomAsync<string>(
            new ShellQuickOpenView(),
            viewModel,
            options: CreateOptions());

    private static async Task ShowAsync(Control view, object? viewModel)
    {
        await OverlayDialog.ShowCustomAsync<object>(view, viewModel, options: CreateOptions());
    }
}
