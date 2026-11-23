using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Git.ViewModels;

// GitBranchDialogMode / GitBranchDialogRequest 定义在 ShellGitViewModel.cs（跨弹层共享）。

/// <summary>
/// 新建/重命名分支弹层（GitHub Desktop create-branch-dialog 口径）：名称 + 基准分支
/// （当前分支/默认分支二选一，与默认分支相同时合并为一项）；创建后由调用方检出。
/// </summary>
public sealed class GitBranchDialogViewModel : ReactiveObject, IDialogContext
{
    private readonly Func<string, string> _localize;
    private string _name;
    private string _baseBranch;
    private int _baseIndex;
    private string _errorText = string.Empty;

    public GitBranchDialogViewModel(GitBranchDialogRequest request, Func<string, string> localize)
    {
        _localize = localize;
        Mode = request.Mode;
        CurrentBranch = request.CurrentBranch;
        DefaultBranch = request.DefaultBranch;
        _name = request.InitialName;

        // 基准选项（GHD create-branch：当前分支/默认分支；二者相同则合并为一项）。
        var options = new List<string>();
        _baseValues = new List<string>();
        if (CurrentBranch != DefaultBranch)
        {
            options.Add(_localize(Vex.VexL.GitBranchCurrent) + "（" + CurrentBranch + "）");
            _baseValues.Add(CurrentBranch);
        }
        options.Add(_localize(Vex.VexL.GitBranchGroupDefault) + "（" + DefaultBranch + "）");
        _baseValues.Add(DefaultBranch);
        BaseOptions = options;
        _baseIndex = 0;
        _baseBranch = _baseValues[0];
    }

    public event EventHandler<object?>? RequestClose;

    public GitBranchDialogMode Mode { get; }
    public string CurrentBranch { get; }
    public string DefaultBranch { get; }

    public string Title => Mode == GitBranchDialogMode.Rename
        ? _localize(Vex.VexL.GitBranchRename)
        : _localize(Vex.VexL.GitBranchCreate);

    public string OkText => Mode == GitBranchDialogMode.Rename
        ? _localize(Vex.VexL.GitBranchRename)
        : _localize(Vex.VexL.GitBranchCreate);

    public bool ShowBase => Mode == GitBranchDialogMode.Create;

    /// <summary>基准说明文案（create 模式：新分支将基于所选基准创建）。</summary>
    public string BaseNoteText => _localize(Vex.VexL.GitBranchBaseNote) + " " + BaseBranch;

    public string Name { get => _name; set { this.RaiseAndSetIfChanged(ref _name, value); ErrorText = string.Empty; } }

    public IReadOnlyList<string> BaseOptions { get; }
    private readonly List<string> _baseValues;

    public int BaseIndex
    {
        get => _baseIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _baseIndex, value);
            if (value >= 0 && value < _baseValues.Count) BaseBranch = _baseValues[value];
        }
    }

    public string BaseBranch { get => _baseBranch; private set => this.RaiseAndSetIfChanged(ref _baseBranch, value); }

    public bool HasError => ErrorText.Length > 0;

    public string ErrorText { get => _errorText; private set { this.RaiseAndSetIfChanged(ref _errorText, value); this.RaisePropertyChanged(nameof(HasError)); } }

    /// <summary>校验（GHD/命名规则轻量版）：非空、不以 - 或 . 开头、无空格与 ..、不以 / 或 . 结尾。</summary>
    public void Save()
    {
        var name = Name.Trim();
        if (name.Length == 0 || name.StartsWith('-') || name.StartsWith('.')
            || name.Contains(' ') || name.Contains("..")
            || name.EndsWith('/') || name.EndsWith('.'))
        {
            ErrorText = _localize(Vex.VexL.GitBranchNameInvalid);
            return;
        }
        RequestClose?.Invoke(this, new GitBranchDialogResult(name, BaseBranch));
    }

    public void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => Cancel();
}
