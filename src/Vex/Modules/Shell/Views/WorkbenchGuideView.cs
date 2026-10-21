using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>在独立演示文档中完成六项真实操作；结束后恢复未保存文档与显示偏好。</summary>
public sealed class WorkbenchGuideView : Panel
{
    private static readonly string Demo = $"# 从文字到结构\n\n在这里输入一句话，观察 Markdown 即时渲染。\n\n## 把想法整理清楚\n\n这是一段保留完整正文的演示内容。导图默认最多显示三十个字，你可以调整摘要长度，再点击节点查看全文。\n\n- 写下想法\n  - 补充细节\n- 梳理结构\n- 分享成果\n\n## 下一步\n\n**重点**、[链接]({Vex.Core.Services.ProjectLinks.Website})与引用，都可以在编辑模式中调整。\n\n> 保存 Markdown，或者导出带排版的文档。";
    private readonly Spotlight _mask = new() { IsHitTestVisible = false };
    private readonly Border _card = new() { Width = 320, Padding = new Thickness(18), CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 16 60 0 #40000000") };
    private readonly TextBlock _counter = new() { FontSize = 11 };
    private readonly TextBlock _title = new() { FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
    private readonly TextBlock _feedback = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _skip = new() { Classes = { "vex-dismiss" } };
    private readonly Button _next = new() { Content = L(VexL.GuideNext), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _previous = new() { Content = L(VexL.GuidePrevious) };
    private readonly Button _skipBottom = new() { Classes = { "wb-quiet" } };
    private readonly Expander _more = new() { IsVisible = false, FontSize = 12 };
    private readonly List<(ScrollViewer View, Vector Offset)> _scrollPositions = [];
    private (int Start, int Length) _sourceSelection;
    public IReadOnlyList<Rect> SpotlightTargets => _mask.Targets;
    private readonly Button[] _steps = new Button[6];
    private MainWindowViewModel? _vm;
    private MainWindow? _window;
    private IAppSettingsStore? _settings;
    private AppSettings? _savedSettings;
    private TypographyOption? _savedTypography;
    private Control[] _targets = [];
    private string _savedMarkdown = string.Empty;
    private string _savedView = "edit";
    private string _savedEdit = "instant";
    private bool _sidebar;
    private bool _status;
    private bool _focus;
    private bool _mapSettings;
    private bool _typography;
    private bool _changedTheme;
    private CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownLiveEditorState? _liveState;
    private AvaloniaEdit.Document.TextDocument? _sourceDocument;
    private int _sourceCaret;
    private CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.ViewportState? _mapState;
    private bool _outline;
    private IInputElement? _savedFocus;
    private Vex.Modules.Workspace.ViewModels.MarkdownEditorViewModel? _sourceVm;
    private WorkspaceViewController.FloatingSession? _floatingSession;
    private Vex.Modules.Workspace.Services.WorkspaceEditHistory.Snapshot? _history;
    private int _sidebarTab;
    private bool _savedLayouts;
    public int StepIndex { get; private set; }

    private static string L(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(key) ?? string.Empty;

    public WorkbenchGuideView()
    {
        IsVisible = false;
        var stack = new StackPanel { Spacing = 10 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 12 };
        var caption = new TextBlock { FontSize = 11 };
        caption.Text = L(VexL.WBQuickStart);
        header.Children.Add(caption); Grid.SetColumn(_counter, 1); header.Children.Add(_counter);
        Grid.SetColumn(_skip, 2); header.Children.Add(_skip); _skip.Click += (_, _) => Finish();
        stack.Children.Add(header);
        stack.Children.Add(_title); stack.Children.Add(_description); stack.Children.Add(_feedback);
        stack.Children.Add(_more);
        var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 6; i++)
        {
            var index = i;
            var button = new Button { Width = 39, Height = 12, MinHeight = 0, MinWidth = 0,
                Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new Border { Height = 3, Width = 39, CornerRadius = new CornerRadius(2) } };
            button.Click += (_, _) => ShowStep(index);
            _steps[i] = button; steps.Children.Add(button);
        }
        stack.Children.Add(steps);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 8 };
        _skipBottom.Click += (_, _) => Finish(); actions.Children.Add(_skipBottom);
        Grid.SetColumn(_previous, 1); actions.Children.Add(_previous); Grid.SetColumn(_next, 2); actions.Children.Add(_next); stack.Children.Add(actions);
        _card.Child = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Children.Add(_mask); Children.Add(_card);
        _next.Click += (_, _) => NextStep();
        _previous.Click += (_, _) => ShowStep(Math.Max(0, StepIndex - 1));
        SizeChanged += (_, _) => PositionCard();
    }

    public void Start(MainWindowViewModel vm, MainWindow window, IAppSettingsStore? settings, Control[] targets)
    {
        if (IsVisible) { ShowStep(0); return; }
        _vm = vm; _window = window; _settings = settings; _savedSettings = settings?.Current;
        _history = vm.Workbench.History.Capture();
        _floatingSession = window.WorkspaceViews.SuspendFloating();
        _sidebarTab = vm.Navigation.SelectedSideTabIndex;
        _savedLayouts = vm.Workbench.ShowLayouts;
        _savedMarkdown = vm.Markdown; _savedView = vm.Workbench.View; _savedEdit = vm.Workbench.EditMode;
        _savedTypography = vm.Appearance.SelectedTypography;
        _outline = vm.Layout.IsOutlineVisible; _sidebar = vm.Layout.IsSidebarVisible; _status = vm.Layout.IsStatusBarVisible; _focus = vm.Layout.IsFocusMode;
        _mapSettings = vm.Workbench.ShowMapSettings; _typography = vm.Workbench.ShowTypography;
        _targets = targets;
        _savedFocus = window.FocusManager?.GetFocusedElement();
        _liveState = vm.Workbench.LiveEditor?.CaptureState();
        var sourceView = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!;
        _sourceVm = sourceView.DataContext as Vex.Modules.Workspace.ViewModels.MarkdownEditorViewModel;
        _sourceSelection = sourceView.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.Selection;
        _scrollPositions.Clear();
        foreach (var scroll in window.GetVisualDescendants().OfType<ScrollViewer>()) _scrollPositions.Add((scroll, scroll.Offset));
        _sourceCaret = sourceView.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.Editor.CaretOffset;
        _sourceDocument = _sourceVm?.BeginTemporaryDocument();
        _mapState = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!
            .FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!.CaptureViewport();
        vm.BeginGuideDocument(Demo);
        vm.Workbench.ResetHistory();
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Vex.Core.Messaging.MarkdownTextChangedCommand(Demo, 1, 1, Demo.Count(c => c == '\n') + 1));
        vm.Workbench.SummaryLength = 30; vm.Workbench.ShowParagraphs = true; vm.Workbench.ShowLists = true;
        vm.Workbench.ShowCode = false; vm.Workbench.ShowTables = false;
        vm.Layout.IsFocusMode = false; vm.Layout.IsSidebarVisible = true; vm.Layout.IsStatusBarVisible = true;
        _changedTheme = false;
        vm.Appearance.PropertyChanged += AppearanceChanged;
        vm.Workbench.PropertyChanged += WorkbenchChanged;
        vm.PropertyChanged += DocumentChanged;
        IsVisible = true;
        vm.Workbench.IsGuiding = true;
        _card.Transitions = null;
        ShowStep(0);
    }
    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellAppearanceViewModel.SelectedTypography)) { _changedTheme = true; UpdateFeedback(); }
    }
    private void WorkbenchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (StepIndex == 2 && e.PropertyName == nameof(ShellWorkbenchViewModel.Root))
            Dispatcher.UIThread.Post(() => _window?.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")?.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")?.Fit(.65));
        UpdateFeedback();
    }
    private void DocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Markdown)) UpdateFeedback();
    }
    public void NextStep() { if (StepIndex >= 5) Finish(); else ShowStep(StepIndex + 1); }
    public void ShowStep(int index)
    {
        if (_vm is null || !IsVisible) return;
        StepIndex = Math.Clamp(index, 0, 5);
        _vm.Workbench.GuideStep = StepIndex;
        ToolTip.SetTip(_skip, L(VexL.GuideSkip)); Avalonia.Automation.AutomationProperties.SetName(_skip, L(VexL.GuideSkip)); _previous.Content = L(VexL.GuidePrevious);
        _skipBottom.Content = L(VexL.GuideSkip);
        _more.IsVisible = StepIndex == 5; _more.Header = WorkbenchText.Get("GuideAIHeading");
        _more.Content = new TextBlock { Text = WorkbenchText.Get("GuideAIHint"), TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 21 };
        for (var i = 0; i < _steps.Length; i++)
        {
            if (_steps[i].Content is Border segment) segment.Background = FindBrush(i == StepIndex ? "VexAccentBrush" : "VexPanelBorderBrush", "#DFE1EB");
        }
        _counter.Text = string.Format(L(VexL.GuideStepFormat), StepIndex + 1);
        _previous.IsVisible = StepIndex > 0; _next.Content = StepIndex == 5 ? L(VexL.GuideStart) : L(VexL.GuideNext);
        _next.Classes.Add("primary");
        _vm.Workbench.ShowTypography = false; _vm.Workbench.ShowMapSettings = false; _vm.Workbench.ShowLayouts = false;
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem activeMenu) activeMenu.IsSubMenuOpen = false;
        _title.Text = WorkbenchText.Get("GuideTitle" + StepIndex);
        _description.Text = WorkbenchText.Get("GuideDescription" + StepIndex);
        switch (StepIndex)
        {
            case 0: _vm.Workbench.SelectLayout("instant"); break;
            case 1: _vm.Workbench.SelectLayout("instant"); _vm.Layout.ShowOutline(); break;
            case 2:
                _vm.Workbench.SelectLayout("map"); _vm.Workbench.ShowParagraphs = _vm.Workbench.ShowLists = _vm.Workbench.ShowCode = _vm.Workbench.ShowTables = false;
                break;
            case 3: _vm.Workbench.SelectLayout("instant-map"); break;
            case 4: _vm.Workbench.SelectLayout("instant"); break;
            case 5: _vm.Workbench.SelectLayout("read"); break;
        }
        UpdateFeedback();
        _next.Focus();
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible || _window is null) return;
            if (StepIndex is 0 or 4 or 5)
                foreach (var scroll in _targets[StepIndex == 5 ? 0 : StepIndex].GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
            _window.UpdateLayout(); PositionCard();
        }, DispatcherPriority.Render);
    }
    private void UpdateFeedback()
    {
        if (_vm is null) return;
        _feedback.Text = StepIndex switch
        {
            0 when _vm.Markdown != Demo => L(VexL.GuideFeedbackEdited),
            2 when _vm.Markdown != Demo => WorkbenchText.Get("GuideChapterDone"),
            4 when _changedTheme => L(VexL.GuideFeedbackTheme),
            _ => WorkbenchText.Get("GuideTip" + StepIndex)
        };
        if (IsVisible) Dispatcher.UIThread.Post(PositionCard, DispatcherPriority.Background);
    }
    private void PositionCard()
    {
        if (!IsVisible || _targets.Length < 6) return;
        var highlights = new List<Rect>();
        var target = _targets[StepIndex];
        if (StepIndex == 0 && _vm?.Workbench.IsSourceEditing == true && _window?.FindControl<Control>("EditorGuideTarget") is { } source) target = source;
        if (StepIndex == 0 && _vm?.Workbench.IsInstantEditing == true
            && _window?.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget") is { } instant)
        {
            var paragraph = instant.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
                .FirstOrDefault(viewer => viewer.Markdown?.StartsWith("在这里输入", StringComparison.Ordinal) == true);
            if (paragraph is not null) AddTarget(paragraph); else AddTarget(target);
        }
        else if (StepIndex == 2 && _window?.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget") is { } host
            && host.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map") is { } map && _vm?.Workbench.Root is { } root)
        {
            var nodes = AllNodes(root).Select(node => map.GetNodeBounds(node.Id)).OfType<Rect>().ToArray();
            if (nodes.Length > 0 && map.TranslatePoint(default, this) is { } origin)
            {
                var union = nodes.Aggregate((left, right) => left.Union(right));
                var area = new Rect(union.Position + origin, union.Size).Inflate(6).Intersect(new Rect(Bounds.Size));
                if (area.Width > 0 && area.Height > 0) highlights.Add(area);
            }
            if (host.FindControl<Border>("NodeInspector") is { IsEffectivelyVisible: true } inspector) AddTarget(inspector);
        }
        else AddTarget(target);
        if (StepIndex is 1 or 3 or 4 && _window?.FindControl<Control>("ViewSurface") is { } surface) AddTarget(surface);
        if (StepIndex == 0 && _window?.FindControl<ShellEditorOptionsView>("EditorOptions") is { } options) AddTarget(options);
        if (StepIndex == 3 && _window?.FindControl<WorkspaceLayoutGallery>("LayoutGallery") is { IsVisible: true } layouts) AddTarget(layouts);
        if (StepIndex == 4 && _window?.FindControl<Control>("TypographyGallery") is { IsVisible: true } typography) AddTarget(typography);
        if (highlights.Count == 0) highlights.Add(new Rect(20,20,240,40));
        var rect = highlights[0];
        _mask.Targets = highlights; _mask.Accent = FindBrush("VexAccentBrush", "#7457E8");
        _card.Background = FindBrush("VexPanelBackgroundBrush", "#FFFFFF");
        _card.BorderBrush = FindBrush("VexPanelBorderBrush", "#DFE1EB");
        _title.Foreground = _description.Foreground = FindBrush("VexEditorForegroundBrush", "#333344");
        _counter.Foreground = _feedback.Foreground = FindBrush("VexMutedBrush", "#77778A");
        _card.Margin = new Thickness(0);
        _card.MaxHeight = Math.Max(100, Bounds.Height - 40); _card.Measure(new Size(_card.Width, _card.MaxHeight));
        var width = _card.Width; var height = Math.Min(_card.DesiredSize.Height, Bounds.Height - 40);
        var candidates = new[] { new Point(24, Bounds.Height - height - 24), new Point(Bounds.Width - width - 24, Bounds.Height - height - 24), new Point(rect.Right + 20, rect.Y), new Point(rect.Left - width - 20, rect.Y) }
            .Select(p => new Rect(Math.Clamp(p.X, 20, Math.Max(20, Bounds.Width - width - 20)), Math.Clamp(p.Y, 20, Math.Max(20, Bounds.Height - height - 20)), width, height));
        var placement = StepIndex switch
        {
            0 => new Rect(Math.Clamp(rect.X + 12, 18, Bounds.Width - width - 18), Math.Clamp(rect.Bottom + 18, 18, Bounds.Height - height - 18), width, height),
            2 => new Rect(18, Math.Clamp(target.TranslatePoint(default, this)?.Y + 64 ?? 110, 18, Bounds.Height - height - 18), width, height),
            3 or 4 => candidates.First(),
            5 => new Rect(Bounds.Width - width - 18, 82, width, height),
            _ => candidates.OrderBy(candidate => highlights.Sum(h => { var overlap = candidate.Intersect(h); return overlap.Width * overlap.Height; })).First()
        };
        _card.HorizontalAlignment = HorizontalAlignment.Left; _card.VerticalAlignment = VerticalAlignment.Top;
        _card.Margin = new Thickness(placement.X, placement.Y, 0, 0); _mask.Card = placement;
        void AddTarget(Control control)
        {
            if (!control.IsEffectivelyVisible || control.TranslatePoint(default, this) is not { } point) return;
            var area = new Rect(point, control.Bounds.Size).Inflate(4).Intersect(new Rect(Bounds.Size));
            if (area.Width > 0 && area.Height > 0) highlights.Add(area);
        }
        static IEnumerable<CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode> AllNodes(CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode node)
        {
            yield return node;
            foreach (var child in node.Children) foreach (var descendant in AllNodes(child)) yield return descendant;
        }
    }
    private IBrush FindBrush(string key, string fallback) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brush.Parse(fallback);
    public bool AllowsPointer(Visual source)
    {
        if (!IsVisible) return true;
        var path = source.GetVisualAncestors().Prepend(source).ToArray();
        if (path.Contains(_card)) return true;
        if (_window is null) return false;
        bool Within(string name) => _window.FindControl<Control>(name) is { } control && path.Contains(control);
        return StepIndex switch
        {
            0 => Within("InstantGuideTarget") || Within("EditorGuideTarget") || Within("EditorOptions"),
            1 => path.OfType<ShellOutlineView>().Any() || path.OfType<TabItem>().Any()
                || path.OfType<ShellFilesView>().Any() && path.OfType<TextBox>().Any(),
            2 => Within("MindMapGuideTarget"),
            3 => Within("LayoutGallery") && !path.OfType<Button>().Any(b => b.Name == "SeparateViewsButton")
                || path.OfType<Control>().Any(c => c.Name is "LayoutButton" or "PerspectiveTarget"),
            4 => Within("TypographyGallery") || Within("EditorOptions"),
            5 => path.Contains(_targets[5]) || path.OfType<Control>().Any(c => c.Name is "SaveButton" or "ToolsMenuItem"),
            _ => false
        };
    }
    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsVisible) return false;
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift && _window is not null)
        {
            var controls = _window.GetVisualDescendants().OfType<Control>()
                .Where(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled && AllowsPointer(control)).ToArray();
            var current = Array.IndexOf(controls, _window.FocusManager?.GetFocusedElement() as Control);
            if (controls.Length > 0) controls[(current + (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? controls.Length - 1 : 1) + controls.Length) % controls.Length].Focus(NavigationMethod.Tab);
            return true;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.O or Key.N or Key.W or Key.P or Key.F or Key.H or Key.S)
            return !(StepIndex == 5 && e.Key == Key.S);
        if (e.Key is Key.F5 or Key.F8 or Key.F11 || e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Enter) return true;
        var input = e.Source as Visual;
        var editing = input is TextBox or AvaloniaEdit.Editing.TextArea || input?.GetVisualAncestors().Any(v => v is TextBox or AvaloniaEdit.Editing.TextArea) == true;
        if (e.Key == Key.Escape && !editing) { Finish(); return true; }
        if (editing || e.KeyModifiers != KeyModifiers.None) return false;
        if (e.Key == Key.Right) { NextStep(); return true; }
        if (e.Key == Key.Left) { ShowStep(StepIndex - 1); return true; }
        return false;
    }
    public void Finish()
    {
        if (!IsVisible || _vm is null) return;
        IsVisible = false;
        _vm.Workbench.IsGuiding = false;
        _card.Transitions = null;
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem menu) menu.IsSubMenuOpen = false;
        _vm.Appearance.PropertyChanged -= AppearanceChanged;
        _vm.Workbench.PropertyChanged -= WorkbenchChanged;
        _vm.PropertyChanged -= DocumentChanged;
        _vm.EndGuideDocument();
        _sourceVm?.RestoreDocument(_sourceDocument);
        _vm.Appearance.SelectedTypography = _savedTypography;
        if (_liveState is { } live && _vm.Workbench.LiveEditor is { } editor) editor.RestoreState(live);
        _vm.Workbench.RestorePreferences(_savedSettings, _savedView, _savedEdit);
        _vm.Workbench.ShowMapSettings = _mapSettings; _vm.Workbench.ShowTypography = _typography;
        _vm.Workbench.ShowLayouts = _savedLayouts;
        _vm.Navigation.SelectedSideTabIndex = _sidebarTab;
        if (_history is { } history) _vm.Workbench.History.Restore(history);
        if (_floatingSession is { } floating) _window?.WorkspaceViews.RestoreFloating(floating);
        _floatingSession = null;
        _vm.Layout.IsOutlineVisible = _outline; _vm.Layout.IsFocusMode = _focus; _vm.Layout.IsSidebarVisible = _sidebar; _vm.Layout.IsStatusBarVisible = _status;
        if (_savedSettings is { } saved) _settings?.Update(_ => saved with { HasSeenOnboardingGuide = true });
        if (_window is not null) _window.FindControl<ShellWorkbenchToolbarView>("WorkbenchToolbar")!.QuickStartButton.IsVisible = false;
        var window = _window;
        var mapState = _mapState;
        var focus = _savedFocus;
        var caret = _sourceCaret;
        var selection = _sourceSelection;
        var scrollPositions = _scrollPositions.ToArray();
        Dispatcher.UIThread.Post(() =>
        {
            if (window is null) return;
            window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!
                .FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.Editor.CaretOffset = caret;
            if (mapState is { } state) window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!
                .FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!.RestoreViewport(state, onNextRoot: true);
            var restoredSource = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.Editor;
            restoredSource.Select(selection.Start, selection.Length);
            foreach (var (view, offset) in scrollPositions) view.Offset = offset;
            focus?.Focus();
        }, DispatcherPriority.Background);
        _vm = null;
    }

    private sealed class Spotlight : Control
    {
        private IReadOnlyList<Rect> _targets = [];
        public IReadOnlyList<Rect> Targets { get => _targets; set { _targets = value; InvalidateVisual(); } }
        private Rect _card;
        public Rect Card { get => _card; set { _card = value; InvalidateVisual(); } }
        public IBrush Accent { get; set; } = Brushes.MediumPurple;
        public override void Render(DrawingContext context)
        {
            Geometry? holes = null;
            foreach (var target in Targets)
            {
                var hole = new RectangleGeometry(target, 7, 7);
                holes = holes is null ? hole : new CombinedGeometry(GeometryCombineMode.Union, holes, hole);
            }
            Geometry geometry = holes is null ? new RectangleGeometry(new Rect(Bounds.Size))
                : new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(Bounds.Size)), holes);
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#24243E"), .52), null, geometry);
            foreach (var target in Targets) context.DrawRectangle(null, new Pen(Accent, 1), target, 7, 7);
        }
    }
}
