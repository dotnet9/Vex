namespace Vex.Core.Models;

/// <summary>携带会话代次和目录修订的只读快照。</summary>
public sealed record WorkspaceDirectorySnapshot(
    string RootPath,
    long Generation,
    string Path,
    long Revision,
    IReadOnlyList<WorkspaceDirectoryEntry> Entries);
