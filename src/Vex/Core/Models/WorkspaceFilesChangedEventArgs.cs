namespace Vex.Core.Models;

/// <summary>通知受影响的父目录和需要失效的文件；通知线程由消费者调度。</summary>
public sealed class WorkspaceFilesChangedEventArgs(
    long generation,
    string rootPath,
    IReadOnlyList<string> directoryPaths,
    IReadOnlyList<string> changedPaths,
    bool isRecovery = false,
    Exception? error = null) : EventArgs
{
    public long Generation { get; } = generation;
    public string RootPath { get; } = rootPath;
    public IReadOnlyList<string> DirectoryPaths { get; } = directoryPaths;
    public IReadOnlyList<string> ChangedPaths { get; } = changedPaths;
    public bool IsRecovery { get; } = isRecovery;
    public Exception? Error { get; } = error;
}
