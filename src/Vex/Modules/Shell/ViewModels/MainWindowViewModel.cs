using Avalonia.Threading;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject
{
    private static readonly TimeSpan MarkdownDerivedStateDebounce = TimeSpan.FromMilliseconds(220);
    private readonly IDocumentService _documentService;
    private readonly IWorkspaceDocumentState _workspaceDocumentState;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IMarkdownStatisticsService _statisticsService;
    private readonly IDocumentFileFactory _documentFileFactory;
    private readonly IShellDocumentWorkflowText _text;
    private readonly IShellUnsavedChangesGuard _unsavedChanges;
    private readonly IShellDocumentUtilityActions _documentUtilities;
    private readonly IShellExternalPathResolver _externalPaths;
    private readonly IAutoSaveDraftService _drafts;
    private readonly IShellStatusPublisher _statusPublisher;
    private FileSystemWatcher? _currentFileWatcher;
    private Timer? _currentFileChangeTimer;
    private Timer? _markdownDerivedStateTimer;
    private DocumentSnapshot _document;
    private IReadOnlyList<DocumentFile> _documentFiles = [];
    private string _lastSavedMarkdown = string.Empty;
    private string _markdown = string.Empty;
    private string? _watchedFilePath;
    private DateTimeOffset? _watchedFileLastWriteTimeUtc;
    private DateTimeOffset? _lastSkippedExternalWriteTimeUtc;

    public MainWindowViewModel(
        IDocumentService documentService,
        IWorkspaceDocumentState workspaceDocumentState,
        IMarkdownOutlineService outlineService,
        IMarkdownStatisticsService statisticsService,
        IDocumentFileFactory documentFileFactory,
        ShellAppearanceViewModel appearance,
        ShellDocumentInfoViewModel documentInfo,
        ShellDialogsViewModel dialogs,
        ShellEditorActionsViewModel editorActions,
        ShellEditorDisplayViewModel editorDisplay,
        ShellFindBarViewModel findBar,
        ShellHelpViewModel help,
        ShellWindowLayoutViewModel layout,
        ShellNavigationViewModel navigation,
        ShellRecentDocumentsViewModel recent,
        ShellStatusViewModel status,
        IShellDocumentWorkflowText text,
        IShellUnsavedChangesGuard unsavedChanges,
        IShellDocumentUtilityActions documentUtilities,
        IShellExternalPathResolver externalPaths,
        IAutoSaveDraftService drafts,
        IShellStatusPublisher statusPublisher)
    {
        _documentService = documentService;
        _workspaceDocumentState = workspaceDocumentState;
        _outlineService = outlineService;
        _statisticsService = statisticsService;
        _documentFileFactory = documentFileFactory;
        Appearance = appearance;
        DocumentInfo = documentInfo;
        Dialogs = dialogs;
        EditorActions = editorActions;
        EditorDisplay = editorDisplay;
        FindBar = findBar;
        Help = help;
        Layout = layout;
        Navigation = navigation;
        Recent = recent;
        Status = status;
        _text = text;
        _unsavedChanges = unsavedChanges;
        _documentUtilities = documentUtilities;
        _externalPaths = externalPaths;
        _drafts = drafts;
        _statusPublisher = statusPublisher;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);

        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        _document = RestoreDraftIfAvailable(_document, out var restoredInitialDraft);
        Markdown = _document.Markdown;
        RefreshDocumentInfo();
        if (restoredInitialDraft)
        {
            _statusPublisher.PublishResource(VexL.StatusRecoveredAutoSaveDraft);
        }
    }

    public event EventHandler? CloseWindowRequested;

    public ShellAppearanceViewModel Appearance { get; }
    public ShellDocumentInfoViewModel DocumentInfo { get; }
    public ShellDialogsViewModel Dialogs { get; }
    public ShellEditorActionsViewModel EditorActions { get; }
    public ShellEditorDisplayViewModel EditorDisplay { get; }
    public ShellFindBarViewModel FindBar { get; }
    public ShellHelpViewModel Help { get; }
    public ShellWindowLayoutViewModel Layout { get; }
    public ShellNavigationViewModel Navigation { get; }
    public ShellRecentDocumentsViewModel Recent { get; }
    public ShellStatusViewModel Status { get; }

    public async Task OpenStartupDocumentAsync(IEnumerable<string> arguments)
    {
        var target = _externalPaths.ResolveStartupArgument(arguments);
        if (target.Path is not { Length: > 0 } path)
        {
            return;
        }

        if (target.Kind == ShellExternalPathKind.Folder)
        {
            await RequestUnsavedConfirmationAsync(
                _text.TitleBeforeOpeningFolder,
                _text.BeforeOpeningFolder(_document.FileName),
                GuardedAction(
                    VexL.ErrorMessageCannotOpenFolderFormat,
                    async () => await ApplyDocumentFilesAsync(await _documentService.OpenFolderPathAsync(path), true),
                    path));
            return;
        }

        if (target.Kind == ShellExternalPathKind.File)
        {
            await RequestUnsavedConfirmationAsync(
                _text.TitleBeforeOpening,
                _text.BeforeOpeningFile(_document.FileName, target.FileName ?? path),
                GuardedAction(
                    VexL.ErrorMessageCannotOpenFileFormat,
                    async () => ApplyDocument(await _documentService.OpenPathAsync(path)),
                    path));
        }
    }

    public Task OpenDroppedPathAsync(string path)
    {
        var target = _externalPaths.ResolveDroppedPath(path);
        if (target.Kind == ShellExternalPathKind.Missing)
        {
            return PublishAndComplete(_text.PublishDroppedItemUnavailable);
        }

        if (target.Kind == ShellExternalPathKind.Unsupported)
        {
            return PublishAndComplete(_text.PublishDropMarkdownOrTextFile);
        }

        if (target.Path is not { Length: > 0 } resolvedPath)
        {
            return Task.CompletedTask;
        }

        if (target.Kind == ShellExternalPathKind.Folder)
        {
            return RequestUnsavedConfirmationAsync(
                _text.TitleBeforeOpeningFolder,
                _text.BeforeOpeningDroppedFolder(_document.FileName),
                GuardedAction(
                    VexL.ErrorMessageCannotOpenFolderFormat,
                    () => OpenFolderPathCoreAsync(resolvedPath),
                    resolvedPath));
        }

        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpening,
            _text.BeforeOpeningFile(_document.FileName, target.FileName ?? resolvedPath),
            GuardedAction(
                VexL.ErrorMessageCannotOpenFileFormat,
                () => OpenPathCoreAsync(resolvedPath),
                resolvedPath));
    }

    public string Markdown
    {
        get => _markdown;
        set => SetMarkdown(value, refreshImmediately: true);
    }

    private void SetMarkdown(string? value, bool refreshImmediately)
    {
        var normalized = value ?? string.Empty;
        if (_markdown == normalized)
        {
            return;
        }

        this.RaiseAndSetIfChanged(ref _markdown, normalized);
        _document = _document with { Markdown = _markdown };

        if (refreshImmediately)
        {
            CancelScheduledMarkdownDerivedStateRefresh();
            RefreshMarkdownDerivedState();
            return;
        }

        RefreshDocumentState();
        _drafts.QueueSave(_document, Markdown, _lastSavedMarkdown);
        ScheduleMarkdownDerivedStateRefresh();
    }

    public Task NewDocument()
    {
        return RequestUnsavedConfirmationAsync(
            _text.TitleSaveChanges,
            _text.BeforeNewDocument(_document.FileName),
            () =>
            {
                NewDocumentCore();
                return Task.CompletedTask;
            });
    }

    private void NewDocumentCore()
    {
        StopCurrentFileWatcher();
        _drafts.Clear(_document);
        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
        _text.PublishNewDocumentCreated();
        RefreshDocumentInfo();
        PublishWorkspaceDocumentState();
        EditorActions.FocusEditor();
    }

    public Task CloseDocument()
    {
        return RequestUnsavedConfirmationAsync(
            _text.TitleSaveChanges,
            _text.BeforeClosingDocument(_document.FileName),
            () =>
            {
                CloseDocumentCore();
                return Task.CompletedTask;
            });
    }

    private void CloseDocumentCore()
    {
        StopCurrentFileWatcher();
        _drafts.Clear(_document);
        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
        _documentFiles = [];
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles));
        _text.PublishDocumentClosed();
        RefreshDocumentInfo();
        PublishWorkspaceDocumentState();
        EditorActions.FocusEditor();
    }

    public async Task OpenAsync()
    {
        await RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpening,
            _text.BeforeOpeningAnotherFile(_document.FileName),
            GuardedAction(VexL.ErrorMessageCannotOpenFile, OpenAsyncCore));
    }

    private async Task OpenAsyncCore()
    {
        var snapshot = await _documentService.OpenAsync();
        if (snapshot is not null)
        {
            ApplyDocument(snapshot);
        }
    }

    public async Task QuickOpenAsync()
    {
        if (_documentFiles.Count > 0)
        {
            CodeWF.EventBus.EventBus.Default.Publish(new ShellSidebarTabSelectedCommand(0));
            _text.PublishChooseDocumentFromLoadedFolder();
            return;
        }

        await OpenAsync();
    }

    public async Task OpenFolderAsync()
    {
        await RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpeningFolder,
            _text.BeforeOpeningFolder(_document.FileName),
            GuardedAction(VexL.ErrorMessageCannotOpenFolder, OpenFolderAsyncCore));
    }

    private async Task OpenFolderAsyncCore()
    {
        await ApplyDocumentFilesAsync(await _documentService.OpenFolderAsync(), true);
    }

    private async Task OpenPathCoreAsync(string path)
    {
        ApplyDocument(await _documentService.OpenPathAsync(path));
    }

    private async Task OpenFolderPathCoreAsync(string folder)
    {
        await ApplyDocumentFilesAsync(await _documentService.OpenFolderPathAsync(folder), true);
    }

    private async Task ApplyDocumentFilesAsync(IReadOnlyList<DocumentFile> files, bool bypassUnsavedPrompt = false)
    {
        _documentFiles = files.ToArray();
        var firstFile = _documentFiles.FirstOrDefault();
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles, firstFile));

        _text.PublishLoadedMarkdownFiles(_documentFiles.Count);
        if (firstFile is not null)
        {
            if (bypassUnsavedPrompt)
            {
                await OpenDocumentFileCoreAsync(firstFile);
            }
            else
            {
                await OpenDocumentFileAsync(firstFile, null);
            }
        }
    }

    private async Task OpenDocumentFileAsync(DocumentFile file, DocumentFile? previousSelection)
    {
        if (DocumentInfo.CurrentFilePath?.Equals(file.Path, StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        await RequestUnsavedConfirmationAsync(
            _text.TitleBeforeSwitchingFiles,
            _text.BeforeSwitchingFile(_document.FileName, file.Name),
            GuardedAction(
                VexL.ErrorMessageCannotOpenFileFormat,
                () => OpenDocumentFileCoreAsync(file),
                file.Path),
            () => CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileSelectionChangedCommand(previousSelection)));
    }

    private async Task OpenDocumentFileCoreAsync(DocumentFile file)
    {
        var snapshot = await _documentService.OpenPathAsync(file.Path);
        ApplyDocument(snapshot);
    }

    public async Task SaveAsync()
    {
        await RunWithErrorOverlayAsync(
            VexL.ErrorMessageCannotSaveFormat,
            async () =>
            {
                var saved = await _documentService.SaveAsync(_document with { Markdown = Markdown });
                if (saved is not null)
                {
                    ApplyDocument(saved, false, false);
                    _text.PublishSaved(saved.FileName);
                }
            },
            _document.FileName);
    }

    public async Task SaveAsAsync()
    {
        await RunWithErrorOverlayAsync(
            VexL.ErrorMessageCannotSaveFormat,
            async () =>
            {
                var saved = await _documentService.SaveAsAsync(_document with { Markdown = Markdown });
                if (saved is not null)
                {
                    ApplyDocument(saved, false, false);
                    _text.PublishSavedAs(saved.FileName);
                }
            },
            _document.FileName);
    }

    public async Task SaveAllAsync()
    {
        // 当前仍是单文档编辑器，保留“保存全部”入口时必须明确只保存当前文档。
        await SaveAsync();
        _text.PublishSaveAllResult(DocumentInfo.IsModified);
    }

    public Task DeleteAsync()
    {
        if (DocumentInfo.CurrentFilePath is not { Length: > 0 } path)
        {
            return Task.CompletedTask;
        }

        return RequestDeleteFileAsync(path);
    }

    public async Task ConfirmDeleteAsync()
    {
        if (Dialogs.PendingDeletePath is not { Length: > 0 } path)
        {
            Dialogs.ClearDeleteConfirmation();
            return;
        }

        await RunWithErrorOverlayAsync(
            VexL.ErrorMessageCannotDeleteFormat,
            () => DeleteFileCoreAsync(path),
            path);
    }

    public async Task OpenFileLocationAsync()
    {
        if (DocumentInfo.CurrentFilePath is { Length: > 0 } path)
        {
            await OpenFileLocationCoreAsync(path);
        }
    }

    private Task RequestDeleteFileAsync(string path)
    {
        if (!IsCurrentDocumentPath(path))
        {
            Dialogs.ShowDeleteConfirmation(path);
            return Task.CompletedTask;
        }

        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeDeleting,
            _text.BeforeDeleting(_document.FileName),
            () =>
            {
                Dialogs.ShowDeleteConfirmation(path);
                return Task.CompletedTask;
            });
    }

    private async Task DeleteFileCoreAsync(string path)
    {
        var wasCurrentDocument = IsCurrentDocumentPath(path);
        if (wasCurrentDocument)
        {
            StopCurrentFileWatcher();
            _drafts.Clear(_document);
        }

        await _documentService.DeleteAsync(path);
        Recent.RemoveRecentDocument(path);
        RemoveDocumentFileFromList(path);
        Dialogs.ClearDeleteConfirmation();

        if (wasCurrentDocument)
        {
            NewDocumentCore();
            CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles));
        }
        else
        {
            var selected = FindCurrentDocumentFile();
            CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles, selected));
        }

        _text.PublishFileDeleted();
    }

    private Task OpenFileLocationCoreAsync(string path)
    {
        return RunWithErrorOverlayAsync(
            VexL.ErrorMessageCannotOpenLocationFormat,
            () => _documentService.OpenFileLocationAsync(path),
            path);
    }

    public async Task ReopenWithEncodingAsync(string? encodingName)
    {
        if (DocumentInfo.CurrentFilePath is not { Length: > 0 } path || string.IsNullOrWhiteSpace(encodingName))
        {
            _text.PublishOpenFileBeforeEncoding();
            return;
        }

        await RequestUnsavedConfirmationAsync(
            _text.TitleBeforeReopening,
            _text.BeforeReopeningWithEncoding(_document.FileName, encodingName),
            GuardedAction(
                VexL.ErrorMessageCannotOpenFileFormat,
                () => ReopenWithEncodingCoreAsync(path, encodingName),
                path));
    }

    private async Task ReopenWithEncodingCoreAsync(string path, string encodingName)
    {
        ApplyDocument(await _documentService.OpenPathAsync(path, encodingName));
        _text.PublishReopenedWithEncoding(encodingName);
    }

    private void ApplyDocument(DocumentSnapshot snapshot, bool updateMarkdown = true, bool restoreDraft = true)
    {
        if (!IsSameDocument(_document, snapshot))
        {
            _drafts.Clear(_document);
        }

        var restoredDraft = false;
        _document = restoreDraft ? RestoreDraftIfAvailable(snapshot, out restoredDraft) : snapshot;
        _lastSavedMarkdown = snapshot.Markdown;
        if (snapshot.FilePath is { Length: > 0 } path)
        {
            Recent.AddRecentDocument(path);
            SyncDocumentFileList(path);
        }

        if (updateMarkdown)
        {
            Markdown = _document.Markdown;
        }

        RefreshDocumentInfo();
        PublishWorkspaceDocumentState();
        StartCurrentFileWatcher();
        _text.PublishOpened(snapshot.FileName);
        if (restoredDraft)
        {
            _statusPublisher.PublishResource(VexL.StatusRecoveredAutoSaveDraft);
        }

        EditorActions.FocusEditor();
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        if (Markdown != command.Markdown)
        {
            SetMarkdown(command.Markdown, refreshImmediately: false);
        }
    }

    [EventHandler]
    public void ApplyDocumentFileOpenRequested(DocumentFileOpenRequestedCommand command) => _ = OpenDocumentFileAsync(command.File, command.PreviousSelection);

    [EventHandler]
    public void ApplyDocumentFileOpenLocationRequested(DocumentFileOpenLocationRequestedCommand command) => _ = OpenFileLocationCoreAsync(command.File.Path);

    [EventHandler]
    public void ApplyDocumentFileDeleteRequested(DocumentFileDeleteRequestedCommand command) => _ = RequestDeleteFileAsync(command.File.Path);

    [EventHandler]
    public void ApplyDocumentFileRenameRequested(DocumentFileRenameRequestedCommand command) => Dialogs.ShowRenameFilePanel(command.File.Path);

    [EventHandler]
    public void ApplyShellDroppedPath(ShellDroppedPathCommand command) => _ = OpenDroppedPathAsync(command.Path);

    [EventHandler]
    public void ApplyShellStartupArguments(ShellStartupArgumentsCommand command) => _ = OpenStartupDocumentAsync(command.Arguments);

    private void RefreshMarkdownDerivedState()
    {
        PublishWorkspaceDocumentState();
        RefreshDocumentInfo();
        CodeWF.EventBus.EventBus.Default.Publish(new OutlineItemsChangedCommand(_outlineService.BuildOutline(Markdown)));
    }

    private void RefreshDocumentState()
    {
        DocumentInfo.RefreshState(_document, Markdown, _lastSavedMarkdown);
    }

    private void ScheduleMarkdownDerivedStateRefresh()
    {
        _markdownDerivedStateTimer ??= new Timer(
            _ => Dispatcher.UIThread.Post(RefreshMarkdownDerivedState),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _markdownDerivedStateTimer.Change(MarkdownDerivedStateDebounce, Timeout.InfiniteTimeSpan);
    }

    private void CancelScheduledMarkdownDerivedStateRefresh()
    {
        _markdownDerivedStateTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void PublishWorkspaceDocumentState()
    {
        _workspaceDocumentState.UpdateDocument(Markdown, _document.FilePath);
    }

    private void RefreshDocumentInfo()
    {
        DocumentInfo.Refresh(_document, Markdown, _lastSavedMarkdown, _statisticsService.Count(Markdown));
        _drafts.QueueSave(_document, Markdown, _lastSavedMarkdown);
    }

    private void SyncDocumentFileList(string path)
    {
        var selected = _documentFiles.FirstOrDefault(file => PathsEqual(file.Path, path));
        if (selected is null)
        {
            selected = _documentFileFactory.Create(path);
            _documentFiles = [selected];
            CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles, selected));
            return;
        }

        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileSelectionChangedCommand(selected));
    }

    private DocumentFile? FindCurrentDocumentFile()
    {
        return _document.FilePath is { Length: > 0 } path
            ? _documentFiles.FirstOrDefault(file => PathsEqual(file.Path, path))
            : null;
    }

    private void RemoveDocumentFileFromList(string path)
    {
        _documentFiles = _documentFiles
            .Where(file => !PathsEqual(file.Path, path))
            .ToArray();
    }

    public void ShowProperties() => _documentUtilities.ShowProperties(Dialogs, DocumentInfo);
    public Task Export(string? format) => RunWithErrorOverlayAsync(
        VexL.ErrorMessageCannotExportFormat,
        () => _documentUtilities.ExportAsync(_document, Markdown, format),
        string.IsNullOrWhiteSpace(format) ? _document.FileName : format);

    public Task CopyHtml(string? target) => RunWithErrorOverlayAsync(
        VexL.ErrorMessageCannotCopyHtmlFormat,
        () => _documentUtilities.CopyHtmlAsync(_document, Markdown, target),
        _text.CopyTargetName(target));

    public Task Print() => RunWithErrorOverlayAsync(
        VexL.ErrorMessageCannotPrintFormat,
        () => _documentUtilities.PrintAsync(_document, Markdown));
    public async Task ConfirmRenameFileAsync()
    {
        if (Dialogs.PendingRenamePath is not { Length: > 0 } path)
        {
            Dialogs.ClearRenameFilePanel();
            return;
        }

        await RunWithErrorOverlayAsync(
            VexL.ErrorMessageCannotRenameFormat,
            () => RenameFileCoreAsync(path, Dialogs.RenameFileName),
            path);
    }

    public void WordCount() => _documentUtilities.WordCount(Dialogs, DocumentInfo.Statistics);
    public bool CloseFloatingPanel() => Dialogs.CloseFloatingPanel();
    public void ShowFindPanel() => FindBar.ShowFindPanel();
    public void ShowReplacePanel() => FindBar.ShowReplacePanel();
    public void CloseFindPanel() => FindBar.CloseFindPanel();
    public void FindNext() => FindBar.FindNext();
    public void ReplaceNext() => FindBar.ReplaceNext();
    public void ReplaceAll() => FindBar.ReplaceAll();

    public async Task OpenRecentDocumentAsync(int index)
    {
        if (!Recent.TryGetDocument(index, out var recent) || recent is null)
        {
            _text.PublishRecentFileUnavailable();
            return;
        }

        var target = _externalPaths.ResolveRecentPath(recent.Path);
        if (target is not { Kind: ShellExternalPathKind.File, Path: { Length: > 0 } path })
        {
            Recent.RemoveRecentDocument(recent.Path);
            _text.PublishRecentFileRemovedMissing();
            return;
        }

        await RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpeningRecent,
            _text.BeforeOpeningRecent(_document.FileName, recent.DisplayText),
            GuardedAction(
                VexL.ErrorMessageCannotOpenFileFormat,
                async () => ApplyDocument(await _documentService.OpenPathAsync(path)),
                path));
    }

    public Task BeginWindowCloseAsync()
    {
        _drafts.Flush();
        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeClosingVex,
            _text.BeforeClosingVex(_document.FileName),
            () =>
            {
                StopCurrentFileWatcher();
                _drafts.Clear(_document);
                CloseWindowRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            });
    }

    public Task SavePendingActionAsync() => _unsavedChanges.SavePendingActionAsync(SaveAsync, () => DocumentInfo.IsModified);

    public Task DiscardPendingActionAsync() => _unsavedChanges.DiscardPendingActionAsync();

    private static Task PublishAndComplete(Action publish)
    {
        publish();
        return Task.CompletedTask;
    }

    private Task RequestUnsavedConfirmationAsync(
        string title,
        string message,
        Func<Task> continuation,
        Action? cancellation = null) =>
        _unsavedChanges.RunAsync(
            title,
            message,
            DocumentInfo.IsModified,
            DocumentInfo.CurrentFilePath,
            continuation,
            cancellation);

    private Func<Task> GuardedAction(string messageResourceKey, Func<Task> action, params object?[] messageArgs)
    {
        return () => RunWithErrorOverlayAsync(messageResourceKey, action, messageArgs);
    }

    private async Task RunWithErrorOverlayAsync(string messageResourceKey, Func<Task> action, params object?[] messageArgs)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Dialogs.ShowError(messageResourceKey, exception, messageArgs);
        }
    }

    private async Task RenameFileCoreAsync(string path, string newName)
    {
        var wasCurrentDocument = IsCurrentDocumentPath(path);
        if (wasCurrentDocument)
        {
            StopCurrentFileWatcher();
        }

        var renamedPath = await _documentService.RenameAsync(path, newName);
        var renamedFile = ReplaceRenamedDocumentFile(path, renamedPath);
        Recent.RemoveRecentDocument(path);
        if (wasCurrentDocument)
        {
            _drafts.Clear(_document);
            _document = _document with
            {
                FilePath = renamedPath,
                FileName = Path.GetFileName(renamedPath)
            };
            Recent.AddRecentDocument(renamedPath);
            RefreshDocumentInfo();
            PublishWorkspaceDocumentState();
            StartCurrentFileWatcher();
        }

        Dialogs.ClearRenameFilePanel();
        var selectedFile = wasCurrentDocument ? renamedFile : FindCurrentDocumentFile() ?? renamedFile;
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles, selectedFile));
        _text.PublishRenamedFile(Path.GetFileName(renamedPath));
    }

    private DocumentFile ReplaceRenamedDocumentFile(string oldPath, string newPath)
    {
        var existing = _documentFiles.FirstOrDefault(file => PathsEqual(file.Path, oldPath));
        var renamed = existing is null
            ? _documentFileFactory.Create(newPath)
            : new DocumentFile(
                newPath,
                Path.GetFileName(newPath),
                existing.FolderName,
                existing.ModifiedText,
                existing.Preview);

        _documentFiles = _documentFiles.Count == 0
            ? [renamed]
            : _documentFiles
                .Select(file => PathsEqual(file.Path, oldPath) ? renamed : file)
                .ToArray();

        if (!_documentFiles.Any(file => PathsEqual(file.Path, newPath)))
        {
            _documentFiles = [.. _documentFiles, renamed];
        }

        return renamed;
    }

    private void StartCurrentFileWatcher()
    {
        StopCurrentFileWatcher();
        if (_document.FilePath is not { Length: > 0 } path || !File.Exists(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        _watchedFilePath = fullPath;
        _watchedFileLastWriteTimeUtc = TryGetLastWriteTimeUtc(fullPath);
        _lastSkippedExternalWriteTimeUtc = null;
        _currentFileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size
                           | NotifyFilters.CreationTime
        };
        _currentFileWatcher.Changed += HandleWatchedFileChanged;
        _currentFileWatcher.Created += HandleWatchedFileChanged;
        _currentFileWatcher.Deleted += HandleWatchedFileChanged;
        _currentFileWatcher.Renamed += HandleWatchedFileChanged;
        _currentFileWatcher.EnableRaisingEvents = true;
    }

    private void StopCurrentFileWatcher()
    {
        if (_currentFileWatcher is not null)
        {
            _currentFileWatcher.EnableRaisingEvents = false;
            _currentFileWatcher.Changed -= HandleWatchedFileChanged;
            _currentFileWatcher.Created -= HandleWatchedFileChanged;
            _currentFileWatcher.Deleted -= HandleWatchedFileChanged;
            _currentFileWatcher.Renamed -= HandleWatchedFileChanged;
            _currentFileWatcher.Dispose();
            _currentFileWatcher = null;
        }

        _currentFileChangeTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _watchedFilePath = null;
        _watchedFileLastWriteTimeUtc = null;
        _lastSkippedExternalWriteTimeUtc = null;
    }

    private void HandleWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_watchedFilePath is null)
        {
            return;
        }

        _currentFileChangeTimer ??= new Timer(
            _ => Dispatcher.UIThread.Post(() => _ = ReloadWatchedFileAsync()),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _currentFileChangeTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private async Task ReloadWatchedFileAsync()
    {
        if (_watchedFilePath is not { Length: > 0 } watchedPath || !IsCurrentDocumentPath(watchedPath))
        {
            return;
        }

        if (!File.Exists(watchedPath))
        {
            _statusPublisher.PublishResource(VexL.StatusCurrentFileUnavailable);
            return;
        }

        var writeTime = TryGetLastWriteTimeUtc(watchedPath);
        if (writeTime is not null
            && _watchedFileLastWriteTimeUtc is not null
            && writeTime.Value <= _watchedFileLastWriteTimeUtc.Value)
        {
            return;
        }

        if (DocumentInfo.IsModified)
        {
            if (writeTime is null || _lastSkippedExternalWriteTimeUtc != writeTime)
            {
                _lastSkippedExternalWriteTimeUtc = writeTime;
                _statusPublisher.PublishResource(VexL.StatusExternalFileChangedWithUnsavedEdits);
            }

            return;
        }

        await RunWithErrorOverlayAsync(
            VexL.ErrorMessageCannotOpenFileFormat,
            async () =>
            {
                var reloaded = await _documentService.ReloadAsync(_document);
                _document = reloaded;
                _lastSavedMarkdown = reloaded.Markdown;
                if (!MarkdownTextComparer.EqualsNormalizedLineEndings(Markdown, reloaded.Markdown))
                {
                    Markdown = reloaded.Markdown;
                }
                else
                {
                    RefreshDocumentInfo();
                }

                _watchedFileLastWriteTimeUtc = TryGetLastWriteTimeUtc(watchedPath);
                RefreshDocumentFileInList(watchedPath);
                _text.PublishExternalFileReloaded(reloaded.FileName);
            },
            watchedPath);
    }

    private void RefreshDocumentFileInList(string path)
    {
        var existing = _documentFiles.FirstOrDefault(file => PathsEqual(file.Path, path));
        if (existing is null)
        {
            var selected = _documentFileFactory.Create(path);
            _documentFiles = [selected];
            CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles, selected));
            return;
        }

        var refreshed = _documentFileFactory.Create(path);
        var file = new DocumentFile(
            refreshed.Path,
            refreshed.Name,
            existing.FolderName,
            refreshed.ModifiedText,
            refreshed.Preview);
        _documentFiles = _documentFiles
            .Select(item => PathsEqual(item.Path, path) ? file : item)
            .ToArray();
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFilesChangedCommand(_documentFiles, file));
    }

    private DocumentSnapshot RestoreDraftIfAvailable(DocumentSnapshot document, out bool restoredDraft)
    {
        var restored = _drafts.TryRestore(document);
        if (restored is null)
        {
            restoredDraft = false;
            return document;
        }

        restoredDraft = true;
        return restored;
    }

    private static bool IsSameDocument(DocumentSnapshot left, DocumentSnapshot right)
    {
        if (left.FilePath is { Length: > 0 } leftPath && right.FilePath is { Length: > 0 } rightPath)
        {
            return PathsEqual(leftPath, rightPath);
        }

        return string.IsNullOrWhiteSpace(left.FilePath) && string.IsNullOrWhiteSpace(right.FilePath);
    }

    private bool IsCurrentDocumentPath(string path)
    {
        return _document.FilePath is { Length: > 0 } currentPath
               && PathsEqual(currentPath, path);
    }

    private static DateTimeOffset? TryGetLastWriteTimeUtc(string path)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
