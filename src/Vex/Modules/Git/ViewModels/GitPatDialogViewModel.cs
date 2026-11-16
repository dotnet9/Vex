using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Git.ViewModels;

/// <summary>
/// HTTPS 远程的访问令牌输入弹层（Vex 实现新增，原型无此层——演示态不需要真实凭据）。
/// 返回打包答案（token + remember），由 ShellGitViewModel.DecodePatAnswer 拆解。
/// </summary>
public sealed class GitPatDialogViewModel : ReactiveObject, IDialogContext
{
    private string _token = string.Empty;
    private bool _remember = true;

    public event EventHandler<object?>? RequestClose;

    public string Token { get => _token; set => this.RaiseAndSetIfChanged(ref _token, value); }

    /// <summary>保存到本机凭据管理器（默认勾选）。</summary>
    public bool Remember { get => _remember; set => this.RaiseAndSetIfChanged(ref _remember, value); }

    public bool CanSave => !string.IsNullOrWhiteSpace(Token);

    public void Save()
    {
        if (!CanSave) return;
        RequestClose?.Invoke(this, ShellGitViewModel.EncodePatAnswer(Token.Trim(), Remember));
    }

    public void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => Cancel();
}
