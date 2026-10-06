using ReactiveUI;
using Ursa.Controls;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.Views;

namespace Vex.Modules.Shell.ViewModels;

// 集中管理 Shell 浮层交互：错误提示与重命名走 Ursa 浮层体系，删除确认走窗内浮层。
public sealed class ShellDialogsViewModel : ReactiveObject
{
    private readonly IShellStatusPublisher _statusPublisher;
    private readonly IAppLocalizer _localizer;
    private readonly IShellOverlayService _overlay;

    public ShellDialogsViewModel(
        IShellStatusPublisher statusPublisher,
        IAppLocalizer localizer,
        IShellOverlayService overlay)
    {
        _statusPublisher = statusPublisher;
        _localizer = localizer;
        _overlay = overlay;
    }

    public async Task<bool> ShowDeleteConfirmationAsync(string path)
    {
        var confirmationText = path is { Length: > 0 }
            ? _localizer.Format(VexL.DeleteConfirmFileFormat, Path.GetFileName(path))
            : _localizer.Get(VexL.DeleteConfirmCurrentFile);
        var confirmed = await _overlay.ConfirmDeleteAsync(
            confirmationText,
            _localizer.Get(VexL.DeletePermanentWarning),
            path);
        if (!confirmed)
        {
            _statusPublisher.PublishResource(VexL.StatusDeleteCanceled);
        }

        return confirmed;
    }

    /// <summary>
    /// 以 Ursa 自定义对话框收集新文件名；取消或关闭返回 null。
    /// </summary>
    public async Task<string?> RenameFileAsync(string path)
    {
        var viewModel = new ShellRenameDialogViewModel(_localizer, path, global::System.IO.Path.GetFileName(path));
        var newName = await OverlayDialog.ShowCustomAsync<string?>(
            new ShellRenameDialogView(),
            viewModel);
        if (string.IsNullOrWhiteSpace(newName))
        {
            _statusPublisher.PublishResource(VexL.StatusRenameCanceled);
            return null;
        }

        return newName;
    }

    public void ShowError(string messageResourceKey, Exception exception, params object?[] messageArgs)
    {
        var message = messageArgs.Length > 0
            ? _localizer.Format(messageResourceKey, messageArgs)
            : _localizer.Get(messageResourceKey);

        ShowError(_localizer.Get(VexL.ErrorTitle), message, ResolveErrorDetail(exception));
    }

    public void ShowError(string title, string message, string detail)
    {
        _statusPublisher.PublishResource(VexL.StatusErrorPanelShown);
        _ = ShowErrorDialogAsync(title, message, detail);
    }

    // 错误对话框本身已经是兜底 UI，展示失败时不能再向上抛，避免打断宿主流程。
    private async Task ShowErrorDialogAsync(string title, string message, string detail)
    {
        try
        {
            await OverlayMessageBox.ShowAsync(
                string.IsNullOrWhiteSpace(detail) ? message : $"{message}\n\n{detail}",
                title,
                icon: MessageBoxIcon.Error,
                button: MessageBoxButton.OK);
        }
        catch (Exception)
        {
            // 对话框宿主不可用时静默放弃，状态栏提示仍已发出。
        }
    }

    private string ResolveErrorDetail(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? _localizer.Get(VexL.ErrorDetailFallback)
            : exception.Message;
    }


}
