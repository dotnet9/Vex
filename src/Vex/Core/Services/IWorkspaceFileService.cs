using Vex.Core.Models;

namespace Vex.Core.Services;

/// <summary>管理单个工作区的目录缓存、按需路径索引和摘要。</summary>
public interface IWorkspaceFileService : IDisposable
{
    string? RootPath { get; }

    long Generation { get; }

    event EventHandler<WorkspaceFilesChangedEventArgs>? Changed;

    void Open(string rootPath);

    void Close();

    void Refresh();

    Task<WorkspaceDirectorySnapshot> GetDirectoryAsync(string path, bool refresh = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentFile>> GetFilesAsync(CancellationToken cancellationToken = default);

    Task<string> GetPreviewAsync(string path, CancellationToken cancellationToken = default);

    bool IsCurrent(WorkspaceDirectorySnapshot snapshot);
}
