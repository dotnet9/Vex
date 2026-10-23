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
        string title,
        string message,
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
        // 退出流不提供保存按钮（用户可取消后自行保存），切换流提供 保存并切换。
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
            viewModel);
    }
}
