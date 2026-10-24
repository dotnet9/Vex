using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;
using CodeWF.Avalonia.MindView.Markdown;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>文档始终保存在 Workspace 状态中，三个视图只改变呈现和交互方式。</summary>
public sealed partial class ShellWorkbenchViewModel : ReactiveObject
{
    private readonly IWorkspaceDocumentState _document;
    public EditorPreferences Preferences { get; }
    private readonly IAppSettingsStore _settings;
    private readonly IEditorAppearanceState _appearance;
    private readonly IEditorDisplayState _display;
    private readonly ShellWindowLayoutViewModel _layout;
    private string _view;
    private string _edit;
    private string _workspaceLayout = "instant";
    private bool _showLayouts;
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
    private string? _mapDocumentPath;
    private string? _mapMarkdown;
    private int _visibleNodeCount;
    private bool _isPresenting;
    private bool _reportedPresentationMap;
    private double _presentationZoom = 1.5;
    private string _mindMapLayout = "right";
    private string _mindMapStyle = "quiet";
    private (string View, string Edit, Avalonia.Controls.WindowState WindowState, MarkdownLiveEditorState? LiveState)? _presentationSnapshot;

    public ShellWorkbenchViewModel(IWorkspaceDocumentState document, IAppSettingsStore settings,
        IEditorAppearanceState appearance, ShellWindowLayoutViewModel layout, IEditorDisplayState display, EditorPreferences preferences)
    {
        Preferences = preferences;
        preferences.PreferencesChanged += (_, _) =>
        {
            ApplyLiveAppearance();
            this.RaisePropertyChanged(nameof(EditorSurfaceMargin));
        };
        _document = document; _settings = settings; _appearance = appearance; _layout = layout; _display = display;
        // 排版主题/尺寸档变化要即时重放到实时编辑器，而不是只在挂载时应用一次。
        appearance.Changed += (_, _) => ApplyLiveAppearance();
        display.Changed += (_, _) => { if (_live is { } live) live.EnableAutoPair = display.EnableAutoPair; };
        _view = settings.Current.WorkbenchView is "read" or "mindmap" ? settings.Current.WorkbenchView : "edit";
        _edit = settings.Current.EditMode == "source" ? "source" : "instant";
        _workspaceLayout = WorkbenchLayoutOption.All.Any(option => option.Id == settings.Current.WorkspaceLayout)
            ? settings.Current.WorkspaceLayout! : _view == "mindmap" ? "map" : _view == "read" ? "read" : _edit;
        if (LayoutOption.Editor is { } initialMode) _edit = initialMode;
        _summaryLength = Math.Clamp(settings.Current.MindMapSummaryLength ?? 30, 1, 120);
        _mindMapLayout = settings.Current.MindMapLayout is { } savedLayout && MindMapLayoutKeys.Contains(savedLayout) ? savedLayout : "right";
        _mindMapStyle = settings.Current.MindMapStyle is { } savedStyle && MindMapStyleKeys.Contains(savedStyle) ? savedStyle : "quiet";
        // 原型默认（workbench.js §settings）：导图仅显示章节标题，段落/列表/富块默认关闭。
        _showParagraphs = settings.Current.MindMapShowParagraphs ?? false;
        _showCode = settings.Current.MindMapShowCode ?? false;
        _showTables = settings.Current.MindMapShowTables ?? false;
        _showLists = settings.Current.MindMapShowLists ?? false;
        History.Reset(document.Markdown);
        document.DocumentChanged += (_, _) =>
        {
            History.Observe(document.Markdown); SynchronizeLive(); QueueMap();
            if (IsPresenting)
            {
                // 演示期间外部打开文件或更新正文，退出时不能重放旧文档的编辑快照。
                if (_presentationSnapshot is { } snapshot) _presentationSnapshot = (snapshot.View, snapshot.Edit, snapshot.WindowState, null);
                BuildPresentation(); NotifyPresentation();
            }
        };
        appearance.Changed += (_, _) => ApplyLiveAppearance();
        CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged += OnCultureChanged;
        ApplyLayout(); QueueMap();
    }

