using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject
{
    private readonly IDocumentService _documentService;
    private readonly IWorkspaceDocumentState _workspaceDocumentState;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IMarkdownStatisticsService _statisticsService;
    private readonly IDocumentFileFactory _documentFileFactory;
    private readonly IEventBus _eventBus;
    private readonly IShellDocumentWorkflowText _text;
    private readonly IShellUnsavedChangesGuard _unsavedChanges;
    private readonly IShellDocumentUtilityActions _documentUtilities;
    private readonly IShellExternalPathResolver _externalPaths;
    private readonly IAutoSaveDraftService _drafts;
    private readonly IShellStatusPublisher _statusPublisher;
    private DocumentSnapshot _document;
    private IReadOnlyList<DocumentFile> _documentFiles = [];
    private string _lastSavedMarkdown = string.Empty;
    private string _markdown = string.Empty;

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
        IShellStatusPublisher statusPublisher,
        IEventBus eventBus)
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
        _eventBus = eventBus;
        _eventBus.Subscribe(this);

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
        set
        {
            var normalized = value ?? string.Empty;
            if (_markdown != normalized)
            {
                this.RaiseAndSetIfChanged(ref _markdown, normalized);
                _document = _document with { Markdown = _markdown };
                RefreshMarkdownDerivedState();
            }
        }
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
        _drafts.Clear(_document);
        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
        _text.PublishNewDocumentCreated();
        RefreshDocumentInfo();
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
        _drafts.Clear(_document);
        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
        _documentFiles = [];
        _eventBus.Publish(new DocumentFilesChangedCommand(_documentFiles));
        _text.PublishDocumentClosed();
        RefreshDocumentInfo();
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
            _eventBus.Publish(new ShellSidebarTabSelectedCommand(0));
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
        _eventBus.Publish(new DocumentFilesChangedCommand(_documentFiles, firstFile));

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
            () => _eventBus.Publish(new DocumentFileSelectionChangedCommand(previousSelection)));
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

        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeDeleting,
            _text.BeforeDeleting(_document.FileName),
            () =>
            {
                Dialogs.ShowDeleteConfirmation(path);
                return Task.CompletedTask;
            });
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
            async () =>
            {
                await _documentService.DeleteAsync(path);
                _drafts.Clear(_document);
                Recent.RemoveRecentDocument(path);
                Dialogs.ClearDeleteConfirmation();
                NewDocumentCore();
                _text.PublishFileDeleted();
            },
            path);
    }

    public async Task OpenFileLocationAsync()
    {
        if (DocumentInfo.CurrentFilePath is { Length: > 0 } path)
        {
            await RunWithErrorOverlayAsync(
                VexL.ErrorMessageCannotOpenLocationFormat,
                () => _documentService.OpenFileLocationAsync(path),
                path);
        }
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
            Markdown = command.Markdown;
        }
    }

    [EventHandler]
    public void ApplyDocumentFileOpenRequested(DocumentFileOpenRequestedCommand command) => _ = OpenDocumentFileAsync(command.File, command.PreviousSelection);

    [EventHandler]
    public void ApplyShellDroppedPath(ShellDroppedPathCommand command) => _ = OpenDroppedPathAsync(command.Path);

    [EventHandler]
    public void ApplyShellStartupArguments(ShellStartupArgumentsCommand command) => _ = OpenStartupDocumentAsync(command.Arguments);

    private void RefreshMarkdownDerivedState()
    {
        _workspaceDocumentState.UpdateMarkdown(Markdown);
        RefreshDocumentInfo();
        _eventBus.Publish(new OutlineItemsChangedCommand(_outlineService.BuildOutline(Markdown)));
    }

    private void RefreshDocumentInfo()
    {
        DocumentInfo.Refresh(_document, Markdown, _lastSavedMarkdown, _statisticsService.Count(Markdown));
        _drafts.QueueSave(_document, Markdown, _lastSavedMarkdown);
    }

    private void SyncDocumentFileList(string path)
    {
        var selected = _documentFiles.FirstOrDefault(file => file.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            selected = _documentFileFactory.Create(path);
            _documentFiles = [selected];
            _eventBus.Publish(new DocumentFilesChangedCommand(_documentFiles, selected));
            return;
        }

        _eventBus.Publish(new DocumentFileSelectionChangedCommand(selected));
    }

    public void ShowProperties() => _documentUtilities.ShowProperties(Dialogs, DocumentInfo);
    public Task Export(string? format) => RunWithErrorOverlayAsync(
        VexL.ErrorMessageCannotExportFormat,
        () => _documentUtilities.ExportAsync(_document, Markdown, format),
        string.IsNullOrWhiteSpace(format) ? _document.FileName : format);

    public Task Print() => RunWithErrorOverlayAsync(
        VexL.ErrorMessageCannotPrintFormat,
        () => _documentUtilities.PrintAsync(_document, Markdown));
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
            return leftPath.Equals(rightPath, StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(left.FilePath) && string.IsNullOrWhiteSpace(right.FilePath);
    }
}
