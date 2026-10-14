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
/// 重复打开同一浮层时不额外去重：Ursa 的模态浮层栈本身屏蔽下层交互，
/// 上层关闭后底层仍在，符合「窗内浮层」语义。
/// </summary>
public sealed class ShellOverlayService(IUpdateService updates, IAppLocalizer localizer) : IShellOverlayService
{
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
        using var model = new ShellAboutViewModel(updates, localizer,
            url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }),
            () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
        await ShowAsync(new ShellAboutOverlayView(), model);
    }

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
