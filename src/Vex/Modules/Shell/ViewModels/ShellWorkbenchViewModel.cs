using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;
using CodeWF.Avalonia.MindView.Markdown;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>文档始终保存在 Workspace 状态中，三个视图只改变呈现和交互方式。</summary>
public sealed class ShellWorkbenchViewModel : ReactiveObject
{
    private readonly IWorkspaceDocumentState _document;
    private readonly IAppSettingsStore _settings;
    private readonly IEditorAppearanceState _appearance;
    private readonly ShellWindowLayoutViewModel _layout;
    private string _view;
    private string _edit;
    private int _summaryLength;
    private bool _showParagraphs;
    private bool _showCode;
    private bool _showTables;
    private bool _showLists;
    private bool _showMapSource;
    private bool _showMapSettings;
    private bool _showTypography;
    private MarkdownMindMapNode? _root;
    private MarkdownMindMapNode? _selected;
    private MarkdownLiveEditorView? _live;
    private bool _syncingLive;
    private int _mapVersion;

    public ShellWorkbenchViewModel(IWorkspaceDocumentState document, IAppSettingsStore settings,
        IEditorAppearanceState appearance, ShellWindowLayoutViewModel layout)
    {
        _document = document; _settings = settings; _appearance = appearance; _layout = layout;
        _view = settings.Current.WorkbenchView is "read" or "mindmap" ? settings.Current.WorkbenchView : "edit";
        _edit = settings.Current.EditMode == "source" ? "source" : "instant";
        _summaryLength = Math.Clamp(settings.Current.MindMapSummaryLength ?? 30, 1, 120);
        _showParagraphs = settings.Current.MindMapShowParagraphs ?? true;
        _showCode = settings.Current.MindMapShowCode ?? false;
        _showTables = settings.Current.MindMapShowTables ?? false;
        _showLists = settings.Current.MindMapShowLists ?? true;
        document.DocumentChanged += (_, _) => { SynchronizeLive(); QueueMap(); };
        appearance.Changed += (_, _) => ApplyLiveAppearance();
        CodeWF.Avalonia.Lang.I18nManager.Instance.CultureChanged += (_, _) => this.RaisePropertyChanged(nameof(ViewLabel));
        ApplyLayout(); QueueMap();
    }

