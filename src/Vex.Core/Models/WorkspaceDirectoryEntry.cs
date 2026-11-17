namespace Vex.Core.Models;

/// <summary>单层目录中的轻量条目；目录不预先枚举子级。</summary>
public sealed record WorkspaceDirectoryEntry(string Path, string Name, bool IsDirectory, DocumentFile? DocumentFile);
