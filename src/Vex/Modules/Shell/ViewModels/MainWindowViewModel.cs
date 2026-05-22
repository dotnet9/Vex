using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject
{
    private const int MaxRecentDocuments = 5;
    private readonly IDocumentService _documentService;
    private readonly IMarkdownExportService _exportService;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IMarkdownStatisticsService _statisticsService;
    private readonly IHelpService _helpService;
    private readonly IEventBus _eventBus;
    private DocumentSnapshot _document;
    private string _lastSavedMarkdown = string.Empty;
    private string _markdown = string.Empty;
    private string _statusText = "Ready";
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private bool _isSidebarVisible = true;
    private bool _isStatusBarVisible = true;
    private bool _isPreviewVisible = true;
    private bool _isAlwaysOnTop;
    private bool _isFullScreen;
    private bool _isSourceMode;
    private bool _sidebarBeforeSourceMode = true;
    private bool _previewBeforeSourceMode = true;
    private bool _isStatisticsPanelVisible;
    private bool _isAboutPanelVisible;
    private bool _isPropertiesPanelVisible;
    private bool _isDeleteConfirmVisible;
    private bool _isUnsavedConfirmVisible;
    private string? _pendingDeletePath;
    private Func<Task>? _pendingUnsavedContinuation;
    private Action? _pendingUnsavedCancellation;
    private string _unsavedConfirmTitle = "Save changes?";
    private string _unsavedConfirmMessage = "Save changes before continuing?";
    private string _unsavedConfirmPath = "Unsaved document";
    private double _editorZoom = 1.0;
    private MarkdownStatistics _statistics = new(0, 0, 1);
    private DocumentFile? _selectedDocumentFile;
    private OutlineItem? _selectedOutlineItem;

    public MainWindowViewModel(
        IDocumentService documentService,
        IMarkdownExportService exportService,
        IMarkdownOutlineService outlineService,
        IMarkdownStatisticsService statisticsService,
        IHelpService helpService,
        ShellAppearanceViewModel appearance,
        ShellFindBarViewModel findBar,
        IEventBus eventBus)
    {
        _documentService = documentService;
        _exportService = exportService;
        _outlineService = outlineService;
        _statisticsService = statisticsService;
        _helpService = helpService;
        Appearance = appearance;
        FindBar = findBar;
        _eventBus = eventBus;
        _eventBus.Subscribe(this);
        LoadRecentDocuments();

        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
    }

    public event EventHandler? CloseWindowRequested;

    public ShellAppearanceViewModel Appearance { get; }

    public ShellFindBarViewModel FindBar { get; }

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

    public ObservableCollection<DocumentFile> DocumentFiles { get; } = [];

    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];

    public ObservableCollection<RecentDocument> RecentDocuments { get; } = [];

    public string Markdown
    {
        get => _markdown;
        set
        {
            if (SetProperty(ref _markdown, value ?? string.Empty))
            {
                _document = _document with { Markdown = _markdown };
                RefreshMarkdownDerivedState();
                NotifyDocumentStateChanged();
            }
        }
    }

    public string WindowTitle => $"{(IsModified ? "*" : string.Empty)}{_document.FileName} - Vex";

    public string CurrentDocumentTitle => $"{(IsModified ? "*" : string.Empty)}{_document.FileName}";

    public string? CurrentFilePath => _document.FilePath;

    public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFilePath);

    public bool HasRecentDocuments => RecentDocuments.Count > 0;

    public bool HasDocumentFiles => DocumentFiles.Count > 0;

    public bool IsDocumentFilesEmpty => !HasDocumentFiles;

    public bool HasOutlineItems => OutlineItems.Count > 0;

    public bool IsOutlineEmpty => !HasOutlineItems;

    public bool HasRecentDocument1 => RecentDocuments.Count > 0;

    public bool HasRecentDocument2 => RecentDocuments.Count > 1;

    public bool HasRecentDocument3 => RecentDocuments.Count > 2;

    public bool HasRecentDocument4 => RecentDocuments.Count > 3;

    public bool HasRecentDocument5 => RecentDocuments.Count > 4;

    public string RecentDocument1Text => GetRecentDocumentText(0);

    public string RecentDocument2Text => GetRecentDocumentText(1);

    public string RecentDocument3Text => GetRecentDocumentText(2);

    public string RecentDocument4Text => GetRecentDocumentText(3);

    public string RecentDocument5Text => GetRecentDocumentText(4);

    public bool IsModified => !string.Equals(Markdown, _lastSavedMarkdown, StringComparison.Ordinal);

    public string DocumentStateText => IsModified ? "Modified" : "Saved";

    public string CurrentEncodingText => GetEncodingDisplayName(_document.Encoding);

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public int CaretLine
    {
        get => _caretLine;
        set => SetProperty(ref _caretLine, value);
    }

    public int CaretColumn
    {
        get => _caretColumn;
        set => SetProperty(ref _caretColumn, value);
    }

    public int SelectedSideTabIndex
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsSidebarVisible
    {
        get => _isSidebarVisible;
        set
        {
            if (SetProperty(ref _isSidebarVisible, value))
            {
                OnPropertyChanged(nameof(SidebarColumnWidth));
                OnPropertyChanged(nameof(SidebarSplitterWidth));
            }
        }
    }

    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set => SetProperty(ref _isStatusBarVisible, value);
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (SetProperty(ref _isPreviewVisible, value))
            {
                OnPropertyChanged(nameof(PreviewColumnWidth));
                OnPropertyChanged(nameof(PreviewSplitterWidth));
            }
        }
    }

    public GridLength SidebarColumnWidth => IsSidebarVisible ? new GridLength(320) : new GridLength(0);

    public GridLength SidebarSplitterWidth => IsSidebarVisible ? new GridLength(6) : new GridLength(0);

    public GridLength PreviewColumnWidth => IsPreviewVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength PreviewSplitterWidth => IsPreviewVisible ? new GridLength(6) : new GridLength(0);

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetProperty(ref _isAlwaysOnTop, value);
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set => SetProperty(ref _isFullScreen, value);
    }

    public bool IsSourceMode
    {
        get => _isSourceMode;
        set => SetProperty(ref _isSourceMode, value);
    }

    public bool IsStatisticsPanelVisible
    {
        get => _isStatisticsPanelVisible;
        set => SetProperty(ref _isStatisticsPanelVisible, value);
    }

    public bool IsAboutPanelVisible
    {
        get => _isAboutPanelVisible;
        set => SetProperty(ref _isAboutPanelVisible, value);
    }

    public bool IsPropertiesPanelVisible
    {
        get => _isPropertiesPanelVisible;
        set => SetProperty(ref _isPropertiesPanelVisible, value);
    }

    public bool IsDeleteConfirmVisible
    {
        get => _isDeleteConfirmVisible;
        set => SetProperty(ref _isDeleteConfirmVisible, value);
    }

    public bool IsUnsavedConfirmVisible
    {
        get => _isUnsavedConfirmVisible;
        set => SetProperty(ref _isUnsavedConfirmVisible, value);
    }

    public string DeleteConfirmText => _pendingDeletePath is { Length: > 0 }
        ? $"Delete {Path.GetFileName(_pendingDeletePath)}?"
        : "Delete current file?";

    public string DeleteConfirmPath => _pendingDeletePath ?? string.Empty;

    public string UnsavedConfirmTitle => _unsavedConfirmTitle;

    public string UnsavedConfirmMessage => _unsavedConfirmMessage;

    public string UnsavedConfirmPath => _unsavedConfirmPath;

    public double EditorZoom
    {
        get => _editorZoom;
        set
        {
            if (SetProperty(ref _editorZoom, value))
            {
                OnPropertyChanged(nameof(EditorFontSize));
                OnPropertyChanged(nameof(ZoomText));
            }
        }
    }

    public double EditorFontSize => Math.Round(15 * EditorZoom, 1);

    public string ZoomText => $"{EditorZoom:P0}";

    public MarkdownStatistics Statistics
    {
        get => _statistics;
        set
        {
            if (SetProperty(ref _statistics, value))
            {
                OnPropertyChanged(nameof(WordCountText));
                OnPropertyChanged(nameof(CharacterCountText));
                OnPropertyChanged(nameof(LineCountText));
            }
        }
    }

    public string WordCountText => $"{Statistics.Words} words";

    public string CharacterCountText => $"{Statistics.Characters} chars";

    public string LineCountText => $"{Statistics.Lines} lines";

    public string PropertyNameText => _document.FileName;

    public string PropertyLocationText => CurrentFilePath ?? "Unsaved document";

    public string PropertySizeText => CurrentFilePath is { Length: > 0 } path && File.Exists(path)
        ? FormatFileSize(new FileInfo(path).Length)
        : $"{Encoding.UTF8.GetByteCount(Markdown):N0} B";

    public DocumentFile? SelectedDocumentFile
    {
        get => _selectedDocumentFile;
        set
        {
            var previousSelection = _selectedDocumentFile;
            if (SetProperty(ref _selectedDocumentFile, value) && value is not null)
            {
                _ = OpenDocumentFileAsync(value, previousSelection);
            }
        }
    }

    public OutlineItem? SelectedOutlineItem
    {
        get => _selectedOutlineItem;
        set
        {
            if (SetProperty(ref _selectedOutlineItem, value) && value is not null)
            {
                SelectedSideTabIndex = 1;
                _eventBus.Publish(new NavigateToLineCommand(value.Line));
                SetStatus($"Navigated to {value.Title}.");
            }
        }
    }

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
        PublishEditorAction(EditorActionKind.FocusEditor);
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
        DocumentFiles.Clear();
        SelectedDocumentFile = null;
        SelectedOutlineItem = null;
        NotifyDocumentFilesChanged();
        SetStatus("Document closed.");
        NotifyDocumentChanged();
        PublishEditorAction(EditorActionKind.FocusEditor);
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
        if (DocumentFiles.Count > 0)
        {
            SelectedSideTabIndex = 0;
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
        DocumentFiles.Clear();
        foreach (var file in files)
        {
            DocumentFiles.Add(file);
        }
        NotifyDocumentFilesChanged();

        SetStatus(files.Count == 0 ? "No markdown files loaded." : $"Loaded {files.Count} markdown files.");
        if (files.Count > 0)
        {
            SetProperty(ref _selectedDocumentFile, files[0], nameof(SelectedDocumentFile));
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
            () => RestoreSelectedDocumentFile(previousSelection));
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

    public void ClearRecentDocuments()
    {
        RecentDocuments.Clear();
        SaveRecentDocuments();
        NotifyRecentDocumentsChanged();
        SetStatus("Recent files cleared.");
    }

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
                ShowDeleteConfirmation(path);
                return Task.CompletedTask;
            });
    }

    private void ShowDeleteConfirmation(string path)
    {
        _pendingDeletePath = path;
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(DeleteConfirmPath));
        IsDeleteConfirmVisible = true;
    }

    public async Task ConfirmDeleteAsync()
    {
        if (_pendingDeletePath is not { Length: > 0 } path)
        {
            IsDeleteConfirmVisible = false;
            return;
        }

        await _documentService.DeleteAsync(path);
        RemoveRecentDocument(path);
        IsDeleteConfirmVisible = false;
        _pendingDeletePath = null;
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(DeleteConfirmPath));
        NewDocumentCore();
        SetStatus("File deleted.");
    }

    public void CancelDelete()
    {
        _pendingDeletePath = null;
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(DeleteConfirmPath));
        IsDeleteConfirmVisible = false;
        SetStatus("Delete canceled.");
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
            AddRecentDocument(path);
        }

        if (updateMarkdown)
        {
            Markdown = snapshot.Markdown;
        }
        else
        {
            NotifyDocumentStateChanged();
        }

        NotifyDocumentChanged();
        SetStatus($"Opened {snapshot.FileName}.");
        PublishEditorAction(EditorActionKind.FocusEditor);
    }

    private void NotifyDocumentChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(HasCurrentFile));
        OnPropertyChanged(nameof(CurrentEncodingText));
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(PropertyLocationText));
        OnPropertyChanged(nameof(PropertySizeText));
        NotifyDocumentStateChanged();
    }

    private void NotifyDocumentStateChanged()
    {
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(DocumentStateText));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(PropertySizeText));
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        CaretLine = command.CaretLine;
        CaretColumn = command.CaretColumn;
        if (Markdown != command.Markdown)
        {
            Markdown = command.Markdown;
        }
    }

    [EventHandler]
    public void ApplyWorkspaceStatusChanged(WorkspaceStatusChangedCommand command)
    {
        StatusText = command.Message;
    }

    private void RefreshMarkdownDerivedState()
    {
        Statistics = _statisticsService.Count(Markdown);
        OutlineItems.Clear();
        foreach (var item in _outlineService.BuildOutline(Markdown))
        {
            OutlineItems.Add(item);
        }

        NotifyOutlineChanged();
    }

    private void PublishEditorAction(EditorActionKind action)
    {
        _eventBus.Publish(new EditorActionCommand(action));
    }

    public void ShowProperties()
    {
        IsPropertiesPanelVisible = true;
        SetStatus($"{CurrentDocumentTitle} | {DocumentStateText} | {CurrentEncodingText} | {PropertySizeText} | {PropertyLocationText}");
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

    public void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    public void ShowOutline()
    {
        IsSidebarVisible = true;
        SelectedSideTabIndex = 1;
    }

    public void ShowFiles()
    {
        IsSidebarVisible = true;
        SelectedSideTabIndex = 0;
    }

    public void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;
    }

    public void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    public void ToggleSourceMode()
    {
        if (!IsSourceMode)
        {
            _sidebarBeforeSourceMode = IsSidebarVisible;
            _previewBeforeSourceMode = IsPreviewVisible;
            IsSidebarVisible = false;
            IsPreviewVisible = false;
            IsSourceMode = true;
            SetStatus("Source mode enabled.");
            PublishEditorAction(EditorActionKind.FocusEditor);
            return;
        }

        IsSidebarVisible = _sidebarBeforeSourceMode;
        IsPreviewVisible = _previewBeforeSourceMode;
        IsSourceMode = false;
        SetStatus("Source mode disabled.");
        PublishEditorAction(EditorActionKind.FocusEditor);
    }

    public void ToggleAlwaysOnTop()
    {
        IsAlwaysOnTop = !IsAlwaysOnTop;
    }

    public void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
    }

    public void ActualSize()
    {
        EditorZoom = 1.0;
    }

    public void ZoomIn()
    {
        EditorZoom = Math.Min(1.8, EditorZoom + 0.1);
    }

    public void ZoomOut()
    {
        EditorZoom = Math.Max(0.7, EditorZoom - 0.1);
    }

    public void WordCount()
    {
        IsStatisticsPanelVisible = true;
        SetStatus($"Words {Statistics.Words}, Characters {Statistics.Characters}, Lines {Statistics.Lines}.");
    }

    public void CloseStatisticsPanel()
    {
        IsStatisticsPanelVisible = false;
    }

    public void CloseAboutPanel()
    {
        IsAboutPanelVisible = false;
    }

    public void ClosePropertiesPanel()
    {
        IsPropertiesPanelVisible = false;
    }

    public bool CloseFloatingPanel()
    {
        if (IsUnsavedConfirmVisible)
        {
            CancelPendingAction();
            return true;
        }

        if (IsDeleteConfirmVisible)
        {
            CancelDelete();
            return true;
        }

        if (IsPropertiesPanelVisible)
        {
            IsPropertiesPanelVisible = false;
            SetStatus("Properties closed.");
            return true;
        }

        if (IsStatisticsPanelVisible)
        {
            IsStatisticsPanelVisible = false;
            SetStatus("Statistics closed.");
            return true;
        }

        if (IsAboutPanelVisible)
        {
            IsAboutPanelVisible = false;
            SetStatus("About closed.");
            return true;
        }

        return false;
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

    public void Undo()
    {
        PublishEditorAction(EditorActionKind.Undo);
    }

    public void Redo()
    {
        PublishEditorAction(EditorActionKind.Redo);
    }

    public void Cut()
    {
        PublishEditorAction(EditorActionKind.Cut);
    }

    public void Copy()
    {
        PublishEditorAction(EditorActionKind.Copy);
    }

    public void Paste()
    {
        PublishEditorAction(EditorActionKind.Paste);
    }

    public void SelectAll()
    {
        PublishEditorAction(EditorActionKind.SelectAll);
    }

    public void FocusEditor()
    {
        PublishEditorAction(EditorActionKind.FocusEditor);
    }

    public void InsertAction(EditorActionKind action)
    {
        PublishEditorAction(action);
    }

    public async Task OpenHelpTopic(string? topic)
    {
        switch (topic)
        {
            case "changelog":
                await _helpService.OpenDocumentAsync("CHANGELOG.zh-CN.md");
                SetStatus("Opened changelog.");
                break;
            case "quick-start":
                await _helpService.OpenDocumentAsync("QuickStart.zh-CN.md");
                SetStatus("Opened quick start.");
                break;
            case "thanks":
                await _helpService.OpenDocumentAsync("ACKNOWLEDGEMENTS.zh-CN.md");
                SetStatus("Opened acknowledgements.");
                break;
            case "website":
                await _helpService.OpenWebsiteAsync();
                break;
            case "feedback":
                await _helpService.OpenFeedbackAsync();
                break;
            case "about":
                IsAboutPanelVisible = true;
                SetStatus("About Vex.");
                break;
            default:
                SetStatus($"{topic ?? "Help"} is queued for implementation.");
                break;
        }
    }

    private void SetStatus(string message)
    {
        StatusText = message;
        _eventBus.Publish(new WorkspaceStatusChangedCommand(message));
    }

    private async Task OpenRecentDocumentAsync(int index)
    {
        if (index < 0 || index >= RecentDocuments.Count)
        {
            SetStatus("Recent file is unavailable.");
            return;
        }

        var recent = RecentDocuments[index];
        if (!File.Exists(recent.Path))
        {
            RemoveRecentDocument(recent.Path);
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
        if (_pendingUnsavedContinuation is null)
        {
            IsUnsavedConfirmVisible = false;
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

    public void CancelPendingAction()
    {
        var cancellation = _pendingUnsavedCancellation;
        ClearUnsavedConfirmation();
        cancellation?.Invoke();
        SetStatus("Action canceled. Unsaved changes kept.");
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

        _pendingUnsavedContinuation = continuation;
        _pendingUnsavedCancellation = cancellation;
        _unsavedConfirmTitle = title;
        _unsavedConfirmMessage = message;
        _unsavedConfirmPath = CurrentFilePath ?? "Unsaved document";
        OnPropertyChanged(nameof(UnsavedConfirmTitle));
        OnPropertyChanged(nameof(UnsavedConfirmMessage));
        OnPropertyChanged(nameof(UnsavedConfirmPath));
        IsUnsavedConfirmVisible = true;
        SetStatus("Unsaved changes need a decision.");
    }

    private async Task ContinuePendingActionAsync()
    {
        var continuation = _pendingUnsavedContinuation;
        ClearUnsavedConfirmation();
        if (continuation is not null)
        {
            await continuation();
        }
    }

    private void ClearUnsavedConfirmation()
    {
        _pendingUnsavedContinuation = null;
        _pendingUnsavedCancellation = null;
        IsUnsavedConfirmVisible = false;
    }

    private void RestoreSelectedDocumentFile(DocumentFile? documentFile)
    {
        SetProperty(ref _selectedDocumentFile, documentFile, nameof(SelectedDocumentFile));
    }

    private void AddRecentDocument(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentDocuments.FirstOrDefault(item => item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentDocuments.Remove(existing);
        }

        RecentDocuments.Insert(0, new RecentDocument(fullPath));
        while (RecentDocuments.Count > MaxRecentDocuments)
        {
            RecentDocuments.RemoveAt(RecentDocuments.Count - 1);
        }

        SaveRecentDocuments();
        NotifyRecentDocumentsChanged();
    }

    private void RemoveRecentDocument(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentDocuments.FirstOrDefault(item => item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        RecentDocuments.Remove(existing);
        SaveRecentDocuments();
        NotifyRecentDocumentsChanged();
    }

    private void LoadRecentDocuments()
    {
        if (!File.Exists(RecentDocumentsPath))
        {
            return;
        }

        var paths = File.ReadAllLines(RecentDocumentsPath);
        foreach (var path in paths.Where(File.Exists).Take(MaxRecentDocuments))
        {
            RecentDocuments.Add(new RecentDocument(Path.GetFullPath(path)));
        }

        NotifyRecentDocumentsChanged();
    }

    private void SaveRecentDocuments()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RecentDocumentsPath)!);
        var paths = RecentDocuments.Select(item => item.Path).ToArray();
        File.WriteAllLines(RecentDocumentsPath, paths);
    }

    private void NotifyRecentDocumentsChanged()
    {
        OnPropertyChanged(nameof(HasRecentDocuments));
        OnPropertyChanged(nameof(HasRecentDocument1));
        OnPropertyChanged(nameof(HasRecentDocument2));
        OnPropertyChanged(nameof(HasRecentDocument3));
        OnPropertyChanged(nameof(HasRecentDocument4));
        OnPropertyChanged(nameof(HasRecentDocument5));
        OnPropertyChanged(nameof(RecentDocument1Text));
        OnPropertyChanged(nameof(RecentDocument2Text));
        OnPropertyChanged(nameof(RecentDocument3Text));
        OnPropertyChanged(nameof(RecentDocument4Text));
        OnPropertyChanged(nameof(RecentDocument5Text));
    }

    private void NotifyDocumentFilesChanged()
    {
        OnPropertyChanged(nameof(HasDocumentFiles));
        OnPropertyChanged(nameof(IsDocumentFilesEmpty));
    }

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutlineItems));
        OnPropertyChanged(nameof(IsOutlineEmpty));
    }

    private string GetRecentDocumentText(int index)
    {
        return index >= 0 && index < RecentDocuments.Count
            ? RecentDocuments[index].DisplayText
            : "No recent files";
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes:N0} B",
            < 1024 * 1024 => $"{bytes / 1024d:N1} KB",
            _ => $"{bytes / 1024d / 1024d:N1} MB"
        };
    }

    private static string GetEncodingDisplayName(Encoding encoding)
    {
        if (encoding is UTF8Encoding { Preamble.Length: > 0 })
        {
            return "UTF-8 BOM";
        }

        if (encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return "UTF-8";
        }

        return encoding.WebName.ToUpperInvariant();
    }

    private static bool IsSupportedDocumentPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static string RecentDocumentsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeWF",
            "Vex",
            "recent-files.txt");

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
