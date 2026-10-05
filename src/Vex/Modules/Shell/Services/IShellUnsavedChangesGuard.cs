namespace Vex.Modules.Shell.Services;

public interface IShellUnsavedChangesGuard
{
    /// <summary>
    /// 未保存时弹出 Ursa 三按钮确认（保存/不保存/取消），按选择推进或终止流程。
    /// saveAsync 与 isStillModified 供"保存并继续"分支使用。
    /// </summary>
    Task RunAsync(
        string title,
        string message,
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null,
        Func<Task>? saveAsync = null,
        Func<bool>? isStillModified = null);
}
