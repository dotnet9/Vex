using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Git.ViewModels;

/// <summary>
/// 合并分支弹层（GitHub Desktop merge-choose-branch-dialog 口径）：标题「合并到 当前分支」，
/// 选择源分支；确认返回源分支名，取消返回 null。
/// </summary>
public sealed class GitMergeDialogViewModel : ReactiveObject, IDialogContext
{
    private readonly Func<string, string> _localize;
    private string _selectedSource;

    public GitMergeDialogViewModel(string currentBranch, IReadOnlyList<string> sourceBranches, Func<string, string> localize)
    {
        _localize = localize;
        CurrentBranch = currentBranch;
        Sources = sourceBranches;
        _selectedSource = sourceBranches.FirstOrDefault() ?? string.Empty;
    }

    public event EventHandler<object?>? RequestClose;

    public string CurrentBranch { get; }

    public IReadOnlyList<string> Sources { get; }

    /// <summary>标题「合并到 当前分支」（原型 git-merge 标题口径）。</summary>
    public string Title => _localize(Vex.VexL.GitMergeInto) + " " + CurrentBranch;

    /// <summary>源分支列表标签。</summary>
    public string SourceLabel => _localize(Vex.VexL.GitMergeChoose);

    /// <summary>说明文案：将把来自 {源} 的提交合并到 {当前分支}。</summary>
    public string NoteText => string.Format(_localize(Vex.VexL.GitMergeNoteFormat), SelectedSource, CurrentBranch);

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (_selectedSource == value) return;
            _selectedSource = value;
            this.RaisePropertyChanged(nameof(SelectedSource));
            this.RaisePropertyChanged(nameof(NoteText));
        }
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(SelectedSource)) return;
        RequestClose?.Invoke(this, SelectedSource);
    }

    public void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => Cancel();
}