    /// <summary>工作区绑定会同步修改编辑器，语言通知必须在 UI 线程发布。</summary>
    private void OnCultureChanged(object? sender, EventArgs args)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnCultureChanged(sender, args));
            return;
        }

        foreach (var name in new[] { nameof(ViewLabel), nameof(LayoutName), nameof(EditingHint), nameof(InstantHint), nameof(MapSummaryLabel), nameof(MapCountLabel), nameof(SelectedSourceLabel), nameof(SelectedKindLabel), nameof(SummaryPreview), nameof(MindMapLayoutLabel), nameof(MindMapStyleLabel), nameof(MoreSegmentLabel) })
            this.RaisePropertyChanged(name);
    }

    public Avalonia.Thickness ViewIndicatorMargin => new(IsReading ? 88 : IsMindMap ? 176 : 0, 0, 0, 0);
    public bool IsEditing => _view == "edit";
    public bool IsReading => _view == "read";
    public bool IsMindMap => _view == "mindmap";
    public string WorkspaceLayout => _workspaceLayout;

    /// <summary>状态栏三段直选：仅单一视图布局高亮对应段，组合布局落在「更多」。</summary>
    public bool IsSourceSegmentActive => _workspaceLayout == "source";
    public bool IsReadSegmentActive => _workspaceLayout == "read";
    public bool IsMapSegmentActive => _workspaceLayout == "map";

    /// <summary>「更多」段：组合布局时显示当前布局名并高亮，否则回落为「更多」。</summary>
    public bool IsMoreSegmentActive => !IsSourceSegmentActive && !IsReadSegmentActive && !IsMapSegmentActive;
    public string MoreSegmentLabel => IsMoreSegmentActive ? LayoutOption.Name : LocalizedMore;
    private static string LocalizedMore => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(VexL.StatusViewMore);
    public void ShowSourceView() => SelectLayout("source");
    public string LayoutName => LayoutOption.Name;
    public WorkbenchLayoutOption LayoutOption => WorkbenchLayoutOption.All.First(option => option.Id == _workspaceLayout);
    public bool HasEditor => !IsPresenting && LayoutOption.Editor is not null;
    public bool IsCombined => !IsPresenting && (LayoutOption.Render && LayoutOption.Map || HasEditor && (LayoutOption.Render || LayoutOption.Map));
    public bool IsMapVisible => IsPresenting ? PresentationMap : LayoutOption.Map;
    public bool ShowLayouts { get => _showLayouts; set => this.RaiseAndSetIfChanged(ref _showLayouts, value); }
    public bool IsInstantEditing => HasEditor && _edit == "instant";
    public bool IsSourceEditing => HasEditor && _edit == "source";
    public bool IsSourceVisible => IsSourceEditing;
    public int MapColumn => IsMindMap && ShowMapSource ? 2 : 0;
    public int MapSpan => ShowMapSource ? 1 : 3;
    public bool HasSelectedNode => SelectedNode is not null;
    public bool IsPreviewVisible => IsPresenting ? PresentationDocument : LayoutOption.Render;
    public bool IsPresenting => _isPresenting;
    public bool ShowEditorOptions => HasEditor && !IsPresenting;

    /// <summary>钉住的编辑工具条为占位行：工作区整体让出工具条高度；取消钉住时悬浮不占位。</summary>
    public Avalonia.Thickness EditorSurfaceMargin => ShowEditorOptions && Preferences.PinEditorToolbar ? new(0, 42, 0, 0) : default;
    public double PresentationZoom => _presentationZoom;
    public bool CanIncreasePresentationText => PresentationZoom < 2.5;
    public bool CanDecreasePresentationText => PresentationZoom > 1;

    /// <summary>演示字号拖动条的双向绑定：取值经 SetPresentationZoom 收敛到 0.1 步长。</summary>
    public double PresentationZoomValue { get => PresentationZoom; set => SetPresentationZoom(value); }
    public string PresentationZoomText => $"{Math.Round(PresentationZoom * 100)}%";
    /// <summary>临时进入全屏阅读；编辑状态、布局及偏好保留原值。</summary>
    public void StartPresentation()
    {
        if (IsPresenting) return;
        _presentationSnapshot = (_view, _edit, _layout.CurrentWindowState, IsInstantEditing ? _live?.CaptureState() : null);
        _layout.IsPresentationMode = true;
        _isPresenting = true;
        _presentationMap = _view == "mindmap";
        BuildPresentation();
        _view = "read";
        ShowTypography = false; ShowMapSettings = false; ShowLayouts = false; CloseInspector();
        _presentationZoom = 1.5;
        _layout.CurrentWindowState = Avalonia.Controls.WindowState.FullScreen;
        // 先暂停副窗口，再通知布局变化，避免布局整理把原副窗口直接关闭。
        NotifyPresentation(true); NotifyModes();
    }
    public void StopPresentation()
    {
        if (_presentationSnapshot is not { } snapshot) return;
        _presentationSnapshot = null;
        _view = snapshot.View; _edit = snapshot.Edit; _isPresenting = false;
        NotifyModes();
        _layout.CurrentWindowState = snapshot.WindowState;
        _layout.IsPresentationMode = false;
        NotifyPresentation(true);
        _presentationDocument = null;
        _presentationMarkdown = string.Empty;
        this.RaisePropertyChanged(nameof(PresentationSections));
        if (snapshot.LiveState is { } liveState && _live is { } live) live.RestoreState(liveState);
        else if (IsInstantEditing) SynchronizeLive();
        else if (IsSourceEditing) CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorActionCommand(EditorActionKind.FocusEditor));
    }
    public void IncreasePresentationText() => SetPresentationZoom(PresentationZoom + .1);
    public void DecreasePresentationText() => SetPresentationZoom(PresentationZoom - .1);
    private void SetPresentationZoom(double value)
    {
        _presentationZoom = Math.Round(Math.Clamp(value, 1, 2.5), 1);
        NotifyPresentation();
    }
    private void NotifyPresentation(bool modeChanged = false)
    {
        if (modeChanged) this.RaisePropertyChanged(nameof(IsPresenting));
        if (_reportedPresentationMap != PresentationMap) { _reportedPresentationMap = PresentationMap; this.RaisePropertyChanged(nameof(PresentationMap)); }
        foreach (var name in new[] { nameof(ShowEditorOptions), nameof(PresentationZoom), nameof(PresentationZoomText), nameof(CanIncreasePresentationText), nameof(CanDecreasePresentationText), nameof(PresentationMarkdown), nameof(PresentationIndex), nameof(PresentationProgress), nameof(PresentationOutline), nameof(PresentationDocument), nameof(PresentationContentMargin), nameof(CanPreviousPresentation), nameof(CanNextPresentation), nameof(IsMapVisible), nameof(IsPreviewVisible) }) this.RaisePropertyChanged(name);
    }
    private static string L(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(key) ?? string.Empty;
    public string EditingHint => L(IsInstantEditing ? VexL.WBInstantDescription : IsReading ? VexL.WBReadDescription : VexL.WBSourceDescription);
    public string InstantHint => L(VexL.WBInstantHint);
    // 原型：段落/列表/富块全关时，设置按钮显示「仅标题」，否则显示摘要长度。
    public string MapSummaryLabel => !ShowParagraphs && !ShowLists && !ShowRichBlocks
        ? L(VexL.WSHeadingsOnly)
        : string.Format(L(VexL.WBSummaryFormat), SummaryLength);
    public int VisibleNodeCount { get => _visibleNodeCount; set { this.RaiseAndSetIfChanged(ref _visibleNodeCount, value); this.RaisePropertyChanged(nameof(MapCountLabel)); } }
    public string MapCountLabel => string.Format(L(VexL.WBNodeCountFormat), VisibleNodeCount);
    public string SummaryPreview => MarkdownMindMapDocument.Summarize(Flatten(Root).FirstOrDefault(n => n.Kind == MarkdownMindMapNodeKind.Paragraph)?.FullText ?? L(VexL.WBSummarySample), SummaryLength);
    public string SelectedTitle => SelectedNode?.Title ?? string.Empty;
    public string SelectedKindLabel => SelectedNode is { } n ? n.Kind == MarkdownMindMapNodeKind.Heading ? $"H{n.HeadingLevel} · {L(VexL.WBHeadingNode)}" : L(n.Kind == MarkdownMindMapNodeKind.Document ? VexL.WBHeadingNode : VexL.WBContentNode) : string.Empty;
    private static IEnumerable<MarkdownMindMapNode> Flatten(MarkdownMindMapNode? node)
    { if (node is null) yield break; yield return node; foreach (var c in node.Children) foreach (var n in Flatten(c)) yield return n; }
    public void CloseInspector() => SelectedNode = null;
    // 原型 reset-settings：恢复「仅标题」默认（段落/列表/富块全关）。
    public void ResetMapSettings()
    { SummaryLength = 30; ShowParagraphs = false; ShowLists = false; ShowCode = false; ShowTables = false; }

    public Avalonia.Controls.GridLength SourceColumnWidth => !IsReading ? new(1, Avalonia.Controls.GridUnitType.Star) : new(0);
    public Avalonia.Controls.GridLength PreviewColumnWidth => IsPreviewVisible || IsMindMap && ShowMapSource ? new(IsMindMap ? 1.94 : 1.22, Avalonia.Controls.GridUnitType.Star) : new(0);
    public Avalonia.Controls.GridLength SplitterWidth => IsMindMap && ShowMapSource ? new(1) : new(0);
    public string View => _view;
    public string EditMode => _edit;
    public string ViewLabel => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(IsReading ? VexL.WBRead : IsMindMap ? VexL.WBMap : IsInstantEditing ? VexL.WBInstant : VexL.WBSource) ?? string.Empty;
    public string? ImageBasePath => _document.FilePath;
    public MarkdownMindMapNode? Root { get => _root; private set => this.RaiseAndSetIfChanged(ref _root, value); }
    public MarkdownMindMapNode? SelectedNode
    {
        get => _selected;
        set { this.RaiseAndSetIfChanged(ref _selected, value); this.RaisePropertyChanged(nameof(HasSelectedNode)); this.RaisePropertyChanged(nameof(SelectedFullText)); this.RaisePropertyChanged(nameof(SelectedSourceLabel)); this.RaisePropertyChanged(nameof(SelectedKindLabel)); this.RaisePropertyChanged(nameof(SelectedTitle)); NotifyChapter(); }
    }
    public string SelectedFullText => SelectedNode?.FullText ?? string.Empty;
    public string SelectedSourceLabel => SelectedNode is { } node ? string.Format(L(VexL.WBSourceLineFormat), node.SourceLine) : string.Empty;
    public bool ShowMapSource { get => _showMapSource; set { if (value != _showMapSource) SelectLayout(value ? "source-map" : "map"); } }

    public static readonly string[] MindMapLayoutKeys = ["right", "balanced", "radial", "down", "outline", "fishbone"];
    public static readonly string[] MindMapStyleKeys = ["quiet", "colorful", "outline", "capsule", "underline", "mono"];

    /// <summary>导图布局（right/balanced/radial/down/outline/fishbone），随设置持久化。</summary>
    public string MindMapLayout
    {
        get => _mindMapLayout;
        set
        {
            if (!MindMapLayoutKeys.Contains(value) || value == _mindMapLayout) return;
            _mindMapLayout = value;
            this.RaisePropertyChanged(nameof(MindMapLayout));
            this.RaisePropertyChanged(nameof(MapLayoutKind));
            _settings.Update(s => s with { MindMapLayout = value });
        }
    }

    /// <summary>导图样式（quiet/colorful/frame/capsule/vein/mono），随设置持久化。</summary>
    public string MindMapStyle
    {
        get => _mindMapStyle;
        set
        {
            if (!MindMapStyleKeys.Contains(value) || value == _mindMapStyle) return;
            _mindMapStyle = value;
            this.RaisePropertyChanged(nameof(MindMapStyle));
            this.RaisePropertyChanged(nameof(MapStyleKind));
            _settings.Update(s => s with { MindMapStyle = value });
        }
    }

    public CodeWF.Avalonia.MindView.MindMapLayoutKind MapLayoutKind =>
        Enum.TryParse(_mindMapLayout, true, out CodeWF.Avalonia.MindView.MindMapLayoutKind kind) ? kind : CodeWF.Avalonia.MindView.MindMapLayoutKind.Right;
    public CodeWF.Avalonia.MindView.MindMapStyleKind MapStyleKind =>
        Enum.TryParse(_mindMapStyle, true, out CodeWF.Avalonia.MindView.MindMapStyleKind style) ? style : CodeWF.Avalonia.MindView.MindMapStyleKind.Quiet;
    public string MindMapLayoutLabel => MindMapLayoutKeyLabel(_mindMapLayout);
    public string MindMapStyleLabel => MindMapStyleKeyLabel(_mindMapStyle);
    public static string MindMapLayoutKeyLabel(string key) => L(key switch
    {
        "balanced" => VexL.LayoutBalanced, "radial" => VexL.LayoutRadial, "down" => VexL.LayoutDown,
        "outline" => VexL.LayoutOutline, "fishbone" => VexL.LayoutFishbone, _ => VexL.LayoutRight,
    });
    public static string MindMapStyleKeyLabel(string key) => L(key switch
    {
        "colorful" => VexL.StyleColorful, "outline" => VexL.StyleFrame, "capsule" => VexL.StyleCapsule,
        "underline" => VexL.StyleVein, "mono" => VexL.StyleMono, _ => VexL.StyleQuiet,
    });
    public bool ShowMapSettings { get => _showMapSettings; set { this.RaiseAndSetIfChanged(ref _showMapSettings, value); if (value) CloseInspector(); } }
    public bool ShowTypography { get => _showTypography; set => this.RaiseAndSetIfChanged(ref _showTypography, value); }

    /// <summary>标题栏「外观」弹出层的开合状态，由 ShellAppearanceMenuView 回写；引导门控据此判定展开完成。</summary>
    public bool IsAppearanceMenuOpen { get => _isAppearanceMenuOpen; set => this.RaiseAndSetIfChanged(ref _isAppearanceMenuOpen, value); }

    /// <summary>引导「继续」代点：请求打开外观弹出层，实际显示由视图完成。</summary>
    public event EventHandler? AppearanceMenuRequested;
    public void RequestAppearanceMenu() => AppearanceMenuRequested?.Invoke(this, EventArgs.Empty);
    private bool _isAppearanceMenuOpen;
    public int SummaryLength { get => _summaryLength; set { var length = Math.Clamp(value, 1, 120); if (_summaryLength == length) return; this.RaiseAndSetIfChanged(ref _summaryLength, length); UpdateMapOptions(); } }
    public bool ShowParagraphs { get => _showParagraphs; set { this.RaiseAndSetIfChanged(ref _showParagraphs, value); UpdateMapOptions(); } }
    public bool ShowCode { get => _showCode; set { this.RaiseAndSetIfChanged(ref _showCode, value); this.RaisePropertyChanged(nameof(ShowRichBlocks)); UpdateMapOptions(); } }
    public bool ShowTables { get => _showTables; set { this.RaiseAndSetIfChanged(ref _showTables, value); this.RaisePropertyChanged(nameof(ShowRichBlocks)); UpdateMapOptions(); } }
    public bool ShowRichBlocks { get => ShowCode && ShowTables; set { ShowCode = value; ShowTables = value; } }
    public bool ShowLists { get => _showLists; set { this.RaiseAndSetIfChanged(ref _showLists, value); UpdateMapOptions(); } }
    public MarkdownLiveEditorView? LiveEditor => _live;
    public event EventHandler<MarkdownMindMapNode>? SourceNavigationRequested;
    public event EventHandler? GuideExitRequested;
    public void ExitGuide() => GuideExitRequested?.Invoke(this, EventArgs.Empty);
    public bool IsGuiding { get; set; }
    public int GuideStep { get; set; }

    public void ShowEdit() => SelectView("edit");
    public void ShowRead() => SelectView("read");
    public void ShowMindMap() => SelectView("mindmap");
    public void ShowInstant() => SelectLayout(IsMapVisible ? "instant-map" : "instant");
    public void ToggleSourceEditing() { if (IsSourceEditing) ShowInstant(); else ShowSource(); }
    public void ShowSource() => SelectLayout(IsMapVisible ? IsPreviewVisible ? "three" : "source-map" : IsPreviewVisible && !IsReading ? "source-render" : "source");
    public void ToggleMapSource() => SelectLayout(HasEditor ? "map" : "source-map");
    public void ToggleLayouts() => ShowLayouts = !ShowLayouts;
    public void SelectLayout(string id)
    {
        var option = WorkbenchLayoutOption.All.FirstOrDefault(option => option.Id == id);
        if (option is null) return;
        History.BreakGroup();
        _workspaceLayout = id;
        if (option.Editor is { } mode) _edit = mode;
        _view = option.Editor is not null ? "edit" : option.Map ? "mindmap" : "read";
        _showMapSource = option.Map && option.Editor == "source";
        if (IsInstantEditing) SynchronizeLive();
        NotifyModes();
        _settings.Update(s => s with { WorkspaceLayout = id, WorkbenchView = _view, EditMode = _edit });
    }
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
        _showParagraphs = settings?.MindMapShowParagraphs ?? false;
        _showCode = settings?.MindMapShowCode ?? false;
        _showTables = settings?.MindMapShowTables ?? false;
        _showLists = settings?.MindMapShowLists ?? false;
        foreach (var name in new[] { nameof(SummaryLength), nameof(ShowParagraphs), nameof(ShowCode), nameof(ShowTables), nameof(ShowRichBlocks), nameof(ShowLists) }) this.RaisePropertyChanged(name);
        SelectLayout(settings?.WorkspaceLayout ?? (view == "read" ? "read" : view == "mindmap" ? "map" : edit)); QueueMap();
    }
    private void SelectView(string view)
    {
        ShowTypography = false;
        SelectLayout(view == "read" ? "read" : view == "mindmap" ? "map" : _edit);
    }
    private void NotifyModes()
    {
        foreach (var name in new[] { nameof(WorkspaceLayout), nameof(LayoutOption), nameof(HasEditor), nameof(IsCombined), nameof(IsMapVisible), nameof(ShowMapSource), nameof(EditingHint), nameof(ViewIndicatorMargin), nameof(View), nameof(EditMode), nameof(ViewLabel), nameof(LayoutName), nameof(IsEditing), nameof(IsReading), nameof(IsMindMap), nameof(IsInstantEditing), nameof(IsSourceEditing), nameof(IsSourceVisible), nameof(IsPreviewVisible), nameof(MapColumn), nameof(MapSpan), nameof(SourceColumnWidth), nameof(PreviewColumnWidth), nameof(SplitterWidth), nameof(ShowEditorOptions), nameof(EditorSurfaceMargin), nameof(IsSourceSegmentActive), nameof(IsReadSegmentActive), nameof(IsMapSegmentActive), nameof(IsMoreSegmentActive), nameof(MoreSegmentLabel) }) this.RaisePropertyChanged(name);
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
        this.RaisePropertyChanged(nameof(MapSummaryLabel));
        this.RaisePropertyChanged(nameof(SummaryPreview));
        QueueMap();
    }
    private async void QueueMap()
    {
        var version = ++_mapVersion;
        var markdown = _document.Markdown;
        var documentPath = _document.FilePath;
        var title = _document.FilePath is { } path ? Path.GetFileNameWithoutExtension(path) : "未命名文档";
        var options = new MarkdownMindMapOptions { SummaryLength = SummaryLength, ShowParagraphs = ShowParagraphs, ShowCode = ShowCode, ShowTables = ShowTables, ShowLists = ShowLists };
        // 合并连续输入；AST 在后台构造，结果过期后不进入当前文档。
        await Task.Delay(140);
        if (version != _mapVersion) return;
        var root = await Task.Run(() => MarkdownMindMapDocument.Parse(markdown, title, options));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != _mapVersion) return;
            var sameDocument = _mapDocumentPath == documentPath;
            var selectedId = sameDocument ? SelectedNode?.Id : null;
            if (!sameDocument) Root = null;
            _mapDocumentPath = documentPath;
            _mapMarkdown = markdown;
            Root = root;
            SelectedNode = _pendingChapterStart is { } start ? Flatten(root).FirstOrDefault(node => node.Kind == MarkdownMindMapNodeKind.Heading && node.SourceStart == start) : Find(root, selectedId);
            _pendingChapterStart = null;
            this.RaisePropertyChanged(nameof(ImageBasePath));
            this.RaisePropertyChanged(nameof(SummaryPreview));
        }, DispatcherPriority.Background);
        static MarkdownMindMapNode? Find(MarkdownMindMapNode node, string? id) => node.Id == id ? node
            : node.Children.Select(child => Find(child, id)).FirstOrDefault(child => child is not null);
    }
    private void RefreshChapterMap()
    {
        ++_mapVersion;
        var markdown = _document.Markdown;
        var title = _document.FilePath is { } path ? Path.GetFileNameWithoutExtension(path) : WorkbenchText.Get("Untitled");
        _mapMarkdown = markdown; _mapDocumentPath = _document.FilePath;
        Root = MarkdownMindMapDocument.Parse(markdown, title, new MarkdownMindMapOptions
        { SummaryLength = SummaryLength, ShowParagraphs = ShowParagraphs, ShowCode = ShowCode, ShowTables = ShowTables, ShowLists = ShowLists });
        SelectedNode = _pendingChapterStart is { } start ? Flatten(Root).FirstOrDefault(node => node.Kind == MarkdownMindMapNodeKind.Heading && node.SourceStart == start) : null;
        _pendingChapterStart = null;
    }
    public void AttachLiveEditor(MarkdownLiveEditorView editor)
    {
        if (ReferenceEquals(_live, editor)) return;
        if (_live is not null) { _live.MarkdownChanged -= LiveChanged; _live.SelectionChanged -= LiveSelectionChanged; }
        _live = editor; editor.MarkdownChanged += LiveChanged;
        editor.SelectionChanged += LiveSelectionChanged;
        editor.EnableAutoPair = _display.EnableAutoPair;
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
        if (_live is null || !IsInstantEditing) return;
        var sameText = _live.Text == _document.Markdown;
        if (sameText && string.Equals(_live.ImageBasePath, _document.FilePath, StringComparison.Ordinal)) return;
        _syncingLive = true;
        try
        {
            if (sameText) _live.ImageBasePath = _document.FilePath;
            else _live.SetText(_document.Markdown, _document.FilePath);
        }
        finally { _syncingLive = false; }
    }
    private void ApplyLiveAppearance()
    {
        if (_live is null) return;
        _live.SyntaxOptions = Preferences.SyntaxOptions;
        _live.CodeFontSize = Preferences.EffectiveCodeFontSize;
        _live.CodeWrap = Preferences.CodeWrap;
        _live.EditorFontSize = Preferences.ResolveBodyFontSize(_appearance.TypographyTheme);
        _live.FontFamily = Preferences.ContentFontFamily;
        _live.TypographyTheme = _appearance.TypographyTheme;
        _live.TypographySize = _appearance.TypographySize;
        _live.DocumentBlockSpacing = 0;
    }
}
