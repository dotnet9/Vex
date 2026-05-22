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
    private readonly IMarkdownExportService _exportService;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IMarkdownStatisticsService _statisticsService;
    private readonly IEventBus _eventBus;
    private readonly IShellDocumentWorkflowText _text;
    private DocumentSnapshot _document;
    private IReadOnlyList<DocumentFile> _documentFiles = [];
    private string _lastSavedMarkdown = string.Empty;
    private string _markdown = string.Empty;

    public MainWindowViewModel(
        IDocumentService documentService,
        IMarkdownExportService exportService,
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
        IEventBus eventBus)
    {
        _documentService = documentService;
        _exportService = exportService;
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
        var path = arguments.FirstOrDefault(argument => File.Exists(argument) || Directory.Exists(argument));
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            await ApplyDocumentFilesAsync(await _documentService.OpenFolderPathAsync(path), true);
            return;
        }

        ApplyDocument(await _documentService.OpenPathAsync(path));
    }

    public Task OpenDroppedPathAsync(string path)
    {
        if (Directory.Exists(path))
        {
            return RequestUnsavedConfirmationAsync(
                _text.TitleBeforeOpeningFolder,
                _text.BeforeOpeningDroppedFolder(_document.FileName),
                () => OpenFolderPathCoreAsync(path));
        }

        if (!File.Exists(path))
        {
            _text.PublishDroppedItemUnavailable();
            return Task.CompletedTask;
        }

        if (!_documentService.IsSupportedDocumentPath(path))
        {
            _text.PublishDropMarkdownOrTextFile();
            return Task.CompletedTask;
        }

        return RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpening,
            _text.BeforeOpeningFile(_document.FileName, Path.GetFileName(path)),
            () => OpenPathCoreAsync(path));
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
            SelectSidebarTab(0);
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
            () => RestoreDocumentFileSelection(previousSelection));
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
        else
        {
            RefreshDocumentInfo();
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
        RefreshDocumentInfo();
        _eventBus.Publish(new OutlineItemsChangedCommand(_outlineService.BuildOutline(Markdown)));
    }

    private void RefreshDocumentInfo()
    {
        DocumentInfo.Refresh(_document, Markdown, _lastSavedMarkdown, _statisticsService.Count(Markdown));
    }

    public void ShowProperties()
    {
        Dialogs.ShowPropertiesPanel();
        _text.PublishPropertiesSummary(
            DocumentInfo.CurrentDocumentTitle,
            DocumentInfo.DocumentStateText,
            DocumentInfo.CurrentEncodingText,
            DocumentInfo.PropertySizeText,
            DocumentInfo.PropertyLocationText);
    }

    public async Task Export(string? format)
    {
        if (format?.Equals("HTML", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportHtmlAsync(_document with { Markdown = Markdown });
            if (path is null)
            {
                _text.PublishHtmlExportCanceled();
            }
            else
            {
                _text.PublishExportedHtmlTo(Path.GetFileName(path));
            }

            return;
        }

        _text.PublishExportNotImplemented(format?.ToUpperInvariant() ?? "Document");
    }

    public async Task Print()
    {
        var path = await _exportService.OpenHtmlPrintPreviewAsync(_document with { Markdown = Markdown });
        _text.PublishPrintPreviewResult(path is null);
    }

    public void WordCount()
    {
        Dialogs.ShowStatisticsPanel();
        _text.PublishStatisticsSummary(DocumentInfo.Statistics);
    }

    public bool CloseFloatingPanel() => Dialogs.CloseFloatingPanel();

    public void ShowFindPanel() => FindBar.ShowFindPanel();

    public void ShowReplacePanel() => FindBar.ShowReplacePanel();

    public void CloseFindPanel() => FindBar.CloseFindPanel();

    public void FindNext() => FindBar.FindNext();

    public void ReplaceNext() => FindBar.ReplaceNext();

    public void ReplaceAll() => FindBar.ReplaceAll();

    private void SelectSidebarTab(int selectedIndex)
    {
        _eventBus.Publish(new ShellSidebarTabSelectedCommand(selectedIndex));
    }

    private void RestoreDocumentFileSelection(DocumentFile? previousSelection)
    {
        _eventBus.Publish(new DocumentFileSelectionChangedCommand(previousSelection));
    }

    public async Task OpenRecentDocumentAsync(int index)
    {
        if (!Recent.TryGetDocument(index, out var recent) || recent is null)
        {
            _text.PublishRecentFileUnavailable();
            return;
        }

        if (!File.Exists(recent.Path))
        {
            Recent.RemoveRecentDocument(recent.Path);
            _text.PublishRecentFileRemovedMissing();
            return;
        }

        await RequestUnsavedConfirmationAsync(
            _text.TitleBeforeOpeningRecent,
            _text.BeforeOpeningRecent(_document.FileName, recent.DisplayText),
            async () => ApplyDocument(await _documentService.OpenPathAsync(recent.Path)));
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

    public async Task SavePendingActionAsync()
    {
        if (!Dialogs.HasPendingUnsavedAction)
        {
            return;
        }

        await SaveAsync();
        if (DocumentInfo.IsModified)
        {
            _text.PublishSaveCanceledActionIncomplete();
            return;
        }

        await ContinuePendingActionAsync();
    }

    public Task DiscardPendingActionAsync()
    {
        return ContinuePendingActionAsync();
    }

    private async Task RequestUnsavedConfirmationAsync(
        string title,
        string message,
        Func<Task> continuation,
        Action? cancellation = null)
    {
        if (!DocumentInfo.IsModified)
        {
            await continuation();
            return;
        }

        Dialogs.ShowUnsavedConfirmation(
            title,
            message,
            DocumentInfo.CurrentFilePath ?? _text.UnsavedDocumentFallback,
            continuation,
            cancellation);
    }

    private async Task ContinuePendingActionAsync()
    {
        var continuation = Dialogs.TakePendingUnsavedContinuation();
        if (continuation is not null)
        {
            await continuation();
        }
    }

}
