using Vex.Core.Models;

namespace Vex.Core.Services;

/// <summary>管理工作区的目录缓存、按需路径索引和摘要；FR-15 起支持多文件夹根。</summary>
public interface IWorkspaceFileService : IDisposable
{
    /// <summary>主根（「打开文件夹」的文件夹；附加根见 <see cref="RootPaths"/>）。</summary>
    string? RootPath { get; }

    /// <summary>全部已挂载根：首根 = 主根，其后为「添加文件夹」追加的附加根（按添加顺序）。</summary>
    IReadOnlyList<string> RootPaths { get; }

    long Generation { get; }

    event EventHandler<WorkspaceFilesChangedEventArgs>? Changed;

    /// <summary>打开（或替换）主工作区根。</summary>
    void Open(string rootPath);

    /// <summary>FR-15：追加一个工作区根（不替换、不嵌套；只挂载，不触碰磁盘内容）。</summary>
    void AddRoot(string rootPath);

    /// <summary>FR-15：移除一个附加根（只从工作区摘除，不删除磁盘内容）；主根不可移除。</summary>
    void RemoveRoot(string rootPath);

    void Close();

    void Refresh();

    Task<WorkspaceDirectorySnapshot> GetDirectoryAsync(string path, bool refresh = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentFile>> GetFilesAsync(CancellationToken cancellationToken = default);

    Task<string> GetPreviewAsync(string path, CancellationToken cancellationToken = default);

    bool IsCurrent(WorkspaceDirectorySnapshot snapshot);
}
