using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CodeWF.EventBus;
using CodeWF.Markdown.Themes;
using Lang.Avalonia;
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
    private readonly IThemeService _themeService;
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
    private bool _isCompactLayout;
    private bool _isSourceMode;
    private bool _sidebarBeforeSourceMode = true;
    private bool _previewBeforeSourceMode = true;
    private bool _isFindPanelVisible;
    private bool _isReplaceVisible;
    private bool _isStatisticsPanelVisible;
    private bool _isAboutPanelVisible;
    private bool _isPropertiesPanelVisible;
    private bool _isDeleteConfirmVisible;
    private string? _pendingDeletePath;
    private string _searchText = string.Empty;
    private string _replacementText = string.Empty;
    private double _editorZoom = 1.0;
    private ThemeOption? _selectedTheme;
    private TypographyOption? _selectedTypography;
    private LanguageOption? _selectedLanguage;
    private MarkdownStatistics _statistics = new(0, 0, 1);
    private DocumentFile? _selectedDocumentFile;
    private OutlineItem? _selectedOutlineItem;

    public MainWindowViewModel(
        IDocumentService documentService,
        IMarkdownExportService exportService,
        IMarkdownOutlineService outlineService,
        IMarkdownStatisticsService statisticsService,
        IThemeService themeService,
        IHelpService helpService,
        IEventBus eventBus)
    {
        _documentService = documentService;
        _exportService = exportService;
        _outlineService = outlineService;
        _statisticsService = statisticsService;
        _themeService = themeService;
        _helpService = helpService;
        _eventBus = eventBus;
        _eventBus.Subscribe(this);
        LoadRecentDocuments();

        ThemeOptions = new ObservableCollection<ThemeOption>(_themeService.GetThemeOptions());
        TypographyOptions = new ObservableCollection<TypographyOption>(
            MarkdownTypographyThemes.All.Select(theme => new TypographyOption(theme.Name, theme.Key)));
        LanguageOptions = new ObservableCollection<LanguageOption>
        {
            new("zh-CN", "简体中文", "中文（简体）"),
            new("zh-Hant", "繁體中文", "中文（繁體）"),
            new("en-US", "English", "English"),
            new("ja-JP", "日本語", "日本語")
        };

        SelectedTheme = ThemeOptions.FirstOrDefault();
        SelectedTypography = TypographyOptions.FirstOrDefault(item => item.Key == MarkdownTypographyThemes.Simple)
                             ?? TypographyOptions.FirstOrDefault();
        _selectedLanguage = LanguageOptions.FirstOrDefault(item => item.CultureName == "zh-CN");
        if (_selectedLanguage is not null)
        {
            I18nManager.Instance.Culture = new CultureInfo(_selectedLanguage.CultureName);
        }

        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
    }

    public async Task OpenStartupDocumentAsync(IEnumerable<string> arguments)
    {
        var path = arguments.FirstOrDefault(argument => File.Exists(argument) || Directory.Exists(argument));
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            await ApplyDocumentFilesAsync(await _documentService.OpenFolderPathAsync(path));
            return;
        }

        ApplyDocument(await _documentService.OpenPathAsync(path));
    }

    public ObservableCollection<DocumentFile> DocumentFiles { get; } = [];

    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];

    public ObservableCollection<RecentDocument> RecentDocuments { get; } = [];

    public ObservableCollection<ThemeOption> ThemeOptions { get; }

    public ObservableCollection<TypographyOption> TypographyOptions { get; }

    public ObservableCollection<LanguageOption> LanguageOptions { get; }

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

    public bool IsCompactLayout
    {
        get => _isCompactLayout;
        set
        {
            if (SetProperty(ref _isCompactLayout, value))
            {
                OnPropertyChanged(nameof(CurrentTypographySize));
            }
        }
    }

    public bool IsSourceMode
    {
        get => _isSourceMode;
        set => SetProperty(ref _isSourceMode, value);
    }

    public bool IsFindPanelVisible
    {
        get => _isFindPanelVisible;
        set => SetProperty(ref _isFindPanelVisible, value);
    }

    public bool IsReplaceVisible
    {
        get => _isReplaceVisible;
        set => SetProperty(ref _isReplaceVisible, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value ?? string.Empty);
    }

    public string ReplacementText
    {
        get => _replacementText;
        set => SetProperty(ref _replacementText, value ?? string.Empty);
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

    public string DeleteConfirmText => _pendingDeletePath is { Length: > 0 }
        ? $"Delete {Path.GetFileName(_pendingDeletePath)}?"
        : "Delete current file?";

    public string DeleteConfirmPath => _pendingDeletePath ?? string.Empty;

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

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value) && value is not null)
            {
                _themeService.ApplyTheme(value);
            }
        }
    }

    public TypographyOption? SelectedTypography
    {
        get => _selectedTypography;
        set
        {
            if (SetProperty(ref _selectedTypography, value))
            {
                OnPropertyChanged(nameof(CurrentTypographyTheme));
            }
        }
    }

    public string? CurrentTypographyTheme => SelectedTypography?.Key;

    public string CurrentTypographySize => IsCompactLayout
        ? MarkdownTypographySizes.Small
        : MarkdownTypographySizes.Normal;

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value is not null)
            {
                I18nManager.Instance.Culture = new CultureInfo(value.CultureName);
                SetStatus($"Language switched to {value.DisplayName}.");
            }
        }
    }

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
            if (SetProperty(ref _selectedDocumentFile, value) && value is not null)
            {
                _ = OpenDocumentFileAsync(value);
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

    public void NewDocument()
    {
        _document = _documentService.CreateNew();
        _lastSavedMarkdown = _document.Markdown;
        Markdown = _document.Markdown;
        SetStatus("New document created.");
        NotifyDocumentChanged();
        PublishEditorAction(EditorActionKind.FocusEditor);
    }

    public void CloseDocument()
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
        await ApplyDocumentFilesAsync(await _documentService.OpenFolderAsync());
    }

    private async Task ApplyDocumentFilesAsync(IReadOnlyList<DocumentFile> files)
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
            await OpenDocumentFileAsync(files[0]);
        }
    }

    private async Task OpenDocumentFileAsync(DocumentFile file)
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

        _pendingDeletePath = path;
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(DeleteConfirmPath));
        IsDeleteConfirmVisible = true;
        return Task.CompletedTask;
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
        NewDocument();
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
    public void ApplyEditorSearchResult(EditorSearchResultCommand command)
    {
        SetStatus(command.Message);
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
        IsFindPanelVisible = true;
        IsReplaceVisible = false;
        SetStatus("Find is ready.");
    }

    public void ShowReplacePanel()
    {
        IsFindPanelVisible = true;
        IsReplaceVisible = true;
        SetStatus("Replace is ready.");
    }

    public void CloseFindPanel()
    {
        IsFindPanelVisible = false;
        SetStatus("Find closed.");
        PublishEditorAction(EditorActionKind.FocusEditor);
    }

    public void FindNext()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatus("Enter search text first.");
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.FindNext, SearchText));
    }

    public void ReplaceNext()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatus("Enter search text first.");
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.ReplaceNext, SearchText, ReplacementText));
    }

    public void ReplaceAll()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatus("Enter search text first.");
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.ReplaceAll, SearchText, ReplacementText));
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

    public void ToggleCompactLayout()
    {
        IsCompactLayout = !IsCompactLayout;
    }

    public void SelectTheme(ThemeOption? theme)
    {
        if (theme is not null)
        {
            SelectedTheme = theme;
        }
    }

    public void SelectThemeByKey(string? key)
    {
        var theme = ThemeOptions.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        SelectTheme(theme);
    }

    public void SelectTypography(TypographyOption? typography)
    {
        if (typography is not null)
        {
            SelectedTypography = typography;
        }
    }

    public void SelectTypographyByKey(string? key)
    {
        var typography = TypographyOptions.FirstOrDefault(item => item.Key?.Equals(key, StringComparison.OrdinalIgnoreCase) == true);
        SelectTypography(typography);
    }

    public void SelectLanguage(LanguageOption? language)
    {
        if (language is not null)
        {
            SelectedLanguage = language;
        }
    }

    public void SelectLanguageByCulture(string? cultureName)
    {
        var language = LanguageOptions.FirstOrDefault(item => item.CultureName.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
        SelectLanguage(language);
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

        ApplyDocument(await _documentService.OpenPathAsync(recent.Path));
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
