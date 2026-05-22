namespace Vex.Modules.Shell.Services;

public interface IShellUnsavedChangesGuard
{
    Task RunAsync(
        string title,
        string message,
        bool isModified,
        string? currentFilePath,
        Func<Task> continuation,
        Action? cancellation = null);

    Task SavePendingActionAsync(Func<Task> saveAsync, Func<bool> isModified);

    Task DiscardPendingActionAsync();
}
