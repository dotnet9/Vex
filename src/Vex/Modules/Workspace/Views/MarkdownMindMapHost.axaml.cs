using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.MindView.Markdown;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Input;
using Avalonia.Threading;
using Vex.Modules.Shell.ViewModels;
using Vex.Core.Models;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownMindMapHost : UserControl
{
    public Control GuideSettingsTarget => SettingsTarget;
    private ShellWorkbenchViewModel? _workbench;
    private bool _refreshingInspector;
    private bool _applyingMapZoom;
    private int? _inspectedStart;
    private int? _renamingStart;
    private (bool Paragraphs, bool Lists, bool Code, bool Tables)? _savedVisibility;
    private Vex.Core.Services.EditorPreferences? _pinPreferences;

    public MarkdownMindMapHost()
    {
        InitializeComponent();
        // 取消钉住时工具条悬浮于导图之上；声明顺序在后会盖住内容，这里用 ZIndex 提升。
        MapEditingToolbar.ZIndex = 1;
        DataContextChanged += OnMapPinContextChanged;
        // 原型 .map-controls 的还原钮写的是 map-fit：适应画布，而不是回到 100%。
        MapZoomDock.ResetRequested += (_, _) => Map.Fit();
        BuildMapStyleFlyout();
    }

    private void OnMapPinContextChanged(object? sender, EventArgs e)
    {
        if (_pinPreferences is not null)
        {
            _pinPreferences.PreferencesChanged -= OnMapPinPreferenceChanged;
            _pinPreferences = null;
        }
        if (DataContext is MainWindowViewModel vm)
        {
            _pinPreferences = vm.Workbench.Preferences;
            _pinPreferences.PreferencesChanged += OnMapPinPreferenceChanged;
        }
        ApplyMapPin();
    }

    private void OnMapPinPreferenceChanged(object? sender, EventArgs e) => ApplyMapPin();

    /// <summary>钉住 = 工具条占位行；取消钉住 = 悬浮在导图顶端，静默至悬停或聚焦。</summary>
    private void ApplyMapPin()
    {
        var pinned = _pinPreferences?.PinMapToolbar ?? true;
        Grid.SetRow(MapEditingToolbar, pinned ? 0 : 1);
        MapEditingToolbar.VerticalAlignment = pinned ? Avalonia.Layout.VerticalAlignment.Stretch : Avalonia.Layout.VerticalAlignment.Top;
        MapEditingToolbar.Classes.Set("floating", !pinned);
    }


    /// <summary>「导图样式」入口：六种布局 + 六种样式的单选弹层（对应原型导图样式按钮）。</summary>
    private void BuildMapStyleFlyout()
    {
        var button = new Button
        {
            Classes = { "wb-quiet", "map-tool" },
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 4,
                Children =
                {
                    new TextBlock { Text = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.WBMapStyleButton") ?? "导图样式", VerticalAlignment = VerticalAlignment.Center },
                    new Avalonia.Controls.Shapes.Path { Classes = { "expand-chevron" }, Data = Avalonia.Media.Geometry.Parse("M6 9L12 15L18 9") },
                },
            },
        };
        ToolTip.SetTip(button, CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.WBMapStyleButton") ?? "导图样式");
        // 原型 .map-appearance-popover：528−32 内距 = 496 内容宽，标题（mb 12）→ 分段页签（mb 14）→ 3 列卡片网格（gap 10、padding 2）。
        var panel = new StackPanel { Spacing = 0, Width = 496 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 12) };
        var headerTitle = new TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        var closeButton = new Button { Classes = { "vex-dismiss" }, Content = "×", Width = 26, Height = 26, Padding = new Thickness(0) };
        header.Children.Add(headerTitle); Grid.SetColumn(closeButton, 1); header.Children.Add(closeButton);
        panel.Children.Add(header);

        // 原型 .appearance-tabs.vex-segmented：结构／风格是标准分段控件（gap 4、段最小宽 80、
        // 32×密度高、4×密度/14 内距、正文减 1 号字），轨道与滑动指示块走 Vex.Controls 共享配方。
        var segments = new Vex.Controls.VexSegmentedPanel { Spacing = 4 };
        var layoutTab = new Button { Classes = { "vex-segment", "map-appearance-tab" }, Content = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.MapTabStructure") ?? "结构" };
        var styleTab = new Button { Classes = { "vex-segment", "map-appearance-tab" }, Content = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.MapTabStyle") ?? "风格" };
        segments.Children.Add(layoutTab); segments.Children.Add(styleTab);
        var tabs = new Border { Classes = { "vex-segmented", "appearance-tabs" }, Child = segments, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(tabs);

        var layoutPage = new StackPanel { Spacing = 0 };
        var stylePage = new StackPanel { Spacing = 0 };
        panel.Children.Add(layoutPage); panel.Children.Add(stylePage);

        var layoutCards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10, RowSpacing = 10, Margin = new Thickness(2) };
        var styleCards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10, RowSpacing = 10, Margin = new Thickness(2) };
        layoutPage.Children.Add(layoutCards); stylePage.Children.Add(styleCards);

        var flyoutHost = new ContentControl();
        var flyout = new Flyout { Content = flyoutHost, Placement = PlacementMode.BottomEdgeAlignedLeft };
        // 触发钮插入工具栏（对照源码旁，原型顺序），随工具条悬浮显示。
        MapToolsRight.Children.Insert(MapToolsRight.Children.IndexOf(MapSourceButton) + 1, button);
        closeButton.Click += (_, _) => flyout.Hide();

        var showStructure = true; // 原型 map-appearance.js：tab 初值 'structure'。
        void ShowPage(bool layout)
        {
            showStructure = layout;
            layoutPage.IsVisible = layout; stylePage.IsVisible = !layout;
            Vex.Controls.VexSegmentedPanel.SetIsActive(layoutTab, layout);
            Vex.Controls.VexSegmentedPanel.SetIsActive(styleTab, !layout);
        }
        ShowPage(true);
        layoutTab.Click += (_, _) => ShowPage(true);
        styleTab.Click += (_, _) => ShowPage(false);

        Action refresh = null!; // 卡片点击要重建两页线稿，先把变量声明提到闭包之外。
        refresh = () =>
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var vm = shell.Workbench;
            headerTitle.Text = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.WBMapStyleButton") ?? "导图样式";
            layoutCards.Children.Clear(); styleCards.Children.Clear();
            layoutCards.RowDefinitions.Clear(); styleCards.RowDefinitions.Clear();
            // 原型 .map-choice 是无状态按钮，选中靠 aria-pressed；这里同构：按下态由 map-choice-on 类回放。
            var cards = new List<(Button Card, Func<bool> IsSelected)>();
            void AddCard(Grid host, string label, string key, string shape, string style, Func<bool> selected, Action apply)
            {
                var card = new Button { Classes = { "map-choice" }, Content = BuildStyleCard(label, shape, style), Name = "MapChoice_" + key };
                Avalonia.Automation.AutomationProperties.SetName(card, label);
                card.Click += (_, _) => { apply(); refresh(); ShowPage(showStructure); }; // 原型 panel.onclick：改完即 render()，两页线稿同时换形/换色。
                Grid.SetColumn(card, host.Children.Count % 3);
                if (host.Children.Count % 3 == 0) host.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(card, host.RowDefinitions.Count - 1);
                host.Children.Add(card);
                cards.Add((card, selected));
            }
            void Sync() { foreach (var (card, selected) in cards) card.Classes.Set("map-choice-on", selected()); }
            foreach (var key in ShellWorkbenchViewModel.MindMapLayoutKeys)
            {
                var captured = key;
                AddCard(layoutCards, ShellWorkbenchViewModel.MindMapLayoutKeyLabel(key), key,
                    key, vm.MindMapStyle,
                    () => vm.MindMapLayout == captured, () => vm.MindMapLayout = captured);
            }
            foreach (var key in ShellWorkbenchViewModel.MindMapStyleKeys)
            {
                var captured = key;
                AddCard(styleCards, ShellWorkbenchViewModel.MindMapStyleKeyLabel(key), key,
                    vm.MindMapLayout, key,
                    () => vm.MindMapStyle == captured, () => vm.MindMapStyle = captured);
            }
            Sync();
        };
        flyout.FlyoutPresenterClasses.Add("map-appearance-popover");
        flyout.Opening += (_, _) => { refresh(); ShowPage(showStructure); flyoutHost.Content = panel; };
        button.Flyout = flyout;
        Map.NodeSelected += (_, node) =>
        {
            if (_workbench?.IsPresenting == true) return;
            FlushInspector();
            if (DataContext is not MainWindowViewModel vm) return;
            vm.Workbench.ShowMapSettings = false;
            vm.Workbench.SelectedNode = node;
        };
        Map.SourceRequested += (_, node) =>
        {
            if (_workbench?.IsPresenting == true)
            {
                var section = _workbench.PresentationSections.LastOrDefault(section => section.Start <= node.SourceStart) ?? _workbench.PresentationSections.FirstOrDefault();
                if (section is not null) { _workbench.NavigatePresentation(section.Index); _workbench.ShowPresentationDocument(); }
            }
            else (DataContext as MainWindowViewModel)?.Workbench.LocateSource(node);
        };
        Map.ChapterActionRequested += (_, action) => { if (_workbench is null) return; _workbench.SelectedNode = action.Node; if (action.Action == "rename") BeginRename(action.Node); else ExecuteChapter(action.Action); };
        Map.ChapterMoveRequested += (_, move) => { FlushInspector(); _workbench?.MoveChapter(move.Node.SourceStart, move.Target.SourceStart, move.Position); };
        Map.ZoomChanged += (_, _) => PublishMapZoom();
        Map.VisibleNodesChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Workbench.VisibleNodeCount = Map.VisibleNodeCount; };
        DataContextChanged += (_, _) => AttachWorkbench();
        AttachedToVisualTree += (_, _) => { EventBus.Default.Subscribe(this); AttachWorkbench(); };
        DetachedFromVisualTree += (_, _) => { EventBus.Default.Unsubscribe(this); if (_workbench is not null) _workbench.PropertyChanged -= WorkbenchChanged; _workbench = null; };
        SizeChanged += (_, _) =>
        {
            MapTitle.IsVisible = false; MapHint.IsVisible = Bounds.Width >= 480 && _workbench?.IsPresenting != true;
            MapHint.MaxWidth = Math.Max(0, Bounds.Width - 180);
            var wrap = Bounds.Width < 490;
            Grid.SetRow(MapToolsRight, wrap ? 1 : 0); Grid.SetColumn(MapToolsRight, wrap ? 0 : 1);
            Grid.SetColumnSpan(MapToolsRight, wrap ? 2 : 1);
            SettingsTarget.MaxHeight = Math.Max(100, Bounds.Height - 74);
        };
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.AccentColorProperty, this.GetResourceObservable("StudioAccentColor").Select(r => r is Color color ? color : Colors.MediumPurple));
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.SuccessColorProperty, this.GetResourceObservable("VexSuccessBrush").Select(r => r is ISolidColorBrush b ? b.Color : Colors.SeaGreen));
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.WarningColorProperty, this.GetResourceObservable("VexWarningBrush").Select(r => r is ISolidColorBrush b ? b.Color : Colors.Orange));
        MapDotGrid.Bind(Border.BackgroundProperty, this.GetResourceObservable("VexMutedBrush").Select(MapDotGridBrush));
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsVisible" && IsVisible)
                Avalonia.Threading.Dispatcher.UIThread.Post(Map.EnsureViewport, Avalonia.Threading.DispatcherPriority.Render);
        };
    }
    /// <summary>原型 .map-viewport 的点阵底纹：muted 21% 的圆点，18px 网格重复一次。</summary>
    /// <remarks>DrawingBrush 在 Native AOT 下会让 libSkiaSharp 崩溃（启动即 0xC0000005），
    /// 故把贴片按 CSS 的 farthest-corner 停止点烘焙成位图，交给 ImageBrush 平铺。</remarks>
    private static readonly ConcurrentDictionary<Color, IImageBrushSource> DotTiles = new();

    private static IBrush MapDotGridBrush(object? resource) => new ImageBrush
    {
        TileMode = TileMode.Tile,
        Source = DotTiles.GetOrAdd(resource is ISolidColorBrush muted ? muted.Color : Colors.Gray, CreateDotTile),
        DestinationRect = new RelativeRect(0, 0, 18, 18, RelativeUnit.Absolute),
    };

    private static IImageBrushSource CreateDotTile(Color muted)
    {
        const int size = 18;
        var buffer = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                float coverage = 0;
                for (var sy = 0; sy < 4; sy++)
                {
                    for (var sx = 0; sx < 4; sx++)
                    {
                        var dx = x + (sx + .5f) / 4 - size / 2f;
                        var dy = y + (sy + .5f) / 4 - size / 2f;
                        coverage += Math.Clamp((.9f - MathF.Sqrt(dx * dx + dy * dy)) / .2f, 0, 1);
                    }
                }
                var alpha = coverage / 16 * .21f;
                var offset = (y * size + x) * 4;
                // Bgra8888 预乘：通道值需先乘自身 alpha。
                buffer[offset] = (byte)(muted.B * alpha);
                buffer[offset + 1] = (byte)(muted.G * alpha);
                buffer[offset + 2] = (byte)(muted.R * alpha);
                buffer[offset + 3] = (byte)(alpha * 255);
            }
        }
        var tile = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var framebuffer = tile.Lock())
        {
            Marshal.Copy(buffer, 0, framebuffer.Address, buffer.Length);
        }
        return tile;
    }
    // 节点详情按原型保持悬浮在导图右上角（.node-inspector）；仅删除确认遮罩需要提升到窗口层。
    public Control DetachConfirmation()
    {
        if (ChapterDeleteLayer.Parent is Panel host) host.Children.Remove(ChapterDeleteLayer);
        return ChapterDeleteLayer;
    }
    private void AttachWorkbench()
    {
        if (_workbench is not null) _workbench.PropertyChanged -= WorkbenchChanged;
        _workbench = (DataContext as MainWindowViewModel)?.Workbench;
        if (_workbench is not null) _workbench.PropertyChanged += WorkbenchChanged;
        Map.IsChapterEditingEnabled = _workbench?.IsPresenting != true;
        ApplyMapZoom(ActiveMapScale());
        RefreshInspector();
        RefreshHeadingsOnly();
    }

    /// <summary>缩放的真值在视图模型（主窗体走偏好、演示走临时态），画布只是它的另一张脸。</summary>
    private double ActiveMapScale() => _workbench is { } state
        ? state.PresentationMap ? state.PresentationMapScale : state.MapZoomPercent / 100
        : 1;

    private void PublishMapZoom()
    {
        if (_applyingMapZoom || _workbench is not { } state) return;
        if (state.PresentationMap) state.PresentationMapScale = Map.Zoom;
        else state.MapZoomPercent = Map.Zoom * 100;
    }

    private void ApplyMapZoom(double scale)
    {
        if (Math.Abs(Map.Zoom - scale) < .005) return;
        _applyingMapZoom = true;
        try { Map.SetZoom(scale); }
        finally { _applyingMapZoom = false; }
    }

    private void WorkbenchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.IsPresenting))
        { MapHint.IsVisible = Bounds.Width >= 480 && _workbench?.IsPresenting != true; Map.IsChapterEditingEnabled = _workbench?.IsPresenting != true; }
        if (e.PropertyName is nameof(ShellWorkbenchViewModel.MapZoomPercent) or nameof(ShellWorkbenchViewModel.PresentationMapScale) or nameof(ShellWorkbenchViewModel.PresentationMap))
            ApplyMapZoom(ActiveMapScale());
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.SelectedNode))
        { Map.SelectNode(_workbench?.SelectedNode?.Id); RefreshInspector(); }
        if (e.PropertyName is nameof(ShellWorkbenchViewModel.ShowParagraphs) or nameof(ShellWorkbenchViewModel.ShowLists) or nameof(ShellWorkbenchViewModel.ShowCode) or nameof(ShellWorkbenchViewModel.ShowTables))
            RefreshHeadingsOnly();
    }
    private void RefreshInspector()
    {
        _refreshingInspector = true;
        try { _inspectedStart = _workbench?.SelectedNode?.SourceStart; ChapterTitleInput.Text = _workbench?.ChapterTitle; ChapterBodyInput.Text = _workbench?.ChapterBody; }
        finally { _refreshingInspector = false; }
    }
    public void FlushInspector()
    {
        if (_refreshingInspector || _workbench is not { IsChapterSelected: true } state || state.SelectedNode?.SourceStart != _inspectedStart) return;
        var title = ChapterTitleInput.Text; var body = ChapterBodyInput.Text;
        var titleChanged = title is not null && title != state.ChapterTitle;
        var bodyChanged = body is not null && body != state.ChapterBody;
        if (titleChanged) state.CommitChapterTitle(title!);
        if (bodyChanged) state.CommitChapterBody(body!);
    }
    private void TitleLostFocus(object? sender, RoutedEventArgs e) => FlushInspector();
    private void BodyLostFocus(object? sender, RoutedEventArgs e) => FlushInspector();
    private void TitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { FlushInspector(); Map.Focus(); e.Handled = true; }
        if (e.Key == Key.Escape) { RefreshInspector(); Map.Focus(); e.Handled = true; }
    }
    private void ChapterClick(object? sender, RoutedEventArgs e) { if (sender is Button { Tag: string command }) ExecuteChapter(command); }
    private void ExecuteChapter(string command)
    {
        FlushInspector();
        if (command == "delete")
        {
            if (_workbench?.IsChapterSelected != true || ReferenceEquals(_workbench.SelectedNode, _workbench.Root)) return;
            DeleteChapterName.Text = _workbench.SelectedTitle; KeepChapterContent.IsChecked = false;
            ChapterDeleteLayer.IsVisible = true; CancelDeleteChapter.Focus(); return;
        }
        _workbench?.ExecuteChapter(command);
        if (command is "child" or "sibling") Dispatcher.UIThread.Post(() => ChapterTitleInput.Focus(), DispatcherPriority.Background);
    }
    private void BeginRename(MarkdownMindMapNode node)
    {
        if (node.Kind != MarkdownMindMapNodeKind.Heading || Map.GetNodeBounds(node.Id) is not { } rect) return;
        _renamingStart = node.SourceStart; InlineChapterTitle.Text = _workbench?.ChapterTitle;
        InlineChapterTitle.Width = Math.Max(130, rect.Width - 16);
        Canvas.SetLeft(InlineChapterTitle, Math.Clamp(rect.X + 8, 4, Math.Max(4, Map.Bounds.Width - InlineChapterTitle.Width - 4)));
        Canvas.SetTop(InlineChapterTitle, Math.Clamp(rect.Center.Y - 15, 4, Math.Max(4, Map.Bounds.Height - 34)));
        RenameLayer.IsVisible = true; InlineChapterTitle.Focus(); InlineChapterTitle.SelectAll();
    }
    private void EndRename(bool commit)
    {
        var start = _renamingStart; _renamingStart = null;
        if (commit && start is not null && _workbench?.SelectedNode?.SourceStart == start) _workbench.CommitChapterTitle(InlineChapterTitle.Text ?? "");
        RenameLayer.IsVisible = false;
    }
    private void InlineTitleKeyDown(object? sender, KeyEventArgs e)
    { if (e.Key is Key.Enter or Key.Escape) { EndRename(e.Key == Key.Enter); Map.Focus(); e.Handled = true; } }
    private void InlineTitleLostFocus(object? sender, RoutedEventArgs e) => EndRename(true);
    public bool HandleEscape()
    {
        if (RenameLayer.IsVisible) { EndRename(false); Map.Focus(); return true; }
        if (ChapterDeleteLayer.IsVisible) { ChapterDeleteLayer.IsVisible = false; Map.Focus(); return true; }
        return false;
    }
    private void CancelDeleteClick(object? sender, RoutedEventArgs e) { ChapterDeleteLayer.IsVisible = false; Map.Focus(); }
    private void ConfirmDeleteClick(object? sender, RoutedEventArgs e) { _workbench?.DeleteChapter(KeepChapterContent.IsChecked == true); ChapterDeleteLayer.IsVisible = false; Map.Focus(); }
    private void HeadingsOnlyClick(object? sender, RoutedEventArgs e)
    {
        if (_workbench is not { } state) return;
        var only = HeadingsOnlyButton.IsChecked == true;
        if (only) { _savedVisibility = (state.ShowParagraphs, state.ShowLists, state.ShowCode, state.ShowTables); state.ShowParagraphs = state.ShowLists = state.ShowCode = state.ShowTables = false; }
        else { var saved = _savedVisibility ?? (false, false, false, false); state.ShowParagraphs = saved.Item1; state.ShowLists = saved.Item2; state.ShowCode = saved.Item3; state.ShowTables = saved.Item4; }
    }
    private void RefreshHeadingsOnly()
    {
        if (_workbench is { } state) HeadingsOnlyButton.IsChecked = !state.ShowParagraphs && !state.ShowLists && !state.ShowCode && !state.ShowTables;
    }
    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_workbench?.IsMapVisible != true || _workbench.Root is null) return;
        var nodes = Flatten(_workbench.Root);
        var node = nodes.Where(n => n.Kind == MarkdownMindMapNodeKind.Heading && n.SourceLine <= command.Line).OrderByDescending(n => n.SourceLine).FirstOrDefault() ?? _workbench.Root;
        Map.RevealNode(node.Id);
        static IEnumerable<MarkdownMindMapNode> Flatten(MarkdownMindMapNode n) { yield return n; foreach (var c in n.Children) foreach (var d in Flatten(c)) yield return d; }
    }
    public void ZoomPresentation(bool increase) { if (increase) Map.ZoomIn(); else Map.ZoomOut(); }
    /// <summary>演示缩放坞的导图文档位：还原=适应画布，语义同 .map-controls 的 map-fit。</summary>
    public void FitMap() => Map.Fit();
    private void ExpandClick(object? sender, RoutedEventArgs e) => Map.ExpandAll();
    private void CollapseClick(object? sender, RoutedEventArgs e)
    {
        if (HasCollapsed(Map.Root)) Map.ExpandAll(); else Map.CollapseAll();
        CollapseBranchesButton.Content = WorkbenchText.Get(HasCollapsed(Map.Root) ? "WBExpand" : "WBCollapse");
        static bool HasCollapsed(MarkdownMindMapNode? node) => node is not null && (node.IsCollapsed || node.Children.Any(HasCollapsed));
    }

    /// <summary>按当前主题取画刷（弹层每次开启重建，换肤后重开即生效）。</summary>
    private IBrush? ThemeBrush(string key) => this.TryFindResource(key, out var value) ? value as IBrush : null;

    /// <summary>原型 map-appearance.js miniature() 的真几何：同一棵 7 节点树（root→2→4，节点 52×22）
    /// 跑原型自己的布局引擎算出的坐标，画布宽高已含 viewBox 的 12px 留白，节点序号 0 恒为根。</summary>
    private static readonly Dictionary<string, (double W, double H, Rect[] Nodes, string[] Links)> MapGlyphs = new()
    {
        ["right"] = (404, 280,
            new[] { new Rect(40, 117, 52, 22), new Rect(164, 71, 52, 22), new Rect(164, 163, 52, 22), new Rect(288, 48, 52, 22), new Rect(288, 94, 52, 22), new Rect(288, 140, 52, 22), new Rect(288, 186, 52, 22) },
            new[] { "M92,128 C128,128 128,82 164,82", "M92,128 C128,128 128,174 164,174", "M216,82 C252,82 252,59 288,59", "M216,82 C252,82 252,105 288,105", "M216,174 C252,174 252,151 288,151", "M216,174 C252,174 252,197 288,197" }),
        ["balanced"] = (652, 188,
            new[] { new Rect(288, 71, 52, 22), new Rect(412, 71, 52, 22), new Rect(164, 71, 52, 22), new Rect(536, 48, 52, 22), new Rect(536, 94, 52, 22), new Rect(40, 48, 52, 22), new Rect(40, 94, 52, 22) },
            new[] { "M340,82 C376,82 376,82 412,82", "M288,82 C252,82 252,82 216,82", "M464,82 C500,82 500,59 536,59", "M464,82 C500,82 500,105 536,105", "M164,82 C128,82 128,59 92,59", "M164,82 C128,82 128,105 92,105" }),
        ["radial"] = (549.5, 443.9,
            new[] { new Rect(236.8, 199, 52, 22), new Rect(360.8, 199, 52, 22), new Rect(112.8, 199, 52, 22), new Rect(433.5, 48, 52, 22), new Rect(433.5, 349.9, 52, 22), new Rect(40, 349.9, 52, 22), new Rect(40, 48, 52, 22) },
            new[] { "M288.75,209.97 L360.75,209.97", "M236.75,209.97 L164.75,209.97", "M392.05,198.97 L454.20,70", "M392.05,220.97 L454.20,349.95", "M133.45,220.97 L71.30,349.95", "M133.45,198.97 L71.30,70" }),
        ["down"] = (384, 346,
            new[] { new Rect(154, 48, 52, 22), new Rect(78, 150, 52, 22), new Rect(230, 150, 52, 22), new Rect(40, 252, 52, 22), new Rect(116, 252, 52, 22), new Rect(192, 252, 52, 22), new Rect(268, 252, 52, 22) },
            new[] { "M180,70 C180,110 104,110 104,150", "M180,70 C180,110 256,110 256,150", "M104,172 C104,212 66,212 66,252", "M104,172 C104,212 142,212 142,252", "M256,172 C256,212 218,212 218,252", "M256,172 C256,212 294,212 294,252" }),
        ["outline"] = (284, 382,
            new[] { new Rect(40, 48, 52, 22), new Rect(104, 88, 52, 22), new Rect(104, 208, 52, 22), new Rect(168, 128, 52, 22), new Rect(168, 168, 52, 22), new Rect(168, 248, 52, 22), new Rect(168, 288, 52, 22) },
            new[] { "M56,70 V99 H104", "M56,70 V219 H104", "M120,110 V139 H168", "M120,110 V179 H168", "M120,230 V259 H168", "M120,230 V299 H168" }),
        ["fishbone"] = (652, 590,
            new[] { new Rect(536, 272, 52, 22), new Rect(78, 150, 52, 22), new Rect(306, 394, 52, 22), new Rect(40, 48, 52, 22), new Rect(116, 48, 52, 22), new Rect(268, 496, 52, 22), new Rect(344, 496, 52, 22) },
            new[] { "M168,283 H536", "M168,283 L104,172", "M396,283 L332,394", "M104,150 C104,110 66,110 66,70", "M104,150 C104,110 142,110 142,70", "M332,416 C332,456 294,456 294,496", "M332,416 C332,456 370,456 370,496" }),
    };

    /// <summary>原型 .map-choice：88 高等比缩略线稿 + 13px 标签。线稿同时表达两件事——
    /// 布局页按当前风格上色、风格页按当前布局排形（原型 miniature(getLayout(), style)）。
    /// 派生色按 CSS 同款算：清雅=强调色描边根实心、缤纷=分支色轮换 + 14% 铺底 + 实色连线、
    /// 线框=根只留 3px 描边、胶囊=rx12 + 12% 强调色铺底、脉络=子节点只剩底边、黑白=整体转前景色。</summary>
    private Control BuildStyleCard(string label, string shape, string style)
    {
        var accent = ThemeBrush("VexAccentBrush") ?? Brushes.Purple;
        var paperBrush = ThemeBrush("VexPanelBackgroundBrush") ?? Brushes.Transparent;
        var foreground = ThemeBrush("VexEditorForegroundBrush") ?? Brushes.Black;
        var muted = ThemeBrush("VexMutedBrush") ?? Brushes.Gray;
        var paper = SolidColor(paperBrush, Colors.Transparent);
        var accentColor = SolidColor(accent, Colors.Purple);
        var branch = new[]
        {
            accentColor,
            Tint(accentColor, SolidColor(ThemeBrush("VexSuccessBrush") ?? muted, Colors.Green), 0.4),
            Tint(accentColor, SolidColor(ThemeBrush("VexWarningBrush") ?? muted, Colors.Orange), 0.45),
            SolidColor(muted, Colors.Gray),
        };
        var colorful = style == "colorful";
        var mono = style == "mono";
        var hollowRoot = style == "outline";
        var capsule = style == "capsule";
        var veined = style == "underline";
        var glyph = MapGlyphs.TryGetValue(shape, out var found) ? found : MapGlyphs["right"];
        var canvas = new Canvas { Width = glyph.W, Height = glyph.H };
        for (var i = 0; i < glyph.Links.Length; i++)
            canvas.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(glyph.Links[i]),
                Stroke = colorful ? new SolidColorBrush(branch[i % 4]) : mono ? foreground : accent,
                StrokeThickness = 2,
                Opacity = colorful ? 1 : 0.6,
                RenderTransform = new TranslateTransform(12, 12),
            });
        for (var i = 0; i < glyph.Nodes.Length; i++)
        {
            var node = glyph.Nodes[i];
            var root = i == 0;
            var color = colorful && !root ? new SolidColorBrush(branch[i % 4]) : mono ? foreground : accent;
            Control piece = veined && !root
                ? new Avalonia.Controls.Shapes.Rectangle { Width = node.Width, Height = 2, Fill = color }
                : new Avalonia.Controls.Shapes.Rectangle
                {
                    Width = node.Width,
                    Height = node.Height,
                    RadiusX = capsule ? 12 : 4,
                    RadiusY = capsule ? 12 : 4,
                    Stroke = color,
                    StrokeThickness = root && hollowRoot ? 3 : 2,
                    Fill = root
                        ? mono ? foreground : hollowRoot ? paperBrush : accent
                        : colorful ? new SolidColorBrush(Tint(paper, branch[i % 4], 0.14))
                        : capsule ? new SolidColorBrush(Tint(paper, accentColor, 0.12))
                        : paperBrush,
                };
            Canvas.SetLeft(piece, node.X + 12);
            Canvas.SetTop(piece, veined && !root ? node.Bottom + 11 : node.Y + 12);
            canvas.Children.Add(piece);
        }
        var mini = new Border { Classes = { "map-choice-mini" }, Child = new Viewbox { Child = canvas, Stretch = Stretch.Uniform } };
        var name = new TextBlock { Classes = { "map-choice-label" }, Text = label, HorizontalAlignment = HorizontalAlignment.Left };
        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(mini); stack.Children.Add(name);
        return stack;
    }

    private static Color SolidColor(IBrush brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private static Color Tint(Color background, Color overlay, double amount) => Color.FromArgb(
        background.A,
        (byte)(background.R + (overlay.R - background.R) * amount),
        (byte)(background.G + (overlay.G - background.G) * amount),
        (byte)(background.B + (overlay.B - background.B) * amount));

}