    public bool IsEditing => _view == "edit";
    public bool IsReading => _view == "read";
    public bool IsMindMap => _view == "mindmap";
    public bool IsInstantEditing => IsEditing && _edit == "instant";
    public bool IsSourceEditing => IsEditing && _edit == "source";
    public bool IsSourceVisible => IsSourceEditing || IsMindMap && ShowMapSource;
    public int MapColumn => IsMindMap && ShowMapSource ? 2 : 0;
    public int MapSpan => ShowMapSource ? 1 : 3;
    public bool HasSelectedNode => SelectedNode is not null;
    public bool IsPreviewVisible => IsReading || IsSourceEditing;
    public Avalonia.Controls.GridLength SourceColumnWidth => !IsReading ? new(1, Avalonia.Controls.GridUnitType.Star) : new(0);
    public Avalonia.Controls.GridLength PreviewColumnWidth => IsPreviewVisible || IsMindMap && ShowMapSource ? new(1.06, Avalonia.Controls.GridUnitType.Star) : new(0);
    public Avalonia.Controls.GridLength SplitterWidth => IsSourceEditing || IsMindMap && ShowMapSource ? new(5) : new(0);
    public string View => _view;
    public string EditMode => _edit;
    public string ViewLabel => CodeWF.Avalonia.Lang.I18nManager.Instance.GetResource(IsReading ? VexL.WBRead : IsMindMap ? VexL.WBMap : IsInstantEditing ? VexL.WBInstant : VexL.WBSource) ?? string.Empty;
    public string? ImageBasePath => _document.FilePath;
    public MarkdownMindMapNode? Root { get => _root; private set => this.RaiseAndSetIfChanged(ref _root, value); }
    public MarkdownMindMapNode? SelectedNode
    {
        get => _selected;
        set { this.RaiseAndSetIfChanged(ref _selected, value); this.RaisePropertyChanged(nameof(HasSelectedNode)); this.RaisePropertyChanged(nameof(SelectedFullText)); this.RaisePropertyChanged(nameof(SelectedSourceLabel)); }
    }
    public string SelectedFullText => SelectedNode?.FullText ?? "点击节点查看完整内容，双击定位到 Markdown 源码。";
    public string SelectedSourceLabel => SelectedNode is { } node ? $"源码第 {node.SourceLine} 行" : "节点详情";
    public bool ShowMapSource { get => _showMapSource; set { this.RaiseAndSetIfChanged(ref _showMapSource, value); NotifyModes(); } }
    public bool ShowMapSettings { get => _showMapSettings; set => this.RaiseAndSetIfChanged(ref _showMapSettings, value); }
    public bool ShowTypography { get => _showTypography; set => this.RaiseAndSetIfChanged(ref _showTypography, value); }
    public int SummaryLength { get => _summaryLength; set { var length = Math.Clamp(value, 1, 120); if (_summaryLength == length) return; this.RaiseAndSetIfChanged(ref _summaryLength, length); UpdateMapOptions(); } }
    public bool ShowParagraphs { get => _showParagraphs; set { this.RaiseAndSetIfChanged(ref _showParagraphs, value); UpdateMapOptions(); } }
    public bool ShowCode { get => _showCode; set { this.RaiseAndSetIfChanged(ref _showCode, value); UpdateMapOptions(); } }
    public bool ShowTables { get => _showTables; set { this.RaiseAndSetIfChanged(ref _showTables, value); UpdateMapOptions(); } }
    public bool ShowLists { get => _showLists; set { this.RaiseAndSetIfChanged(ref _showLists, value); UpdateMapOptions(); } }
    public MarkdownLiveEditorView? LiveEditor => _live;
    public event EventHandler<MarkdownMindMapNode>? SourceNavigationRequested;
    public event EventHandler? GuideExitRequested;
    public void ExitGuide() => GuideExitRequested?.Invoke(this, EventArgs.Empty);

