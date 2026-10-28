using Avalonia.Threading;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>直接打开文件时先建立父工作区，读取只涉及父目录的一层。</summary>
    private async Task EnsureDocumentWorkspaceAsync(string path)
    {
        if (_workspaceFiles.RootPath is { } root && IsPathUnderDirectory(path, root)) return;
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is not { } directory) return;
        _workspaceFiles.Open(directory);
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
        // 工作区服务在首次快照前挂接唯一递归监控器，这里仅协调会话身份。
        if (folderPath is not { Length: > 0 }) return;
        if (_workspaceFiles.RootPath is not { } root || !PathsEqual(root, folderPath))
            _workspaceFiles.Open(folderPath);
        _watchedFolderPath = folderPath;
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
