using Avalonia.Threading;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly object _fileCheckGate = new();
    private long _fileWatchEpoch;
    private long _fileEventRevision;
    private long _externalCandidateRevision;
    private long _activeFileReadEpoch = -1;
    private Task? _activeFileReadTask;
    private DateTime? _firstFileEventUtc;
    private bool _fileWatcherNeedsRestart;

    private void StopCurrentFileWatcher()
    {
        lock (_fileCheckGate)
        {
            ++_fileWatchEpoch;
            ++_fileEventRevision;
            _firstFileEventUtc = null;
            _currentFileChangeTimer?.Dispose();
            _currentFileChangeTimer = null;
        }
        if (_currentFileWatcher is not null)
        {
            _currentFileWatcher.EnableRaisingEvents = false;
            _currentFileWatcher.Changed -= HandleWatchedFileChanged;
            _currentFileWatcher.Created -= HandleWatchedFileChanged;
            _currentFileWatcher.Deleted -= HandleWatchedFileChanged;
            _currentFileWatcher.Renamed -= HandleWatchedFileChanged;
            _currentFileWatcher.Error -= HandleCurrentFileWatcherError;
            _currentFileWatcher.Dispose();
            _currentFileWatcher = null;
        }
        _watchedFilePath = null;
        _watchedFileLastWriteTimeUtc = null;
        _fileWatcherNeedsRestart = false;
    }

    private void HandleWatchedFileChanged(object sender, FileSystemEventArgs args)
    {
        if (!ReferenceEquals(sender, _currentFileWatcher) || _watchedFilePath is not { } path) return;
        if (PathsEqual(path, args.FullPath) || args is RenamedEventArgs renamed && PathsEqual(path, renamed.OldFullPath))
            ScheduleCurrentFileCheck();
    }

    private void HandleCurrentFileWatcherError(object sender, ErrorEventArgs args)
    {
        if (!ReferenceEquals(sender, _currentFileWatcher)) return;
        _fileWatcherNeedsRestart = true;
        ScheduleCurrentFileCheck();
    }

    private void ScheduleCurrentFileCheck()
    {
        lock (_fileCheckGate)
        {
            if (_watchedFilePath is null) return;
            var epoch = _fileWatchEpoch;
            ++_fileEventRevision;
            _firstFileEventUtc ??= DateTime.UtcNow;
            var remaining = 2000 - (DateTime.UtcNow - _firstFileEventUtc.Value).TotalMilliseconds;
            _currentFileChangeTimer ??= new Timer(_ => Dispatcher.UIThread.Post(() =>
            {
                if (epoch != _fileWatchEpoch) return;
                lock (_fileCheckGate) _firstFileEventUtc = null;
                _ = ReloadWatchedFileAsync();
            }));
            _currentFileChangeTimer.Change(TimeSpan.FromMilliseconds(Math.Clamp(remaining, 1, 500)), Timeout.InfiniteTimeSpan);
        }
    }

    public async Task RetryCurrentFileAsync()
    {
        var restart = _fileWatcherNeedsRestart || _currentFileWatcher is null;
        if (restart) StartCurrentFileWatcher();
        ++_fileEventRevision;
        await ReloadWatchedFileAsync();
    }

    /// <summary>候选读取不修改共享正文；只提交同一文档、同一次读取的稳定结果。</summary>
    private Task ReloadWatchedFileAsync()
    {
        if (_activeFileReadEpoch == _fileWatchEpoch && _activeFileReadTask is { IsCompleted: false }) return _activeFileReadTask;
        return _activeFileReadTask = ReadCurrentFileCandidateAsync();
    }

    private async Task ReadCurrentFileCandidateAsync()
    {
        if (_watchedFilePath is not { } path || !IsCurrentDocumentPath(path)) return;
        if (_fileWatcherNeedsRestart)
        {
            try { StartCurrentFileWatcher(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Protection.FileReadError = _localizer.Get(VexL.StatusCurrentFileUnavailable);
                return;
            }
        }
        var epoch = _fileWatchEpoch;
        if (_activeFileReadEpoch == epoch) return;
        _activeFileReadEpoch = epoch;
        var document = _document;
        var documentVersion = _documentOpenVersion;
        long revision = 0;
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                revision = Interlocked.Read(ref _fileEventRevision);
                try
                {
                    var before = ReadFileStamp(path);
                    if (!before.Exists) throw new FileNotFoundException(null, path);
                    var incoming = await _documentService.ReloadAsync(document);
                    var after = ReadFileStamp(path);
                    if (epoch != _fileWatchEpoch || documentVersion != _documentOpenVersion || !IsCurrentDocumentPath(path)) return;
                    if (revision != Interlocked.Read(ref _fileEventRevision) || before != after)
                    {
                        if (attempt < 2) { await Task.Delay(100 * (attempt + 1)); continue; }
                        ScheduleCurrentFileCheck();
                        return;
                    }
                    Protection.FileReadError = null;
                    _watchedFileLastWriteTimeUtc = after.WriteTime;
                    if (Core.Services.MarkdownTextComparer.EqualsNormalizedLineEndings(_lastSavedMarkdown, incoming.Markdown))
                    {
                        if (Protection.External is not null) ++_externalCandidateRevision;
                        Protection.External = null;
                    }
                    else if (Protection.External is not { } previous
                             || !Core.Services.MarkdownTextComparer.EqualsNormalizedLineEndings(previous.Markdown, incoming.Markdown))
                    {
                        ++_externalCandidateRevision;
                        Protection.External = incoming;
                    }
                    return;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    if (epoch != _fileWatchEpoch || documentVersion != _documentOpenVersion) return;
                    if (attempt < 2) { await Task.Delay(100 * (attempt + 1)); continue; }
                    Protection.External = null;
                    Protection.FileReadError = _localizer.Get(VexL.StatusCurrentFileUnavailable);
                    _statusPublisher.PublishResource(VexL.StatusCurrentFileUnavailable);
                }
            }
        }
        finally
        {
            if (_activeFileReadEpoch == epoch) _activeFileReadEpoch = -1;
            if (epoch == _fileWatchEpoch && revision != Interlocked.Read(ref _fileEventRevision)) ScheduleCurrentFileCheck();
        }
    }

    private static (bool Exists, long Length, DateTimeOffset WriteTime) ReadFileStamp(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? (true, info.Length, new DateTimeOffset(info.LastWriteTimeUtc)) : (false, 0, default);
    }
}
