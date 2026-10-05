using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public sealed class ShellUnsavedChangesGuard : IShellUnsavedChangesGuard
{
    private readonly ShellDialogsViewModel _dialogs;
    private readonly IShellDocumentWorkflowText _text;
    private readonly IShellStatusPublisher _statusPublisher;

    public ShellUnsavedChangesGuard(
        ShellDialogsViewModel dialogs,
        IShellDocumentWorkflowText text,
        IShellStatusPublisher statusPublisher)
    {
        _dialogs = dialogs;
        _text = text;
        _statusPublisher = statusPublisher;
    }

    public async Task RunAsync(
        string title,
        string message,
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null,
        Func<Task>? saveAsync = null,
        Func<bool>? isStillModified = null)
    {
        if (!isModified)
        {
            await continuation();
            return;
        }

        // 这里只负责决策分发，不直接执行文件操作；保存与后续动作由调用方闭包描述。
        var result = await OverlayMessageBox.ShowAsync(
            message,
            title,
            icon: MessageBoxIcon.Warning,
            button: MessageBoxButton.YesNoCancel);
        switch (result)
        {
            case MessageBoxResult.Yes:
                if (saveAsync is not null)
                {
                    await saveAsync();
                    if (isStillModified?.Invoke() == true)
                    {
                        _text.PublishSaveCanceledActionIncomplete();
                        return;
                    }
                }

                await continuation();
                break;
            case MessageBoxResult.No:
                await continuation();
                break;
            default:
                cancellation?.Invoke();
                _statusPublisher.PublishResource(VexL.StatusActionCanceledUnsavedKept);
                break;
        }
    }
}
