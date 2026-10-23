using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public interface IShellUnsavedChangesGuard
{
    /// <summary>
    /// 未保存时弹出确认（退出流：继续编辑/放弃修改并退出；切换流：取消/不保存直接切换/保存并切换），
    /// 按选择推进或终止流程。saveAsync 与 isStillModified 供「保存并继续」分支使用。
    /// </summary>
    Task RunAsync(
        string title,
        string message,
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null,
        Func<Task>? saveAsync = null,
        Func<bool>? isStillModified = null,
        Func<Task<bool>>? keepDraftAsync = null,
        ShellUnsavedFlow flow = ShellUnsavedFlow.Switch);
}
