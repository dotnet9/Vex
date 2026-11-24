using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Git.ViewModels;

/// <summary>删除分支确认弹层（GitHub Desktop delete-branch-dialog 口径）：确认返回 true。</summary>
public sealed class GitBranchDeleteConfirmViewModel : ReactiveObject, IDialogContext
{
    private readonly Func<string, string> _localize;

    public GitBranchDeleteConfirmViewModel(string branchName, Func<string, string> localize)
    {
        _localize = localize;
        BranchName = branchName;
    }

    public event EventHandler<object?>? RequestClose;

    public string BranchName { get; }

    /// <summary>确认文案：删除分支 {0}？</summary>
    public string Message => string.Format(_localize(Vex.VexL.GitBranchDeleteConfirmFormat), BranchName);

    public void Confirm() => RequestClose?.Invoke(this, true);

    public void Cancel() => RequestClose?.Invoke(this, false);

    public void Close() => Cancel();
}
