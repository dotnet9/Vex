using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject
{
    private readonly IDocumentService _documentService;
    private readonly IMarkdownExportService _exportService;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IMarkdownStatisticsService _statisticsService;
    private readonly IEventBus _eventBus;
    private DocumentSnapshot _document;
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
                "Save changes before opening a folder?",
                $"Save changes to {_document.FileName} before opening the dropped folder?",
                () => OpenFolderPathCoreAsync(path));
        }

        if (!File.Exists(path))
        {
            SetStatus("Dropped item is unavailable.");
            return Task.CompletedTask;
        }

        if (!IsSupportedDocumentPath(path))
        {
            SetStatus("Drop a Markdown or text file.");
            return Task.CompletedTask;
        }

        return RequestUnsavedConfirmationAsync(
            "Save changes before opening?",
            $"Save changes to {_document.FileName} before opening {Path.GetFileName(path)}?",
            () => OpenPathCoreAsync(path));
    }

    public string Markdown
    {
        get => _markdown;
        set
        {
            if (SetProperty(ref _markdown, value ?? string.Empty))
            {
                _document = _document with { Markdown = _markdown };
                RefreshMarkdownDerivedState();
            }
        }
    }

    private string? CurrentFilePath => DocumentInfo.CurrentFilePath;

    private bool IsModified => DocumentInfo.IsModified;

    public Task NewDocument()
    {
        return RequestUnsavedConfirmationAsync(
            "Save changes?",
            $"Save changes to {_document.FileName} before creating a new document?",
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
        SetStatus("New document created.");
        NotifyDocumentChanged();
        EditorActions.FocusEditor();
    }

    public Task CloseDocument()
    {
        return RequestUnsavedConfirmationAsync(
            "Save changes?",
            $"Save changes to {_document.FileName} before closing this document?",
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
        Navigation.ClearDocumentFiles();
        SetStatus("Document closed.");
        NotifyDocumentChanged();
        EditorActions.FocusEditor();
    }

    public void NewWindow()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true });
        }
    }

    public async Task OpenAsync()
    {
        await RequestUnsavedConfirmationAsync(
            "Save changes before opening?",
            $"Save changes to {_document.FileName} before opening another file?",
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
        if (Navigation.HasDocumentFiles)
        {
            Navigation.SelectedSideTabIndex = 0;
            SetStatus("Choose a document from the loaded folder.");
            return;
        }

        await OpenAsync();
    }

    public async Task OpenFolderAsync()
    {
        await RequestUnsavedConfirmationAsync(
            "Save changes before opening a folder?",
            $"Save changes to {_document.FileName} before opening a folder?",
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
        Navigation.SetDocumentFiles(files);

        SetStatus(files.Count == 0 ? "No markdown files loaded." : $"Loaded {files.Count} markdown files.");
        if (files.Count > 0)
        {
            Navigation.SelectDocumentFileSilently(files[0]);
            if (bypassUnsavedPrompt)
            {
                await OpenDocumentFileCoreAsync(files[0]);
            }
            else
            {
                await OpenDocumentFileAsync(files[0], null);
            }
        }
    }

    private async Task OpenDocumentFileAsync(DocumentFile file, DocumentFile? previousSelection)
    {
        if (CurrentFilePath?.Equals(file.Path, StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        await RequestUnsavedConfirmationAsync(
            "Save changes before switching files?",
            $"Save changes to {_document.FileName} before opening {file.Name}?",
            () => OpenDocumentFileCoreAsync(file),
            () => Navigation.RestoreSelectedDocumentFile(previousSelection));
    }

    private async Task OpenDocumentFileCoreAsync(DocumentFile file)
    {
        var snapshot = await _documentService.OpenPathAsync(file.Path);
        ApplyDocument(snapshot);
    }

    public Task OpenRecentDocument1Async() => OpenRecentDocumentAsync(0);

    public Task OpenRecentDocument2Async() => OpenRecentDocumentAsync(1);

    public Task OpenRecentDocument3Async() => OpenRecentDocumentAsync(2);

    public Task OpenRecentDocument4Async() => OpenRecentDocumentAsync(3);

    public Task OpenRecentDocument5Async() => OpenRecentDocumentAsync(4);

    public async Task SaveAsync()
    {
        var saved = await _documentService.SaveAsync(_document with { Markdown = Markdown });
        if (saved is not null)
        {
            ApplyDocument(saved, false);
            SetStatus($"Saved {saved.FileName}.");
        }
    }

    public async Task SaveAsAsync()
    {
        var saved = await _documentService.SaveAsAsync(_document with { Markdown = Markdown });
        if (saved is not null)
        {
            ApplyDocument(saved, false);
            SetStatus($"Saved as {saved.FileName}.");
        }
    }

    public Task DeleteAsync()
    {
        if (CurrentFilePath is not { Length: > 0 } path)
        {
            return Task.CompletedTask;
        }

        return RequestUnsavedConfirmationAsync(
            "Save changes before deleting?",
            $"Save changes to {_document.FileName} before deleting it?",
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
        SetStatus("File deleted.");
    }

    public async Task OpenFileLocationAsync()
    {
        if (CurrentFilePath is { Length: > 0 } path)
        {
            await _documentService.OpenFileLocationAsync(path);
        }
    }

    public async Task ReopenWithEncodingAsync(string? encodingName)
    {
        if (CurrentFilePath is not { Length: > 0 } path || string.IsNullOrWhiteSpace(encodingName))
        {
            SetStatus("Open a file before choosing an encoding.");
            return;
        }

        await RequestUnsavedConfirmationAsync(
            "Save changes before reopening?",
            $"Save changes to {_document.FileName} before reopening it with {encodingName}?",
            () => ReopenWithEncodingCoreAsync(path, encodingName));
    }

    private async Task ReopenWithEncodingCoreAsync(string path, string encodingName)
    {
        ApplyDocument(await _documentService.OpenPathAsync(path, encodingName));
        SetStatus($"Reopened with {encodingName}.");
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

        NotifyDocumentChanged();
        SetStatus($"Opened {snapshot.FileName}.");
        EditorActions.FocusEditor();
    }

    private void NotifyDocumentChanged()
    {
        RefreshDocumentInfo();
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
        Navigation.SetOutlineItems(_outlineService.BuildOutline(Markdown));
    }

    private void RefreshDocumentInfo()
    {
        DocumentInfo.Refresh(_document, Markdown, _lastSavedMarkdown, _statisticsService.Count(Markdown));
    }

    public void ShowProperties()
    {
        Dialogs.ShowPropertiesPanel();
        SetStatus($"{DocumentInfo.CurrentDocumentTitle} | {DocumentInfo.DocumentStateText} | {DocumentInfo.CurrentEncodingText} | {DocumentInfo.PropertySizeText} | {DocumentInfo.PropertyLocationText}");
    }

    public async Task Export(string? format)
    {
        if (format?.Equals("HTML", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportHtmlAsync(_document with { Markdown = Markdown });
            SetStatus(path is null ? "HTML export canceled." : $"Exported HTML to {Path.GetFileName(path)}.");
            return;
        }

        SetStatus($"Export {format ?? "document"} is queued for implementation.");
    }

    public async Task Print()
    {
        var path = await _exportService.OpenHtmlPrintPreviewAsync(_document with { Markdown = Markdown });
        SetStatus(path is null ? "Print preview canceled." : "Opened HTML print preview.");
    }

    public void WordCount()
    {
        Dialogs.ShowStatisticsPanel();
        SetStatus($"Words {DocumentInfo.Statistics.Words}, Characters {DocumentInfo.Statistics.Characters}, Lines {DocumentInfo.Statistics.Lines}.");
    }

    public bool CloseFloatingPanel()
    {
        return Dialogs.CloseFloatingPanel();
    }

    public void ShowFindPanel()
    {
        FindBar.ShowFindPanel();
    }

    public void ShowReplacePanel()
    {
        FindBar.ShowReplacePanel();
    }

    public void CloseFindPanel()
    {
        FindBar.CloseFindPanel();
    }

    public void FindNext()
    {
        FindBar.FindNext();
    }

    public void ReplaceNext()
    {
        FindBar.ReplaceNext();
    }

    public void ReplaceAll()
    {
        FindBar.ReplaceAll();
    }

    private void SetStatus(string message)
    {
        _eventBus.Publish(new WorkspaceStatusChangedCommand(message));
    }

    private async Task OpenRecentDocumentAsync(int index)
    {
        if (!Recent.TryGetDocument(index, out var recent) || recent is null)
        {
            SetStatus("Recent file is unavailable.");
            return;
        }

        if (!File.Exists(recent.Path))
        {
            Recent.RemoveRecentDocument(recent.Path);
            SetStatus("Recent file was removed because it no longer exists.");
            return;
        }

        await RequestUnsavedConfirmationAsync(
            "Save changes before opening recent file?",
            $"Save changes to {_document.FileName} before opening {recent.DisplayText}?",
            async () => ApplyDocument(await _documentService.OpenPathAsync(recent.Path)));
    }

    public Task BeginWindowCloseAsync()
    {
        return RequestUnsavedConfirmationAsync(
            "Save changes before closing Vex?",
            $"Save changes to {_document.FileName} before closing Vex?",
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
        if (IsModified)
        {
            SetStatus("Save canceled. Action was not completed.");
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
        if (!IsModified)
        {
            await continuation();
            return;
        }

        Dialogs.ShowUnsavedConfirmation(
            title,
            message,
            DocumentInfo.CurrentFilePath ?? "Unsaved document",
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

    private static bool IsSupportedDocumentPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
