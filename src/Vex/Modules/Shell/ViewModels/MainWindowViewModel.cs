using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    private readonly IDocumentService _documentService;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IMarkdownStatisticsService _statisticsService;
    private readonly IThemeService _themeService;
    private readonly IHelpService _helpService;
    private readonly IEventBus _eventBus;
    private DocumentSnapshot _document;
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
    private double _editorZoom = 1.0;
    private ThemeOption? _selectedTheme;
    private TypographyOption? _selectedTypography;
    private LanguageOption? _selectedLanguage;
    private MarkdownStatistics _statistics = new(0, 0, 1);
    private DocumentFile? _selectedDocumentFile;
    private OutlineItem? _selectedOutlineItem;

    public MainWindowViewModel(
        IDocumentService documentService,
        IMarkdownOutlineService outlineService,
        IMarkdownStatisticsService statisticsService,
        IThemeService themeService,
        IHelpService helpService,
        IEventBus eventBus)
    {
        _documentService = documentService;
        _outlineService = outlineService;
        _statisticsService = statisticsService;
        _themeService = themeService;
        _helpService = helpService;
        _eventBus = eventBus;
        _eventBus.Subscribe(this);

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
        SelectedLanguage = LanguageOptions.FirstOrDefault(item => item.CultureName == "zh-CN");

        _document = _documentService.CreateNew();
        Markdown = _document.Markdown;
    }

    public ObservableCollection<DocumentFile> DocumentFiles { get; } = [];

    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];

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
            }
        }
    }

    public string WindowTitle => $"{(_document.IsNew ? string.Empty : string.Empty)}{_document.FileName} - Vex";

    public string CurrentDocumentTitle => _document.FileName;

    public string? CurrentFilePath => _document.FilePath;

    public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFilePath);

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
        Markdown = _document.Markdown;
        SetStatus("New document created.");
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

    public async Task OpenFolderAsync()
    {
        var files = await _documentService.OpenFolderAsync();
        DocumentFiles.Clear();
        foreach (var file in files)
        {
            DocumentFiles.Add(file);
        }

        SetStatus(files.Count == 0 ? "No markdown files loaded." : $"Loaded {files.Count} markdown files.");
        if (files.Count > 0)
        {
            SelectedDocumentFile = files[0];
        }
    }

    private async Task OpenDocumentFileAsync(DocumentFile file)
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

    public async Task DeleteAsync()
    {
        if (CurrentFilePath is not { Length: > 0 } path)
        {
            return;
        }

        await _documentService.DeleteAsync(path);
        NewDocument();
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

        ApplyDocument(await _documentService.OpenPathAsync(path, encodingName));
        SetStatus($"Reopened with {encodingName}.");
    }

    private void ApplyDocument(DocumentSnapshot snapshot, bool updateMarkdown = true)
    {
        _document = snapshot;
        if (updateMarkdown)
        {
            Markdown = snapshot.Markdown;
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

    private void RefreshMarkdownDerivedState()
    {
        Statistics = _statisticsService.Count(Markdown);
        OutlineItems.Clear();
        foreach (var item in _outlineService.BuildOutline(Markdown))
        {
            OutlineItems.Add(item);
        }
    }

    private void PublishEditorAction(EditorActionKind action)
    {
        _eventBus.Publish(new EditorActionCommand(action));
    }

    public void ShowProperties()
    {
        SetStatus(CurrentFilePath ?? "Untitled document");
    }

    public void Export(string? format)
    {
        SetStatus($"Export {format ?? "document"} is queued for implementation.");
    }

    public void Print()
    {
        SetStatus("Print is queued for implementation.");
    }

    public void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    public void ShowOutline()
    {
        SelectedSideTabIndex = 1;
    }

    public void ShowFiles()
    {
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
        IsPreviewVisible = !IsPreviewVisible;
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
        SetStatus($"Words {Statistics.Words}, Characters {Statistics.Characters}, Lines {Statistics.Lines}.");
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
                SetStatus("Vex 0.1.0 - 极简之力，妙笔成章.");
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
