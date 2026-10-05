using System.Runtime.CompilerServices;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

public enum ShellUnsavedDecision
{
    /// <summary>保存后继续。</summary>
    Save,

    /// <summary>放弃修改并继续。</summary>
    Discard,

    /// <summary>取消本次流转。</summary>
    Cancel
}

/// <summary>
/// 未保存确认自定义对话框（Ursa OverlayDialog）的 UI 状态。
/// RequestClose 的参数即决策；右上角关闭/Esc 视为 Cancel。
/// </summary>
public sealed class ShellUnsavedDialogViewModel : ReactiveObject, IDialogContext
{
    private ShellUnsavedDecision? _decision;

    public ShellUnsavedDialogViewModel(string title, string message, string path)
    {
        Title = title;
        Message = message;
        Path = path;
    }

    public string Title { get; }

    public string Message { get; }

    public string Path { get; }

    public ShellUnsavedDecision Decision => _decision ?? ShellUnsavedDecision.Cancel;

    public event EventHandler<object?>? RequestClose;

    public void Save() => Complete(ShellUnsavedDecision.Save);

    public void Discard() => Complete(ShellUnsavedDecision.Discard);

    public void Cancel() => Complete(ShellUnsavedDecision.Cancel);

    public void Close() => Cancel();

    private void Complete(ShellUnsavedDecision decision)
    {
        if (_decision is not null)
        {
            return;
        }

        _decision = decision;
        RequestClose?.Invoke(this, decision);
    }
}
