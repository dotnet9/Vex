using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Git.ViewModels;

/// <summary>添加/修改远程弹层（原型 layer-git-remote）：HTTPS 或 SSH 形态校验，保存返回地址，取消返回 null。</summary>
public sealed class GitRemoteDialogViewModel : ReactiveObject, IDialogContext
{
    private readonly Func<string, string> _localize;
    private string _url = string.Empty;
    private string _errorText = string.Empty;

    public GitRemoteDialogViewModel(string? currentUrl, Func<string, string> localize)
    {
        _localize = localize;
        _url = currentUrl ?? string.Empty;
    }

    public event EventHandler<object?>? RequestClose;

    /// <summary>仓库地址（原型占位 https://github.com/dotnet9/Vex.git）。</summary>
    public string Url { get => _url; set { this.RaiseAndSetIfChanged(ref _url, value); ErrorText = string.Empty; } }

    public bool HasError => ErrorText.Length > 0;

    public string ErrorText { get => _errorText; private set { this.RaiseAndSetIfChanged(ref _errorText, value); this.RaisePropertyChanged(nameof(HasError)); } }

    /// <summary>保存远程：校验失败置错误文案（原型 #git-remote-error）。</summary>
    public void Save()
    {
        var value = Url.Trim();
        if (!ShellGitViewModel.IsValidRemoteUrl(value))
        {
            ErrorText = _localize(Vex.VexL.GitRemoteUrlError);
            return;
        }
        RequestClose?.Invoke(this, value);
    }

    public void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => Cancel();
}
