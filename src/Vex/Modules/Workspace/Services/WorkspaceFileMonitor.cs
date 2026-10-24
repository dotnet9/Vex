using System.Diagnostics;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

/// <summary>将一个递归监控器的事件合并为有最长等待边界的工作区变更批次。</summary>
public sealed class WorkspaceFileMonitor : IDisposable
{
    public const int MergeDelayMilliseconds = 500;
    public const int MaximumDelayMilliseconds = 2000;
    private readonly object _gate = new();
    private readonly string _rootPath;
    private readonly Func<string, bool> _isKnownDirectory;
    private readonly Action<IReadOnlyList<WorkspacePathChange>, bool, Exception?> _notify;
    private readonly Dictionary<string, WorkspacePathChange> _pending = new(WorkspaceFileEnumeration.PathComparer);
    private readonly Timer _timer;
    private readonly Timer _availabilityTimer;
    private FileSystemWatcher? _watcher;
    private long _firstEvent;
    private bool _recovery;
    private Exception? _error;
    private bool _disposed;
    private bool _rootUnavailable;

    internal WorkspaceFileMonitor(string rootPath, Func<string, bool> isKnownDirectory,
        Action<IReadOnlyList<WorkspacePathChange>, bool, Exception?> notify)
    {
        _rootPath = rootPath;
        _isKnownDirectory = isKnownDirectory;
        _notify = notify;
        _timer = new Timer(Flush);
        try
        {
            AttachWatcher();
        }
        catch
        {
            _timer.Dispose();
            _watcher?.Dispose();
            throw;
        }
        _availabilityTimer = new Timer(CheckRootAvailability, null, 1000, 1000);
    }

    /// <summary>用于真实事件与可控验证的统一入口。</summary>
    public void ReportChange(string path, string? oldPath = null, bool? isDirectory = null,
        WatcherChangeTypes changeType = WatcherChangeTypes.Created)
    {
        QueuePath(path, isDirectory, changeType);
        if (!string.IsNullOrWhiteSpace(oldPath))
        {
            QueuePath(oldPath, isDirectory, WatcherChangeTypes.Deleted);
        }
    }

    /// <summary>注入监控异常与系统 Error 走相同的恢复入口。</summary>
    public void ReportError(Exception? error = null)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _recovery = true;
            _error = error;
            Schedule();
        }
    }

    internal void RetryWatching()
    {
        lock (_gate)
        {
            if (_disposed) return;
            DetachWatcher();
            AttachWatcher();
        }
    }

    private void AttachWatcher()
    {
        var watcher = new FileSystemWatcher(_rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 32 * 1024
        };
        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Changed += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;
        _watcher = watcher;
        watcher.EnableRaisingEvents = true;
    }

    private void OnCreated(object sender, FileSystemEventArgs e) => ReportChange(e.FullPath, changeType: WatcherChangeTypes.Created);
    private void OnDeleted(object sender, FileSystemEventArgs e) => ReportChange(e.FullPath, changeType: WatcherChangeTypes.Deleted);
    private void OnChanged(object sender, FileSystemEventArgs e) => ReportChange(e.FullPath, changeType: WatcherChangeTypes.Changed);
    private void OnRenamed(object sender, RenamedEventArgs e) => ReportChange(e.FullPath, e.OldFullPath,
        Directory.Exists(e.FullPath) || _isKnownDirectory(e.OldFullPath), WatcherChangeTypes.Renamed);
    private void OnError(object sender, ErrorEventArgs e) => ReportError(e.GetException());

    private void CheckRootAvailability(object? state)
    {
        var unavailable = !Directory.Exists(_rootPath);
        lock (_gate)
        {
            if (_disposed || _rootUnavailable == unavailable) return;
            _rootUnavailable = unavailable;
        }
        ReportError(unavailable ? new DirectoryNotFoundException(_rootPath) : null);
    }

    private void QueuePath(string path, bool? directoryHint, WatcherChangeTypes changeType)
    {
        path = Path.GetFullPath(path);
        var isDirectory = directoryHint ?? (Directory.Exists(path) || _isKnownDirectory(path));
        lock (_gate)
        {
            if (_pending.TryGetValue(path, out var pending)) isDirectory |= pending.IsDirectory;
        }
        if (!WorkspaceFileEnumeration.IsVisiblePath(_rootPath, path)
            || isDirectory && Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal)
            || !isDirectory && !WorkspaceFileEnumeration.IsSupportedDocumentPath(path)
            || isDirectory && changeType == WatcherChangeTypes.Changed)
        {
            return;
        }

        // 链接不会进入目录树或索引，链接内部事件也不能使它们间接进入缓存。
        if (changeType != WatcherChangeTypes.Deleted && HasReparsePoint(path)) return;
        var structural = changeType != WatcherChangeTypes.Changed;
        lock (_gate)
        {
            if (_disposed) return;
            if (_pending.TryGetValue(path, out var previous))
            {
                structural |= previous.IsStructural;
                isDirectory |= previous.IsDirectory;
            }
            _pending[path] = new WorkspacePathChange(path, isDirectory, structural);
            Schedule();
        }
    }

    private bool HasReparsePoint(string path)
    {
        for (var current = path; !WorkspaceFileEnumeration.PathComparer.Equals(current, _rootPath); current = Path.GetDirectoryName(current)!)
        {
            if (string.IsNullOrEmpty(current)) return true;
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 原子替换中的短暂缺失交给后续快照校验。
            }
        }
        return false;
    }

    private void Schedule()
    {
        if (_firstEvent == 0) _firstEvent = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_firstEvent).TotalMilliseconds;
        var due = (int)Math.Max(0, Math.Min(MergeDelayMilliseconds, MaximumDelayMilliseconds - elapsed));
        _timer.Change(due, Timeout.Infinite);
    }

    private void Flush(object? state)
    {
        WorkspacePathChange[] changes;
        bool recovery;
        Exception? error;
        lock (_gate)
        {
            if (_disposed) return;
            changes = _pending.Values.ToArray();
            recovery = _recovery;
            error = _error;
            _pending.Clear();
            _firstEvent = 0;
            _recovery = false;
            _error = null;
        }
        _notify(changes, recovery, error);
    }

    private void DetachWatcher()
    {
        if (_watcher is not { } watcher) return;
        _watcher = null;
        watcher.EnableRaisingEvents = false;
        watcher.Created -= OnCreated;
        watcher.Deleted -= OnDeleted;
        watcher.Changed -= OnChanged;
        watcher.Renamed -= OnRenamed;
        watcher.Error -= OnError;
        watcher.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
            _timer.Dispose();
            _availabilityTimer.Dispose();
            DetachWatcher();
        }
    }

    internal sealed record WorkspacePathChange(string Path, bool IsDirectory, bool IsStructural);
}
