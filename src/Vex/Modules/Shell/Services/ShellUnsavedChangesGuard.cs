using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

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
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null,
        Func<Task>? saveAsync = null,
        Func<bool>? isStillModified = null,
        Func<Task<bool>>? keepDraftAsync = null,
        ShellUnsavedFlow flow = ShellUnsavedFlow.Switch)
    {
        if (!isModified)
        {
            await continuation();
            return;
        }

        // 这里只负责决策分发，不直接执行文件操作；保存与后续动作由调用方闭包描述。
        // 对话框严格按系统保存框参考图：标题「保存」+ 两行内容 + 三按钮（保存·主 / 丢弃 靠左，取消 靠右）；保留草稿仍作为可编程决策保留。
        var decision = await ShowUnsavedDialogAsync(flow);
        switch (decision)
        {
            case ShellUnsavedDecision.KeepDraft:
                if (keepDraftAsync is not null && await keepDraftAsync()) await continuation();
                break;
            case ShellUnsavedDecision.Save:
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
            case ShellUnsavedDecision.Discard:
                await continuation();
                break;
            default:
                cancellation?.Invoke();
                _statusPublisher.PublishResource(VexL.StatusActionCanceledUnsavedKept);
                break;
        }
    }

    private static Task<ShellUnsavedDecision> ShowUnsavedDialogAsync(ShellUnsavedFlow flow)
    {
        var viewModel = new ShellUnsavedDialogViewModel(flow);
        return OverlayDialog.ShowCustomAsync<ShellUnsavedDecision>(
            new ShellUnsavedDialogView(),
            viewModel,
            options: new Ursa.Controls.OverlayDialogOptions { CanDragMove = false, IsCloseButtonVisible = false });
    }
}