    public void ShowEdit() => SelectView("edit");
    public void ShowRead() => SelectView("read");
    public void ShowMindMap() => SelectView("mindmap");
    public void ShowInstant() { _edit = "instant"; SelectView("edit"); }
    public void ShowSource() { _edit = "source"; SelectView("edit"); }
    public void ToggleMapSource() => ShowMapSource = !ShowMapSource;
    public void ToggleMapSettings() => ShowMapSettings = !ShowMapSettings;
    public void OpenMapSettings() { ShowMindMap(); ShowMapSettings = true; }
    public void ToggleTypography() => ShowTypography = !ShowTypography;
    public void LocateSelectedSource() { if (SelectedNode is { } node) LocateSource(node); }
    public void LocateSource(MarkdownMindMapNode node)
    {
        ShowSource(); SourceNavigationRequested?.Invoke(this, node);
    }
    public void RestorePreferences(Vex.Core.Models.AppSettings? settings, string view, string edit)
    {
        _edit = edit;
        _summaryLength = Math.Clamp(settings?.MindMapSummaryLength ?? 30, 1, 120);
        _showParagraphs = settings?.MindMapShowParagraphs ?? true;
        _showCode = settings?.MindMapShowCode ?? false;
        _showTables = settings?.MindMapShowTables ?? false;
        _showLists = settings?.MindMapShowLists ?? true;
        foreach (var name in new[] { nameof(SummaryLength), nameof(ShowParagraphs), nameof(ShowCode), nameof(ShowTables), nameof(ShowLists) }) this.RaisePropertyChanged(name);
        SelectView(view); QueueMap();
    }
    private void SelectView(string view)
    {
        _view = view; ShowTypography = false;
        NotifyModes();
        _settings.Update(s => s with { WorkbenchView = _view, EditMode = _edit });
    }
    private void NotifyModes()
    {
        foreach (var name in new[] { nameof(View), nameof(EditMode), nameof(ViewLabel), nameof(IsEditing), nameof(IsReading), nameof(IsMindMap), nameof(IsInstantEditing), nameof(IsSourceEditing), nameof(IsSourceVisible), nameof(IsPreviewVisible), nameof(MapColumn), nameof(MapSpan), nameof(SourceColumnWidth), nameof(PreviewColumnWidth), nameof(SplitterWidth) }) this.RaisePropertyChanged(name);
        ApplyLayout();
    }
    private void ApplyLayout()
    {
        _layout.IsSourceMode = IsSourceVisible;
        _layout.IsPreviewVisible = IsPreviewVisible;
    }
    private void UpdateMapOptions()
    {
        _settings.Update(s => s with { MindMapSummaryLength = SummaryLength, MindMapShowParagraphs = ShowParagraphs, MindMapShowCode = ShowCode, MindMapShowTables = ShowTables, MindMapShowLists = ShowLists });
        QueueMap();
    }
    private async void QueueMap()
    {
        var version = ++_mapVersion;
        var markdown = _document.Markdown;
        var title = _document.FilePath is { } path ? Path.GetFileNameWithoutExtension(path) : "未命名文档";
        var options = new MarkdownMindMapOptions { SummaryLength = SummaryLength, ShowParagraphs = ShowParagraphs, ShowCode = ShowCode, ShowTables = ShowTables, ShowLists = ShowLists };
        // 合并连续输入；AST 在后台构造，结果过期后不进入当前文档。
        await Task.Delay(140);
        if (version != _mapVersion) return;
        var root = await Task.Run(() => MarkdownMindMapDocument.Parse(markdown, title, options));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != _mapVersion) return;
            var selectedId = SelectedNode?.Id;
            Root = root;
            SelectedNode = Find(root, selectedId);
            this.RaisePropertyChanged(nameof(ImageBasePath));
        }, DispatcherPriority.Background);
        static MarkdownMindMapNode? Find(MarkdownMindMapNode node, string? id) => node.Id == id ? node
            : node.Children.Select(child => Find(child, id)).FirstOrDefault(child => child is not null);
    }
    public void AttachLiveEditor(MarkdownLiveEditorView editor)
    {
        if (ReferenceEquals(_live, editor)) return;
        if (_live is not null) _live.MarkdownChanged -= LiveChanged;
        _live = editor; editor.MarkdownChanged += LiveChanged;
        editor.SelectionChanged += LiveSelectionChanged;
        SynchronizeLive(); ApplyLiveAppearance();
    }
    public void DetachLiveEditor(MarkdownLiveEditorView editor)
    {
        if (!ReferenceEquals(_live, editor)) return;
        editor.MarkdownChanged -= LiveChanged; editor.SelectionChanged -= LiveSelectionChanged; _live = null;
    }
    private void LiveChanged(object? sender, string markdown)
    {
        if (_syncingLive) return;
        var lines = markdown.Count(c => c == '\n') + 1;
        EventBus.Default.Publish(new MarkdownTextChangedCommand(markdown, 1, 1, lines));
    }
    private void LiveSelectionChanged(object? sender, MarkdownCaretEventArgs e)
    {
        if (!_syncingLive && IsInstantEditing && _live is { } live)
            EventBus.Default.Publish(new MarkdownTextChangedCommand(live.Text, e.Line, e.Column, e.LineCount));
    }
    private void SynchronizeLive()
    {
        if (_live is null || _live.Text == _document.Markdown) return;
        _syncingLive = true;
        try { _live.SetText(_document.Markdown); _live.ImageBasePath = _document.FilePath; }
        finally { _syncingLive = false; }
    }
    private void ApplyLiveAppearance()
    {
        if (_live is null) return;
        _live.TypographyTheme = _appearance.TypographyTheme;
        _live.TypographySize = _appearance.TypographySize;
    }
}
