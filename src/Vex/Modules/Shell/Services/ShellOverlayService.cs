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
/// 设置、确认使用窗内浮层；检查更新是非模态小窗（可最小化，下载转后台）；内置文档交给独立工作台进程打开。
/// </summary>
public sealed class ShellOverlayService(IUpdateService updates, IAppLocalizer localizer, IDocumentWindowLauncher windows, IShellToastPublisher toasts) : IShellOverlayService, IDisposable
{
    public Task<DocumentReviewChoice> ShowDocumentReviewAsync(ShellDocumentReviewModel model)
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is not null) { model.MaximumWidth = Math.Max(300, window.Bounds.Width - 48); model.MaximumHeight = Math.Max(250, window.Bounds.Height - 100); }
        return OverlayDialog.ShowCustomAsync<DocumentReviewChoice>(new ShellDocumentReviewView(), model, options: CreateOptions());
    }
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



    public async Task ShowAboutAsync()
    {
        using var model = new ShellAboutViewModel(localizer, OpenUrl);
        await ShowAsync(new ShellAboutOverlayView(), model);
        if (model.WantsUpdates) await ShowUpdatesAsync();
        else if (model.WantsChangelog)
        {
            windows.Open(BundledDocuments.GetPath("更新日志.md"));
        }
    }

    private ShellUpdateViewModel? _updateModel;
    private ShellUpdateWindow? _updateWindow;

    /// <summary>更新会话的单一数据源：药丸、气泡、更新窗与「关于」入口共用这一个 ViewModel；
    /// 关闭更新窗不销毁会话，再次打开继续显示下载进度和已校验的安装包。</summary>
    public ShellUpdateViewModel UpdateSession => _updateModel ??= CreateUpdateSession();

    /// <summary>当前更新窗：关闭后置空（下载中关窗只隐藏，实例仍持有）。</summary>
    public ShellUpdateWindow? UpdateWindow => _updateWindow;

    private ShellUpdateViewModel CreateUpdateSession()
    {
        var model = new ShellUpdateViewModel(updates, localizer, OpenUrl,
            () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.TryShutdown(), toasts);
        // 药丸/气泡请求打开更新窗；「完整日志」打开内置《更新日志》文档（与关于弹窗同一条通道）。
        model.RestoreWindowRequested += (_, _) => _ = ShowUpdatesAsync();
        model.OpenChangelogRequested += (_, _) =>
        {
            try { windows.Open(BundledDocuments.GetPath("更新日志.md")); }
            catch { /* 文档缺失不打扰主界面 */ }
        };
        return model;
    }

    public void StartUpdateWatch() => _ = UpdateSession.RunStartupCheckAsync();

    // 非模态小窗：Show 后立即返回，窗体关闭只是收起（下载中拦截为隐藏），会话继续在后台跑。
    // 窗体实例按次创建（关闭即作废），会话 ViewModel 才是长生命周期的那一个。
    public Task ShowUpdatesAsync()
    {
        var model = UpdateSession;
        if (_updateWindow is null)
        {
            _updateWindow = new ShellUpdateWindow(model, localizer, toasts);
            _updateWindow.Closed += (_, _) => _updateWindow = null;
        }
        if (_updateWindow.Owner is null)
        {
            var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner is not null) _updateWindow.AttachOwner(owner);
        }
        if (_updateWindow.WindowState == WindowState.Minimized) _updateWindow.WindowState = WindowState.Normal;
        _updateWindow.Show();
        _updateWindow.Activate();
        _ = model.EnsureCheckedAsync();
        return Task.CompletedTask;
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    public void Dispose()
    {
        _updateWindow?.Close();
        _updateModel?.Dispose();
    }

    public Task ShowPreferencesAsync(ShellPreferencesViewModel viewModel, string category = "general") =>
        ShowAsync(new ShellPreferencesOverlayView(viewModel, localizer, category), viewModel);

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
