using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public sealed class ShellUnsavedChangesGuard : IShellUnsavedChangesGuard
{
    private readonly ShellDialogsViewModel _dialogs;
    private readonly IShellDocumentWorkflowText _text;

    public ShellUnsavedChangesGuard(ShellDialogsViewModel dialogs, IShellDocumentWorkflowText text)
    {
        _dialogs = dialogs;
        _text = text;
    }

    public async Task RunAsync(
        string title,
        string message,
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null)
    {
        if (!isModified)
        {
            await continuation();
            return;
        }

        // 未保存确认的弹窗状态集中在这里，调用方只描述动作和后续流程。
        _dialogs.ShowUnsavedConfirmation(
            title,
            message,
            currentFilePath ?? _text.UnsavedDocumentFallback,
            continuation,
            cancellation);
    }

    public async Task SavePendingActionAsync(Func<Task> saveAsync, Func<bool> isModified)
    {
        if (!_dialogs.HasPendingUnsavedAction)
        {
            return;
        }

        await saveAsync();
        if (isModified())
        {
            _text.PublishSaveCanceledActionIncomplete();
            return;
        }

        await DiscardPendingActionAsync();
    }

    public async Task DiscardPendingActionAsync()
    {
        var continuation = _dialogs.TakePendingUnsavedContinuation();
        if (continuation is not null)
        {
            await continuation();
        }
    }
}
