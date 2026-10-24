using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public interface IShellUnsavedChangesGuard
{
    /// <summary>
    /// 未保存时弹出系统保存框样式的三按钮确认（保存·主 / 丢弃 靠左，取消 靠右；切换流的丢弃标签为「放弃更改」），
    /// 按选择推进或终止流程。saveAsync 与 isStillModified 供「保存并继续」分支使用。
    /// </summary>
    Task RunAsync(
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null,
        Func<Task>? saveAsync = null,
        Func<bool>? isStillModified = null,
        Func<Task<bool>>? keepDraftAsync = null,
        ShellUnsavedFlow flow = ShellUnsavedFlow.Switch);
}
