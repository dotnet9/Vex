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
    private readonly IEventBus _eventBus;
    private readonly IShellDocumentWorkflowText _text;
    private readonly IShellUnsavedChangesGuard _unsavedChanges;
    private readonly IShellDocumentUtilityActions _documentUtilities;
    private readonly IShellExternalPathResolver _externalPaths;
    private DocumentSnapshot _document;
    private IReadOnlyList<DocumentFile> _documentFiles = [];
    private string _lastSavedMarkdown = string.Empty;
    private string _markdown = string.Empty;

    public MainWindowViewModel(
        IDocumentService documentService,
        IWorkspaceDocumentState workspaceDocumentState,
        IMarkdownOutlineService outlineService,
        IMarkdownStatisticsService statisticsService,
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
        IEventBus eventBus)
    {
        _documentService = documentService;
        _workspaceDocumentState = workspaceDocumentState;
        _outlineService = outlineService;
        _statisticsService = statisticsService;
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
        _eventBus = eventBus;
        _eventBus.Subscribe(this);

        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
        RefreshDocumentInfo();
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
            await ApplyDocumentFilesAsync(await _documentService.OpenFolderPathAsync(path), true);
            return;
        }

        if (target.Kind == ShellExternalPathKind.File)
        {
            ApplyDocument(await _documentService.OpenPathAsync(path));
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
                () => OpenFolderPathCoreAsync(resolvedPath));
        }

        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpening,
            _text.BeforeOpeningFile(_document.FileName, target.FileName ?? resolvedPath),
            () => OpenPathCoreAsync(resolvedPath));
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
            OpenAsyncCore);
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
            OpenFolderAsyncCore);
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
            () => OpenDocumentFileCoreAsync(file),
            () => _eventBus.Publish(new DocumentFileSelectionChangedCommand(previousSelection)));
    }

    private async Task OpenDocumentFileCoreAsync(DocumentFile file)
    {
        var snapshot = await _documentService.OpenPathAsync(file.Path);
        ApplyDocument(snapshot);
    }

    public async Task SaveAsync()
    {
        var saved = await _documentService.SaveAsync(_document with { Markdown = Markdown });
        if (saved is not null)
        {
            ApplyDocument(saved, false);
            _text.PublishSaved(saved.FileName);
        }
    }

    public async Task SaveAsAsync()
    {
        var saved = await _documentService.SaveAsAsync(_document with { Markdown = Markdown });
        if (saved is not null)
        {
            ApplyDocument(saved, false);
            _text.PublishSavedAs(saved.FileName);
        }
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

        await _documentService.DeleteAsync(path);
        Recent.RemoveRecentDocument(path);
        Dialogs.ClearDeleteConfirmation();
        NewDocumentCore();
        _text.PublishFileDeleted();
    }

    public async Task OpenFileLocationAsync()
    {
        if (DocumentInfo.CurrentFilePath is { Length: > 0 } path)
        {
            await _documentService.OpenFileLocationAsync(path);
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
            () => ReopenWithEncodingCoreAsync(path, encodingName));
    }

    private async Task ReopenWithEncodingCoreAsync(string path, string encodingName)
    {
        ApplyDocument(await _documentService.OpenPathAsync(path, encodingName));
        _text.PublishReopenedWithEncoding(encodingName);
    }

    private void ApplyDocument(DocumentSnapshot snapshot, bool updateMarkdown = true)
    {
        _document = snapshot;
        _lastSavedMarkdown = snapshot.Markdown;
        if (snapshot.FilePath is { Length: > 0 } path)
        {
            Recent.AddRecentDocument(path);
        }

        if (updateMarkdown)
        {
            Markdown = snapshot.Markdown;
        }

        RefreshDocumentInfo();
        _text.PublishOpened(snapshot.FileName);
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
    public void ApplyDocumentFileOpenRequested(DocumentFileOpenRequestedCommand command)
    {
        _ = OpenDocumentFileAsync(command.File, command.PreviousSelection);
    }

    private void RefreshMarkdownDerivedState()
    {
        _workspaceDocumentState.UpdateMarkdown(Markdown);
        RefreshDocumentInfo();
        _eventBus.Publish(new OutlineItemsChangedCommand(_outlineService.BuildOutline(Markdown)));
    }

    private void RefreshDocumentInfo()
    {
        DocumentInfo.Refresh(_document, Markdown, _lastSavedMarkdown, _statisticsService.Count(Markdown));
    }

    public void ShowProperties() => _documentUtilities.ShowProperties(Dialogs, DocumentInfo);
    public Task Export(string? format) => _documentUtilities.ExportAsync(_document, Markdown, format);
    public Task Print() => _documentUtilities.PrintAsync(_document, Markdown);
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
            async () => ApplyDocument(await _documentService.OpenPathAsync(path)));
    }

    public Task BeginWindowCloseAsync()
    {
        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeClosingVex,
            _text.BeforeClosingVex(_document.FileName),
            () =>
            {
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
}
