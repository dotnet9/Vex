using Avalonia.Threading;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>直接打开文件时先确保所在工作区根已挂载（FR-15：路径在任一已挂载根下即命中，不替换工作区）。</summary>
    private async Task EnsureDocumentWorkspaceAsync(string path)
    {
        if (_workspaceFiles.RootPaths.Any(root => IsPathUnderDirectory(path, root))) return;
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is not { } directory) return;
        _workspaceFiles.Open(directory);
        ClearPersistedWorkspaceExtras();
        var generation = _workspaceFiles.Generation;
        UpdateCurrentWorkspaceFolder(directory, true);
        var snapshot = await _workspaceFiles.GetDirectoryAsync(directory);
        if (generation != _workspaceFiles.Generation || !_workspaceFiles.IsCurrent(snapshot)) return;
        _documentFiles = snapshot.Entries.Select(entry => entry.DocumentFile).OfType<DocumentFile>().ToArray();
        PublishDocumentFilesChanged(focusWorkspaceRoot: true);
        _watchedFolderPath = directory;
    }

    private async Task InitializeDocumentWorkspaceAsync(string path)
    {
        try
        {
            var version = _documentOpenVersion;
            await EnsureDocumentWorkspaceAsync(path);
            if (version == _documentOpenVersion && IsCurrentDocumentPath(path)) SyncDocumentFileList(path);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _statusPublisher.PublishResource(VexL.StatusCurrentFileUnavailable);
        }
    }

    private void StartCurrentFolderWatcher(string? folderPath)
    {
        // 工作区服务在首次快照前挂接递归监控器；路径已在任一已挂载根下时不再替换。
        if (folderPath is not { Length: > 0 }) return;
        if (_workspaceFiles.RootPaths.Any(root => PathsEqual(root, folderPath))) return;
        _workspaceFiles.Open(folderPath);
        ClearPersistedWorkspaceExtras();
        _watchedFolderPath = folderPath;
    }

    /// <summary>FR-15：替换主根时清掉上一工作区的附加根持久化，避免旧附加根挂到新主根下。</summary>
    private void ClearPersistedWorkspaceExtras()
    {
        if (_settingsStore.Current.WorkspaceExtraFolders is { Count: > 0 })
        {
            _settingsStore.Update(settings => settings with { WorkspaceExtraFolders = null });
        }
    }

    private void StopCurrentFolderWatcher()
    {
        _workspaceFiles.Close();
        _watchedFolderPath = null;
    }

    private void WorkspaceFilesChanged(object? sender, WorkspaceFilesChangedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (args.Generation != _workspaceFiles.Generation || _guideDocument is not null) return;
            if (args.IsRecovery || args.ChangedPaths.Any(path => _watchedFilePath is { } current && PathsEqual(path, current)))
                ScheduleCurrentFileCheck();
            if (args.Error is not null) _statusPublisher.PublishResource(VexL.ErrorMessageActionFailed);
        });
    }
}
