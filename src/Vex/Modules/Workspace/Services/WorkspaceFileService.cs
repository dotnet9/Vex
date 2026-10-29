using Vex.Core.Models;
using Vex.Core.Services;
using static Vex.Modules.Workspace.Services.WorkspaceFileMonitor;

namespace Vex.Modules.Workspace.Services;

/// <summary>工作区会话与缓存的唯一所有者；磁盘工作与界面集合更新彼此独立。</summary>
public sealed class WorkspaceFileService(IDocumentFileFactory documentFileFactory) : IWorkspaceFileService
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operations = new(2, 2);
    private Session? _session;
    private long _generation;
    private bool _disposed;
    private long _directoryEnumerationCount;
    private long _indexScanCount;
    private long _previewReadCount;
    private int _activeOperations;
    private int _maximumConcurrentOperations;

    public event EventHandler<WorkspaceFilesChangedEventArgs>? Changed;

    public string? RootPath { get { lock (_gate) return _session?.Root; } }
    public long Generation { get { lock (_gate) return _generation; } }

    public WorkspaceFileServiceDiagnostics Diagnostics => new(
        Interlocked.Read(ref _directoryEnumerationCount), Interlocked.Read(ref _indexScanCount),
        Interlocked.Read(ref _previewReadCount), Volatile.Read(ref _activeOperations),
        Volatile.Read(ref _maximumConcurrentOperations));

    public void Open(string rootPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        Session session;
        Session? previous;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previous = _session;
            session = new Session(root, ++_generation);
            _session = session;
            // 监控先于首个快照挂接；回调通过会话身份拒绝旧通知。
            try
            {
                session.Monitor = new WorkspaceFileMonitor(root,
                    path => IsKnownDirectory(session, path),
                    (changes, recovery, error) => OnMonitorChanged(session, changes, recovery, error));
            }
            catch
            {
                _session = previous;
                _generation--;
                session.Cancellation.Dispose();
                throw;
            }
        }
        Release(previous);
        Publish(session, [root], [], true, null);
    }

    public void Close()
    {
        Session? previous;
        lock (_gate)
        {
            previous = _session;
            _session = null;
            _generation++;
        }
        Release(previous);
    }

    /// <summary>使已加载缓存失效，由界面仅重新读取展开的目录。</summary>
    public void Refresh()
    {
        lock (_gate) RequireSession().Monitor!.ReportError();
    }

    public Task<WorkspaceDirectorySnapshot> GetDirectoryAsync(string path, bool refresh = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<WorkspaceDirectorySnapshot> task;
        lock (_gate)
        {
            var session = RequireSession();
            path = ValidatePath(session, path, directory: true, checkLinks: false);
            if (!session.Directories.TryGetValue(path, out var cache))
            {
                cache = new DirectoryCache();
                session.Directories.Add(path, cache);
            }
            if (refresh && cache.Snapshot is not null) Invalidate(cache);
            if (cache.Snapshot is { } snapshot) return Task.FromResult(snapshot);
            if (cache.Task is null)
            {
                var completion = new TaskCompletionSource<WorkspaceDirectorySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                cache.Task = completion.Task;
                task = completion.Task;
                _ = CompleteDirectoryAsync(session, path, cache, completion);
            }
            else task = cache.Task;
        }
        return task.WaitAsync(cancellationToken);
    }

    private async Task CompleteDirectoryAsync(Session session, string path, DirectoryCache cache,
        TaskCompletionSource<WorkspaceDirectorySnapshot> completion)
    {
        try
        {
            while (true)
            {
                long revision;
                lock (_gate)
                {
                    EnsureCurrent(session);
                    revision = cache.Revision;
                }
                var entries = await RunDiskAsync(session, () =>
                {
                    ValidatePath(session, path, directory: true);
                    Interlocked.Increment(ref _directoryEnumerationCount);
                    var result = new List<WorkspaceDirectoryEntry>();
                    foreach (var entry in WorkspaceFileEnumeration.EnumerateDirectory(path))
                    {
                        session.Token.ThrowIfCancellationRequested();
                        // 单个不可读文件（权限/占用）跳过，不让整目录加载失败。
                        try
                        {
                            result.Add(new WorkspaceDirectoryEntry(entry.Path, Path.GetFileName(entry.Path), entry.IsDirectory,
                                entry.IsDirectory ? null : documentFileFactory.Create(entry.Path, session.Root)));
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                        {
                        }
                    }
                    return result.OrderByDescending(entry => entry.IsDirectory)
                        .ThenBy(entry => entry.Name, WorkspaceFileEnumeration.PathComparer).ToArray();
                }).ConfigureAwait(false);
                lock (_gate)
                {
                    EnsureCurrent(session);
                    if (!session.Directories.TryGetValue(path, out var current) || !ReferenceEquals(cache, current))
                        throw new OperationCanceledException("目录已移除。", session.Token);
                    // 同一目录始终只有一个读取循环；读取期间失效时串行核验最新修订。
                    if (cache.Revision != revision) continue;
                    foreach (var entry in entries.Where(entry => entry.IsDirectory)) session.KnownDirectories.Add(entry.Path);
                    cache.Snapshot = new WorkspaceDirectorySnapshot(session.Root, session.Generation, path, revision, entries);
                    cache.Task = null;
                    completion.TrySetResult(cache.Snapshot);
                    break;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            completion.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(cache.Task, completion.Task)) cache.Task = null;
            }
        }
    }

    public Task<IReadOnlyList<DocumentFile>> GetFilesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<IReadOnlyList<DocumentFile>> task;
        lock (_gate)
        {
            var session = RequireSession();
            if (session.IndexTask is not null) return session.IndexTask.WaitAsync(cancellationToken);
            if (session.Index is not null && session.PendingIndex.Count == 0)
                return Task.FromResult(session.Files!);
            task = StartIndex(session);
        }
        return task.WaitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DocumentFile>> BuildIndexAsync(Session session)
    {
        Dictionary<string, DocumentFile>? index;
        lock (_gate)
        {
            EnsureCurrent(session);
            index = session.Index is null ? null : new(session.Index, WorkspaceFileEnumeration.PathComparer);
        }
        if (index is null)
        {
            index = await ScanIndexAsync(session, session.Root).ConfigureAwait(false);
        }

        while (true)
        {
            string[] pending;
            lock (_gate)
            {
                EnsureCurrent(session);
                pending = session.PendingIndex.ToArray();
                session.PendingIndex.Clear();
                if (pending.Length == 0)
                {
                    session.Index = index;
                    return session.Files = SortIndex(index);
                }
            }
            foreach (var path in CollapsePaths(pending))
            {
                if (WorkspaceFileEnumeration.PathComparer.Equals(path, session.Root) && !Directory.Exists(path))
                    throw new DirectoryNotFoundException(path);
                foreach (var removed in index.Keys.Where(candidate => IsWithin(path, candidate)).ToArray()) index.Remove(removed);
                if (Directory.Exists(path))
                {
                    foreach (var pair in await ScanIndexAsync(session, path).ConfigureAwait(false)) index[pair.Key] = pair.Value;
                }
                else if (File.Exists(path) && WorkspaceFileEnumeration.IsSupportedDocumentPath(path))
                {
                    var file = await RunDiskAsync(session, () => documentFileFactory.Create(path, session.Root)).ConfigureAwait(false);
                    index[path] = file;
                }
            }
        }
    }

    private Task<Dictionary<string, DocumentFile>> ScanIndexAsync(Session session, string path) => RunDiskAsync(session, () =>
    {
        ValidatePath(session, path, directory: true);
        Interlocked.Increment(ref _indexScanCount);
        var index = new Dictionary<string, DocumentFile>(WorkspaceFileEnumeration.PathComparer);
        foreach (var candidate in WorkspaceFileEnumeration.EnumerateFiles(path, session.Token, directory =>
                 {
                     lock (_gate)
                     {
                         EnsureCurrent(session);
                         session.KnownDirectories.Add(directory);
                     }
                 }))
        {
            if (WorkspaceFileEnumeration.IsSupportedDocumentPath(candidate)) index[candidate] = documentFileFactory.Create(candidate, session.Root);
        }
        return index;
    });

    public Task<string> GetPreviewAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<string> task;
        lock (_gate)
        {
            var session = RequireSession();
            path = ValidatePath(session, path, directory: false, checkLinks: false);
            if (!session.Previews.TryGetValue(path, out var preview))
            {
                preview = new PreviewCache();
                session.Previews.Add(path, preview);
            }
            if (preview.Value is { } value) return Task.FromResult(value);
            var revision = preview.Revision;
            task = preview.Task ??= Task.Run(() => ReadPreviewAsync(session, path, preview, revision), session.Token);
        }
        return task.WaitAsync(cancellationToken);
    }

    private async Task<string> ReadPreviewAsync(Session session, string path, PreviewCache cache, long revision)
    {
        try
        {
            var preview = await RunDiskAsync(session, () =>
            {
                ValidatePath(session, path, directory: false);
                Interlocked.Increment(ref _previewReadCount);
                return documentFileFactory.ReadPreview(path);
            }).ConfigureAwait(false);
            lock (_gate)
            {
                EnsureCurrent(session);
                if (cache.Revision != revision || !session.Previews.TryGetValue(path, out var current) || !ReferenceEquals(cache, current))
                    throw new OperationCanceledException("摘要读取已过期。", session.Token);
                return cache.Value = preview;
            }
        }
        finally
        {
            lock (_gate)
            {
                if (cache.Revision == revision) cache.Task = null;
            }
        }
    }

    public bool IsCurrent(WorkspaceDirectorySnapshot snapshot)
    {
        lock (_gate)
        {
            return _session is { } session && session.Generation == snapshot.Generation
                && WorkspaceFileEnumeration.PathComparer.Equals(session.Root, snapshot.RootPath)
                && session.Directories.TryGetValue(snapshot.Path, out var cache) && cache.Revision == snapshot.Revision;
        }
    }

    /// <summary>监控溢出和可控验证共用恢复入口，不预先建立尚未请求的索引。</summary>
    public void RequestRecovery(Exception? error = null)
    {
        WorkspaceFileMonitor monitor;
        lock (_gate) monitor = RequireSession().Monitor!;
        monitor.ReportError(error);
    }

    private bool IsKnownDirectory(Session session, string path)
    {
        lock (_gate) return session.KnownDirectories.Contains(path) || session.Directories.ContainsKey(path);
    }

    private void OnMonitorChanged(Session session, IReadOnlyList<WorkspacePathChange> changes, bool recovery, Exception? error)
    {
        string[] directories;
        string[] changedPaths;
        var metadata = new List<(string Path, long Revision)>();
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session) || _disposed) return;
            var affected = new HashSet<string>(WorkspaceFileEnumeration.PathComparer);
            foreach (var change in changes)
            {
                var metadataRevision = session.MetadataRevisions.GetValueOrDefault(change.Path) + 1;
                session.MetadataRevisions[change.Path] = metadataRevision;
                if (!change.IsDirectory && !change.IsStructural) metadata.Add((change.Path, metadataRevision));
                if (change.IsStructural && Path.GetDirectoryName(change.Path) is { } parent) affected.Add(parent);
                if (change.IsDirectory)
                {
                    foreach (var key in session.Directories.Keys.Where(path => IsWithin(change.Path, path)).ToArray())
                    {
                        Invalidate(session.Directories[key]);
                        session.Directories.Remove(key);
                    }
                    session.KnownDirectories.RemoveWhere(path => IsWithin(change.Path, path));
                    if (Directory.Exists(change.Path)) session.KnownDirectories.Add(change.Path);
                }
                foreach (var key in session.Previews.Keys.Where(path => IsWithin(change.Path, path)).ToArray())
                {
                    var preview = session.Previews[key];
                    preview.Revision++;
                    preview.Value = null;
                    preview.Task = null;
                }
                if (session.Index is not null || session.IndexTask is not null) session.PendingIndex.Add(change.Path);
            }
            if (recovery)
            {
                foreach (var directory in session.Directories.Keys) affected.Add(directory);
                foreach (var preview in session.Previews.Values)
                {
                    preview.Revision++;
                    preview.Value = null;
                    preview.Task = null;
                }
                if (session.Index is not null || session.IndexTask is not null) session.PendingIndex.Add(session.Root);
            }
            foreach (var directory in affected)
            {
                if (session.Directories.TryGetValue(directory, out var cache)) Invalidate(cache);
            }
            directories = affected.ToArray();
            changedPaths = changes.Select(change => change.Path).Distinct(WorkspaceFileEnumeration.PathComparer).ToArray();
            // 已建立的索引随事件修复；普通树首次打开不会进入此分支。
            if ((session.Index is not null || session.IndexTask is not null) && session.IndexTask is null)
            {
                StartIndex(session);
            }
        }
        if (metadata.Count != 0) _ = RefreshMetadataBatchAsync(session, metadata);
        if (recovery)
        {
            try
            {
                session.Monitor!.RetryWatching();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                error = ex;
            }
        }
        Publish(session, directories, changedPaths, recovery, error);
    }

    private async Task RefreshMetadataAsync(Session session, string path, long revision)
    {
        try
        {
            var file = await RunDiskAsync(session, () => documentFileFactory.Create(path, session.Root)).ConfigureAwait(false);
            lock (_gate)
            {
                EnsureCurrent(session);
                if (session.MetadataRevisions.GetValueOrDefault(path) != revision) return;
                if (Path.GetDirectoryName(path) is { } parent && session.Directories.TryGetValue(parent, out var cache)
                    && cache.Snapshot is { } snapshot)
                {
                    var entries = snapshot.Entries.Select(entry => !entry.IsDirectory
                        && WorkspaceFileEnumeration.PathComparer.Equals(entry.Path, path)
                        ? entry with { DocumentFile = file } : entry).ToArray();
                    // 不在后台触发已有绑定模型的 PropertyChanged，界面根据 ChangedPaths 更新自身节点。
                    cache.Snapshot = snapshot with { Entries = entries };
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Publish(session, [], [path], false, ex);
        }
    }

    private async Task RefreshMetadataBatchAsync(Session session, IReadOnlyList<(string Path, long Revision)> metadata)
    {
        foreach (var item in metadata)
        {
            if (session.Token.IsCancellationRequested) return;
            await RefreshMetadataAsync(session, item.Path, item.Revision).ConfigureAwait(false);
        }
    }

    private Task<IReadOnlyList<DocumentFile>> StartIndex(Session session)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<DocumentFile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.IndexTask = completion.Task;
        // 后台修复可能没有等待者，观察故障；显式请求者仍得到原始异常。
        _ = completion.Task.ContinueWith(task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _ = Task.Run(() => CompleteIndexAsync(session, completion));
        return completion.Task;
    }

    private async Task CompleteIndexAsync(Session session, TaskCompletionSource<IReadOnlyList<DocumentFile>> completion)
    {
        try
        {
            while (true)
            {
                var files = await BuildIndexAsync(session).ConfigureAwait(false);
                lock (_gate)
                {
                    EnsureCurrent(session);
                    if (session.PendingIndex.Count != 0) continue;
                    session.IndexTask = null;
                    completion.TrySetResult(files);
                    return;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            lock (_gate)
            {
                if (ReferenceEquals(session.IndexTask, completion.Task)) session.IndexTask = null;
            }
            completion.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (ReferenceEquals(session.IndexTask, completion.Task)) session.IndexTask = null;
                if (ReferenceEquals(_session, session)) session.PendingIndex.Add(session.Root);
            }
            completion.TrySetException(ex);
            Publish(session, [], [], true, ex);
        }
    }

    private async Task<T> RunDiskAsync<T>(Session session, Func<T> action)
    {
        await _operations.WaitAsync(session.Token).ConfigureAwait(false);
        var active = Interlocked.Increment(ref _activeOperations);
        UpdateMaximumConcurrency(active);
        try
        {
            session.Token.ThrowIfCancellationRequested();
            return await Task.Run(action, session.Token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _activeOperations);
            _operations.Release();
        }
    }

    private void UpdateMaximumConcurrency(int value)
    {
        var current = Volatile.Read(ref _maximumConcurrentOperations);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref _maximumConcurrentOperations, value, current);
            if (previous == current) return;
            current = previous;
        }
    }

    private void Publish(Session session, IReadOnlyList<string> directories, IReadOnlyList<string> paths, bool recovery, Exception? error)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session) || _disposed) return;
        }
        Changed?.Invoke(this, new WorkspaceFilesChangedEventArgs(session.Generation, session.Root, directories, paths, recovery, error));
    }

    private Session RequireSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _session ?? throw new InvalidOperationException("当前没有打开的工作区。");
    }

    private void EnsureCurrent(Session session)
    {
        if (!ReferenceEquals(_session, session) || session.Token.IsCancellationRequested || _disposed)
            throw new OperationCanceledException("工作区已切换。", session.Token);
    }

    private static string ValidatePath(Session session, string path, bool directory, bool checkLinks = true)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!WorkspaceFileEnumeration.IsVisiblePath(session.Root, path)
            || directory && !WorkspaceFileEnumeration.PathComparer.Equals(path, session.Root) && Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal))
            throw new UnauthorizedAccessException(path);
        if (!checkLinks) return path;
        for (var current = path; !WorkspaceFileEnumeration.PathComparer.Equals(current, session.Root); current = Path.GetDirectoryName(current)!)
        {
            if (string.IsNullOrEmpty(current)) throw new UnauthorizedAccessException(path);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException(path);
        }
        return path;
    }

    private static bool IsWithin(string parent, string path)
    {
        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return WorkspaceFileEnumeration.PathComparer.Equals(parent, path)
            || path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static IEnumerable<string> CollapsePaths(IEnumerable<string> paths)
    {
        var accepted = new List<string>();
        foreach (var path in paths.OrderBy(path => path.Length))
        {
            if (!accepted.Any(parent => IsWithin(parent, path))) accepted.Add(path);
        }
        return accepted;
    }

    private static DocumentFile[] SortIndex(Dictionary<string, DocumentFile> index) => index.Values
        .OrderBy(file => file.Path, WorkspaceFileEnumeration.PathComparer).ToArray();

    private static void Invalidate(DirectoryCache cache)
    {
        cache.Revision++;
        cache.Snapshot = null;
    }

    private static void Release(Session? session)
    {
        if (session is null) return;
        session.Cancellation.Cancel();
        session.Monitor?.Dispose();
        session.Cancellation.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Close();
    }

    private sealed class Session
    {
        public Session(string root, long generation)
        {
            Root = root;
            Generation = generation;
            Token = Cancellation.Token;
        }

        public string Root { get; }
        public long Generation { get; }
        public CancellationTokenSource Cancellation { get; } = new();
        public CancellationToken Token { get; }
        public WorkspaceFileMonitor? Monitor { get; set; }
        public Dictionary<string, DirectoryCache> Directories { get; } = new(WorkspaceFileEnumeration.PathComparer);
        public HashSet<string> KnownDirectories { get; } = new(WorkspaceFileEnumeration.PathComparer);
        public Dictionary<string, PreviewCache> Previews { get; } = new(WorkspaceFileEnumeration.PathComparer);
        public Dictionary<string, DocumentFile>? Index { get; set; }
        public IReadOnlyList<DocumentFile>? Files { get; set; }
        public Task<IReadOnlyList<DocumentFile>>? IndexTask { get; set; }
        public HashSet<string> PendingIndex { get; } = new(WorkspaceFileEnumeration.PathComparer);
        public Dictionary<string, long> MetadataRevisions { get; } = new(WorkspaceFileEnumeration.PathComparer);
    }

    private sealed class DirectoryCache
    {
        public long Revision { get; set; }
        public WorkspaceDirectorySnapshot? Snapshot { get; set; }
        public Task<WorkspaceDirectorySnapshot>? Task { get; set; }
    }

    private sealed class PreviewCache
    {
        public long Revision { get; set; }
        public string? Value { get; set; }
        public Task<string>? Task { get; set; }
    }
}
