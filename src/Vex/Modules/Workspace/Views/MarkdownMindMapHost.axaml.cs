using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Reactive.Linq;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.MindView.Markdown;
using Avalonia.Interactivity;
using Avalonia.Media;
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
    private int? _inspectedStart;
    private int? _renamingStart;
    private (bool Paragraphs, bool Lists, bool Code, bool Tables)? _savedVisibility;
    private Vex.Core.Services.EditorPreferences? _pinPreferences;

    public MarkdownMindMapHost()
    {
        InitializeComponent();
        BuildMapStyleFlyout();
    }

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
        // 对齐原型截图：标题栏（导图样式 + ×）→ 结构/风格双页签 → 卡片缩略图网格。
        var panel = new StackPanel { Spacing = 10, MinWidth = 430 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var headerTitle = new TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        var closeButton = new Button { Classes = { "vex-dismiss" }, Content = "×", Width = 26, Height = 26, Padding = new Thickness(0) };
        header.Children.Add(headerTitle); Grid.SetColumn(closeButton, 1); header.Children.Add(closeButton);
        panel.Children.Add(header);

        var tabs = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 4 };
        var layoutTab = new RadioButton { GroupName = "map-popup-tab", Classes = { "map-popup-tab" }, Content = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.WBMapDisplay") ?? "结构" };
        var styleTab = new RadioButton { GroupName = "map-popup-tab", Classes = { "map-popup-tab" }, Content = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.WBMapStyleButton") ?? "风格", IsChecked = true };
        Grid.SetColumn(layoutTab, 0); tabs.Children.Add(layoutTab);
        Grid.SetColumn(styleTab, 1); tabs.Children.Add(styleTab);
        panel.Children.Add(tabs);

        var layoutPage = new StackPanel { Spacing = 8 };
        var stylePage = new StackPanel { Spacing = 8 };
        panel.Children.Add(layoutPage); panel.Children.Add(stylePage);

        var layoutCards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 8, RowSpacing = 8 };
        var styleCards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 8, RowSpacing = 8 };
        layoutPage.Children.Add(layoutCards); stylePage.Children.Add(styleCards);

        var flyoutHost = new ContentControl();
        var flyout = new Flyout { Content = flyoutHost, Placement = PlacementMode.BottomEdgeAlignedLeft };
        // 触发钮插入工具栏（对照源码旁，原型顺序），随工具条悬浮显示。
        MapToolsRight.Children.Insert(MapToolsRight.Children.IndexOf(MapSourceButton) + 1, button);
        closeButton.Click += (_, _) => flyout.Hide();

        void ShowPage(bool layout)
        {
            layoutPage.IsVisible = layout; stylePage.IsVisible = !layout;
            layoutTab.IsChecked = layout; styleTab.IsChecked = !layout;
        }
        layoutTab.Click += (_, _) => ShowPage(true);
        styleTab.Click += (_, _) => ShowPage(false);

        var refresh = () =>
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var vm = shell.Workbench;
            headerTitle.Text = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.WBMapStyleButton") ?? "导图样式";
            layoutCards.Children.Clear(); styleCards.Children.Clear();
            foreach (var key in ShellWorkbenchViewModel.MindMapLayoutKeys)
            {
                var captured = key;
                var selected = vm.MindMapLayout == key;
                var card = new RadioButton
                {
                    GroupName = "map-layout", IsChecked = selected,
                    Classes = { "map-style-card" },
                    Content = BuildStyleCard(ShellWorkbenchViewModel.MindMapLayoutKeyLabel(key), selected, StyleGlyph(key, layout: true)),
                };
                card.Click += (_, _) => vm.MindMapLayout = captured;
                Grid.SetRow(card, layoutCards.RowDefinitions.Count);
                layoutCards.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetColumn(card, layoutCards.Children.Count % 3);
                layoutCards.Children.Add(card);
            }
            foreach (var key in ShellWorkbenchViewModel.MindMapStyleKeys)
            {
                var captured = key;
                var selected = vm.MindMapStyle == key;
                var card = new RadioButton
                {
                    GroupName = "map-style", IsChecked = selected,
                    Classes = { "map-style-card" },
                    Content = BuildStyleCard(ShellWorkbenchViewModel.MindMapStyleKeyLabel(key), selected, StyleGlyph(key, layout: false)),
                };
                card.Click += (_, _) => vm.MindMapStyle = captured;
                Grid.SetRow(card, styleCards.RowDefinitions.Count);
                styleCards.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetColumn(card, styleCards.Children.Count % 3);
                styleCards.Children.Add(card);
            }
        };
        flyout.Opening += (_, _) => { refresh(); ShowPage(styleTab.IsChecked == true); flyoutHost.Content = panel; };
        Map.SourceRequested += (_, node) =>
        {
            if (_workbench?.IsPresenting == true)
            {
                var section = _workbench.PresentationSections.LastOrDefault(section => section.Start <= node.SourceStart) ?? _workbench.PresentationSections.FirstOrDefault();
                if (section is not null) _workbench.NavigatePresentation(section.Index);
            }
            else (DataContext as MainWindowViewModel)?.Workbench.LocateSource(node);
        };
        Map.ChapterActionRequested += (_, action) => { if (_workbench is null) return; _workbench.SelectedNode = action.Node; if (action.Action == "rename") BeginRename(action.Node); else ExecuteChapter(action.Action); };
        Map.ChapterMoveRequested += (_, move) => { FlushInspector(); _workbench?.MoveChapter(move.Node.SourceStart, move.Target.SourceStart, move.Position); };
        Map.ZoomChanged += (_, _) => ZoomLabel.Content = $"{Map.Zoom:P0}";
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
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsVisible" && IsVisible)
                Avalonia.Threading.Dispatcher.UIThread.Post(Map.EnsureViewport, Avalonia.Threading.DispatcherPriority.Render);
        };
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
        RefreshInspector();
        RefreshHeadingsOnly();
    }
    private void WorkbenchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.IsPresenting))
        { MapHint.IsVisible = Bounds.Width >= 480 && _workbench?.IsPresenting != true; Map.IsChapterEditingEnabled = _workbench?.IsPresenting != true; }
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
    private void ZoomOutClick(object? sender, RoutedEventArgs e) => Map.ZoomOut();
    public void ZoomPresentation(bool increase) { if (increase) Map.ZoomIn(); else Map.ZoomOut(); }
    private void ZoomInClick(object? sender, RoutedEventArgs e) => Map.ZoomIn();
    private void ResetZoomClick(object? sender, RoutedEventArgs e) => Map.ResetZoom();
    private void FitClick(object? sender, RoutedEventArgs e) => Map.Fit();
    private void ExpandClick(object? sender, RoutedEventArgs e) => Map.ExpandAll();
    private void CollapseClick(object? sender, RoutedEventArgs e)
    {
        if (HasCollapsed(Map.Root)) Map.ExpandAll(); else Map.CollapseAll();
        CollapseBranchesButton.Content = WorkbenchText.Get(HasCollapsed(Map.Root) ? "WBExpand" : "WBCollapse");
        static bool HasCollapsed(MarkdownMindMapNode? node) => node is not null && (node.IsCollapsed || node.Children.Any(HasCollapsed));
    }

    /// <summary>卡片缩略图线稿：双层树形示意（布局差异以方向表达，风格差异以描边/填充表达）。</summary>
    private static StackPanel BuildStyleCard(string label, bool selected, (bool Vertical, bool Fill, bool Dashed) glyph)
    {
        var canvas = new Canvas { Width = 120, Height = 64, HorizontalAlignment = HorizontalAlignment.Center };
        SolidColorBrush stroke = glyph.Fill ? new SolidColorBrush(Color.Parse("#8B7CF6")) : new SolidColorBrush(Color.Parse("#B0B0B8"));
        var nodeFill = glyph.Fill ? (IBrush)new SolidColorBrush(Color.Parse("#ECE7FB")) : (IBrush)Brushes.White;
        void Node(double x, double y, double w, double h, bool root)
        {
            var rect = new Border
            {
                Width = w, Height = h,
                CornerRadius = new CornerRadius(glyph.Vertical ? 3 : 4),
                BorderThickness = new Thickness(1.2),
                BorderBrush = stroke,
                Background = root ? new SolidColorBrush(Color.Parse("#3A3A40")) : nodeFill,
            };
            Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);
        }
        void Link(double x1, double y1, double x2, double y2)
        {
            var line = new Avalonia.Controls.Shapes.Path
            {
                Stroke = glyph.Fill ? stroke : new SolidColorBrush(Color.Parse("#B8B8C0")),
                StrokeThickness = glyph.Fill ? 2 : 1.2,
                Data = Geometry.Parse(glyph.Vertical
                    ? $"M {x1},{y1} L {x2},{y2}"
                    : $"M {x1},{y1} C {x1 + 14},{y1} {x2 - 14},{y2} {x2},{y2}"),
            };
            canvas.Children.Add(line);
        }
        if (glyph.Vertical)
        {
            Node(52, 6, 24, 10, root: true);
            Link(64, 16, 24, 34); Link(64, 16, 64, 34); Link(64, 16, 100, 34);
            Node(12, 34, 24, 10, false); Node(52, 34, 24, 10, false); Node(88, 34, 24, 10, false);
            Node(12, 50, 24, 10, false); Node(88, 50, 24, 10, false);
        }
        else
        {
            Node(8, 26, 26, 11, root: true);
            Link(34, 31, 52, 16); Link(34, 31, 52, 46); Link(34, 31, 52, 31);
            Node(52, 10, 26, 11, false); Node(52, 40, 26, 11, false); Node(52, 25, 26, 11, false);
            Link(78, 15, 94, 12); Link(78, 45, 94, 50); Link(78, 30, 94, 30);
            Node(94, 6, 22, 10, false); Node(94, 45, 22, 10, false); Node(94, 25, 22, 10, false);
        }
        var name = new TextBlock { Text = label, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 4, 0, 0) };
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(canvas); stack.Children.Add(name);
        return stack;
    }

    /// <summary>布局/风格的缩略线稿参数。</summary>
    private static (bool Vertical, bool Fill, bool Dashed) StyleGlyph(string key, bool layout) => layout
        ? key switch
        {
            "balanced" => (false, false, false),
            "radial" => (true, false, false),
            "down" => (true, false, false),
            "outline" => (true, false, true),
            "fishbone" => (false, false, true),
            _ => (false, false, false),
        }
        : key switch
        {
            "colorful" => (false, true, false),
            "outline" => (false, false, false),
            "capsule" => (false, false, false),
            "underline" => (false, false, false),
            "mono" => (false, false, true),
            _ => (false, false, false),
        };

}
