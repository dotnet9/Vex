using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>六幕引导视图：聚光遮罩、卡片与气泡的呈现、锚点解析与输入转发；编排逻辑在 <see cref="WorkbenchGuideViewModel"/>。</summary>
public sealed class WorkbenchGuideView : Panel, WorkbenchGuideViewModel.IStage
{
    private readonly WorkbenchGuideViewModel _vm = new();
    private readonly Spotlight _mask = new() { IsHitTestVisible = false };
    private readonly Border _card = new() { Width = 320, Padding = new Thickness(18), CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 16 60 0 #40000000") };
    private readonly ContentControl _bubbleHost = new() { IsVisible = false, IsHitTestVisible = false };
    private readonly Border _bubble = new() { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
    private readonly TextBlock _bubbleText = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 260 };
    private readonly TextBlock _counter = new() { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title = new() { FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
    private readonly TextBlock _feedback = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
    private readonly Button _skip = new() { Classes = { "vex-dismiss" }, Content = "×" };
    private readonly Button _next = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _previous = new();
    private readonly Button _skipBottom = new() { Classes = { "wb-quiet" } };
    private readonly Button[] _steps = new Button[6];
    private readonly List<Control> _focusTargets = [];
    private readonly List<(ScrollViewer View, Vector Offset)> _scrollPositions = [];
    private (int Start, int Length) _sourceSelection;
    public IReadOnlyList<Rect> SpotlightTargets => _mask.Targets;
    private MainWindow? _window;
    private MainWindowViewModel? _shellVm;
    private IAppSettingsStore? _settings;
    private AppSettings? _savedSettings;
    private TypographyOption? _savedTypography;
    private string _savedView = "edit";
    private string _savedEdit = "instant";
    private bool _sidebar;
    private bool _status;
    private bool _focus;
    private bool _mapSettings;
    private bool _typography;
    private (bool Editor, bool Reading, bool Map)? _savedPins;
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
    public int StepIndex => _vm.StepIndex;

    public WorkbenchGuideView()
    {
        IsVisible = false;
        _bubble.Child = _bubbleText; _bubbleHost.Content = _bubble;
        var stack = new StackPanel { Spacing = 10 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_title); Grid.SetColumn(_skip, 1); header.Children.Add(_skip); _skip.Click += (_, _) => Finish();
        stack.Children.Add(header);
        stack.Children.Add(_description); stack.Children.Add(_feedback);
        // 点状进度：每幕一颗，当前幕加长高亮（对应原型 story-dots），可点击直接跳幕。
        var dotsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 6; i++)
        {
            var index = i;
            var button = new Button { Height = 14, MinHeight = 0, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new Border { Height = 6, Width = 8, CornerRadius = new CornerRadius(3) } };
            button.Click += (_, _) => { if (IsVisible) _vm.StartAct(index); };
            _steps[i] = button; dotsRow.Children.Add(button);
        }
        dotsRow.Children.Add(_counter);
        stack.Children.Add(dotsRow);
        // 动作行：左侧结束导览/跳过本段，右侧上一步 + 继续/下一步（主胶囊），无箭头。
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        _skipBottom.Click += (_, _) => { if (_vm.Phase == WorkbenchGuideViewModel.GuidePhase.Intro) Finish(); else _vm.SkipAct(); };
        actions.Children.Add(_skipBottom);
        Grid.SetColumn(_previous, 2); actions.Children.Add(_previous);
        Grid.SetColumn(_next, 3); actions.Children.Add(_next);
        stack.Children.Add(actions);
        _card.Child = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Children.Add(_mask); Children.Add(_card); Children.Add(_bubbleHost);
        _next.Click += (_, _) => _vm.Advance();
        _previous.Click += (_, _) => { if (_vm.CanGoPrevious) _vm.StartAct(_vm.StepIndex - 1); };
        _vm.FinishRequested += Finish;
        _vm.PropertyChanged += (_, e) => ApplyCardProperty(e.PropertyName);
        ApplyCardProperty(null);
        SizeChanged += (_, _) => PositionCard();
    }

    public void Start(MainWindowViewModel vm, MainWindow window, IAppSettingsStore? settings, Control[] targets)
    {
        if (IsVisible) return;
        _window = window; _shellVm = vm; _settings = settings; _savedSettings = settings?.Current;
        _history = vm.Workbench.History.Capture();
        _floatingSession = window.WorkspaceViews.SuspendFloating();
        _sidebarTab = vm.Navigation.SelectedSideTabIndex;
        _savedLayouts = vm.Workbench.ShowLayouts;
        _savedView = vm.Workbench.View; _savedEdit = vm.Workbench.EditMode;
        _savedTypography = vm.Appearance.SelectedTypography;
        _outline = vm.Layout.IsOutlineVisible; _sidebar = vm.Layout.IsSidebarVisible; _status = vm.Layout.IsStatusBarVisible; _focus = vm.Layout.IsFocusMode;
        _mapSettings = vm.Workbench.ShowMapSettings; _typography = vm.Workbench.ShowTypography;
        // 引导门控要高亮工具条内的入口，全程钉住三处工具条，避免目标悬浮隐藏成空高亮。
        var preferences = vm.Workbench.Preferences;
        _savedPins = (preferences.PinEditorToolbar, preferences.PinReadingToolbar, preferences.PinMapToolbar);
        preferences.PinEditorToolbar = true; preferences.PinReadingToolbar = true; preferences.PinMapToolbar = true;
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
        vm.BeginGuideDocument(string.Empty);
        vm.Workbench.ResetHistory();
        vm.Workbench.SummaryLength = 30; vm.Workbench.ShowParagraphs = true; vm.Workbench.ShowLists = true;
        vm.Workbench.ShowCode = false; vm.Workbench.ShowTables = false;
        vm.Layout.IsFocusMode = false; vm.Layout.IsSidebarVisible = true; vm.Layout.IsStatusBarVisible = true;
        vm.Appearance.PropertyChanged += AppearanceChanged;
        IsVisible = true;
        _card.Transitions = null;
        _vm.Begin(vm, this);
        _next.Focus();
    }

    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshColors();

    private void ApplyCardProperty(string? propertyName)
    {
        _title.Text = _vm.CardTitle;
        _description.Text = _vm.CardDescription;
        _description.IsVisible = _vm.CardDescriptionVisible;
        _feedback.Text = _vm.CardMessage;
        _next.Content = _vm.NextLabel;
        _next.IsEnabled = _vm.NextEnabled;
        _previous.IsVisible = _vm.PreviousVisible;
        _previous.Content = WorkbenchText.Get("GuidePrevious");
        _skipBottom.Content = _vm.SkipLabel;
        _skipBottom.IsVisible = _vm.SkipVisible;
        _counter.Text = _vm.CounterText;
        ToolTip.SetTip(_skip, WorkbenchText.Get("GuideSkip"));
        Avalonia.Automation.AutomationProperties.SetName(_skip, WorkbenchText.Get("GuideSkip"));
        _next.Classes.Add("primary");
        RefreshColors();
    }

    private void RefreshColors()
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            if (_steps[i].Content is Border dot)
            {
                dot.Background = FindBrush(i == StepIndex ? "VexAccentBrush" : "VexPanelBorderBrush", i == StepIndex ? "#7457E8" : "#DFE1EB");
                dot.Width = i == StepIndex ? 20 : 8;
            }
        }
        _card.Background = FindBrush("VexPanelBackgroundBrush", "#FFFFFF");
        _card.BorderBrush = FindBrush("VexPanelBorderBrush", "#DFE1EB");
        _bubble.Background = FindBrush("VexPanelBackgroundBrush", "#FFFFFF");
        _bubble.BorderBrush = FindBrush("VexAccentBrush", "#7457E8");
        _title.Foreground = _description.Foreground = _feedback.Foreground = _bubbleText.Foreground = FindBrush("VexEditorForegroundBrush", "#333344");
        _counter.Foreground = FindBrush("VexMutedBrush", "#77778A");
    }

    private IBrush FindBrush(string key, string fallback) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brush.Parse(fallback);

    public void NextStep() => _vm.Advance();

    public void ShowStep(int index) { if (IsVisible) _vm.StartAct(index); }

    /// <summary>锚点语义键 → 实际控件。</summary>
    private Control? Resolve(string anchor)
    {
        if (_window is null) return null;
        return anchor switch
        {
            WorkbenchGuideViewModel.Anchor.Instant => _window.FindControl<Control>("InstantGuideTarget"),
            WorkbenchGuideViewModel.Anchor.Editor => _window.FindControl<Control>("EditorGuideTarget"),
            WorkbenchGuideViewModel.Anchor.Preview => _window.FindControl<Control>("PreviewGuideTarget"),
            WorkbenchGuideViewModel.Anchor.Map => _window.FindControl<Control>("MindMapGuideTarget"),
            WorkbenchGuideViewModel.Anchor.OutlinePanel => _window.FindControl<Control>("OutlineGuideTarget"),
            WorkbenchGuideViewModel.Anchor.OutlineToggle => _window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")?.FindControl<ToggleButton>("OutlineToggle"),
            WorkbenchGuideViewModel.Anchor.TypographyButton => _window.FindControl<ShellEditorOptionsView>("EditorOptions")?.FindControl<Button>("TypographyTarget"),
            WorkbenchGuideViewModel.Anchor.LayoutButton => _window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")?.FindControl<Button>("LayoutButton"),
            WorkbenchGuideViewModel.Anchor.LayoutGallery => _window.FindControl<WorkspaceLayoutGallery>("LayoutGallery"),
            WorkbenchGuideViewModel.Anchor.PresentationTools => _window.FindControl<Control>("PresentationTools"),
            WorkbenchGuideViewModel.Anchor.AppearanceButton => _window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")?.AppearanceButton,
            WorkbenchGuideViewModel.Anchor.AppearanceMenu => _window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")?.MenuContent,
            WorkbenchGuideViewModel.Anchor.MenuItemFile => _window.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget,
            WorkbenchGuideViewModel.Anchor.MenuTools => _window.FindControl<ShellTitleMenuView>("TitleMenuView")?.FindControl<MenuItem>("ToolsMenuItem"),
            WorkbenchGuideViewModel.Anchor.MenuStrip => _window.FindControl<ShellTitleMenuView>("TitleMenuView"),
            var key when key.StartsWith("layout-option:", StringComparison.Ordinal) => _window.FindControl<WorkspaceLayoutGallery>("LayoutGallery")?
                .GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Tag as string == key["layout-option:".Length..]),
            _ => null
        };
    }

    void WorkbenchGuideViewModel.IStage.SetSpotlight(string[] anchors)
    {
        _focusTargets.Clear();
        foreach (var anchor in anchors)
            if (Resolve(anchor) is { } control) _focusTargets.Add(control);
        Dispatcher.UIThread.Post(PositionCard, DispatcherPriority.Render);
    }

    void WorkbenchGuideViewModel.IStage.ScrollAnchor(string anchor, bool top)
    {
        if (Resolve(anchor) is not { } view) return;
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroll is null) return;
        scroll.Offset = top ? default : new Vector(scroll.Offset.X, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
    }

    /// <summary>轮询预览里的 Mermaid 图表块，出图（Image.Source 就绪）才放行；5 秒超时降级，保证引导不被卡死。</summary>
    void WorkbenchGuideViewModel.IStage.WaitForDiagrams(Action done)
    {
        var preview = _window?.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var token = _vm?.RunToken ?? 0;
        var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        poll.Tick += (_, _) =>
        {
            var ready = preview is not null && preview.GetVisualDescendants()
                .OfType<Border>().Any(border => border.Classes.Contains("MdMermaid")
                    && border.Child is Image { Source: not null });
            if (ready || DateTime.UtcNow > deadline || token != _vm?.RunToken)
            {
                poll.Stop();
                if (token == _vm?.RunToken) done();
            }
        };
        poll.Start();
    }

    void WorkbenchGuideViewModel.IStage.FitMap(double scale) =>
        _window?.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")?
            .FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")?.Fit(scale);

    void WorkbenchGuideViewModel.IStage.SelectOutlineItem(string title)
    {
        var outline = _window?.FindControl<Control>("OutlineGuideTarget")?
            .GetVisualDescendants().OfType<ShellOutlineView>().FirstOrDefault()?.DataContext as ShellOutlineViewModel;
        if (outline is null) return;
        var item = outline.OutlineItems.FirstOrDefault(candidate => candidate.Title == title);
        if (item is not null) outline.SelectedOutlineItem = item;
    }

    void WorkbenchGuideViewModel.IStage.OpenFileMenu()
    {
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is { } file) file.IsSubMenuOpen = true;
    }

    void WorkbenchGuideViewModel.IStage.OpenExportSubmenu()
    {
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FindControl<MenuItem>("ExportMenuItem") is { } export) export.IsSubMenuOpen = true;
    }

    void WorkbenchGuideViewModel.IStage.CloseMenus()
    {
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView") is not { } menu) return;
        if (menu.FileMenuTarget is { } file) file.IsSubMenuOpen = false;
        if (menu.FindControl<MenuItem>("ToolsMenuItem") is { } tools) tools.IsSubMenuOpen = false;
    }

    private bool Hit(string anchor, Visual source)
    {
        if (Resolve(anchor) is not { } target) return false;
        var path = source.GetVisualAncestors().Prepend(source);
        return ReferenceEquals(target, source) || path.Contains(target);
    }

    public bool AllowsPointer(Visual source)
    {
        if (!IsVisible) return true;
        var path = source.GetVisualAncestors().Prepend(source).ToArray();
        if (path.Contains(_card) || path.Contains(_bubbleHost)) return true;
        if (!_vm.GateActive) return false;
        return _vm.GateAnchorKeys.Any(anchor => Hit(anchor, source));
    }

    /// <summary>门控纠正：视图判定点击落在两级门范围内但不是目标时转发给编排。</summary>
    public void PointerRejected(Visual source)
    {
        if (_vm.GateScopeKey is { } scope && Hit(scope, source)) _vm.RejectPointer();
    }

    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsVisible || _window is null) return false;
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            var controls = _window.GetVisualDescendants().OfType<Control>()
                .Where(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled && AllowsPointer(control)).ToArray();
            var current = Array.IndexOf(controls, _window.FocusManager?.GetFocusedElement() as Control);
            if (controls.Length > 0) controls[(current + (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? controls.Length - 1 : 1) + controls.Length) % controls.Length].Focus(NavigationMethod.Tab);
            return true;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.O or Key.N or Key.W or Key.P or Key.F or Key.H or Key.S) return true;
        if (e.Key is Key.F5 or Key.F8 or Key.F11 || e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Enter) return true;
        var input = e.Source as Visual;
        var editing = input is TextBox or AvaloniaEdit.Editing.TextArea || input?.GetVisualAncestors().Any(view => view is TextBox or AvaloniaEdit.Editing.TextArea) == true;
        if (e.Key == Key.Escape && !editing) { Finish(); return true; }
        if (editing || e.KeyModifiers != KeyModifiers.None) return false;
        if (e.Key == Key.Right) { _vm.Advance(); return true; }
        if (e.Key == Key.Left && _vm.CanGoPrevious) { _vm.StartAct(_vm.StepIndex - 1); return true; }
        return false;
    }

    public void Finish()
    {
        if (!IsVisible || _shellVm is null) return;
        _vm.Stop();
        IsVisible = false;
        CloseMenus();
        _shellVm.Appearance.PropertyChanged -= AppearanceChanged;
        _shellVm.EndGuideDocument();
        _sourceVm?.RestoreDocument(_sourceDocument);
        _shellVm.Appearance.SelectedTypography = _savedTypography;
        if (_liveState is { } live && _shellVm.Workbench.LiveEditor is { } editor) editor.RestoreState(live);
        _shellVm.Workbench.RestorePreferences(_savedSettings, _savedView, _savedEdit);
        _shellVm.Workbench.ShowMapSettings = _mapSettings; _shellVm.Workbench.ShowTypography = _typography;
        if (_savedPins is { } pins)
        {
            var preferences = _shellVm.Workbench.Preferences;
            preferences.PinEditorToolbar = pins.Editor; preferences.PinReadingToolbar = pins.Reading; preferences.PinMapToolbar = pins.Map;
        }
        _shellVm.Workbench.ShowLayouts = _savedLayouts;
        _shellVm.Navigation.SelectedSideTabIndex = _sidebarTab;
        if (_history is { } history) _shellVm.Workbench.History.Restore(history);
        if (_floatingSession is { } floating) _window?.WorkspaceViews.RestoreFloating(floating);
        _floatingSession = null;
        _shellVm.Layout.IsOutlineVisible = _outline; _shellVm.Layout.IsFocusMode = _focus; _shellVm.Layout.IsSidebarVisible = _sidebar; _shellVm.Layout.IsStatusBarVisible = _status;
        if (_savedSettings is { } saved) _settings?.Update(_ => saved with { HasSeenOnboardingGuide = true });
        if (_window?.FindControl<Button>("QuickStartButton") is { } quickStart) quickStart.IsVisible = false;
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
    }

    private void PositionCard()
    {
        if (!IsVisible || _window is null) return;
        var highlights = new List<Rect>();
        foreach (var target in _focusTargets) AddTarget(target, highlights);
        if (_vm.Phase == WorkbenchGuideViewModel.GuidePhase.Intro) highlights.Clear();
        var rect = highlights.Count > 0 ? highlights[0] : new Rect(Bounds.Width / 2 - 60, Bounds.Height / 2 - 16, 120, 32);
        _mask.Targets = highlights;
        _mask.Accent = FindBrush("VexAccentBrush", "#7457E8");
        RefreshColors();
        _card.Measure(new Size(_card.Width, double.PositiveInfinity));
        var width = _card.Width;
        var height = Math.Min(_card.DesiredSize.Height, Math.Max(100, Bounds.Height - 40));
        if (_bubbleHost.IsVisible)
        {
            _bubbleHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var bubbleSize = _bubbleHost.DesiredSize;
            var x = Math.Clamp(rect.X + rect.Width / 2 - bubbleSize.Width / 2, 12, Math.Max(12, Bounds.Width - bubbleSize.Width - 12));
            var y = rect.Y - bubbleSize.Height - 12;
            if (y < 12) y = rect.Bottom + 12;
            y = Math.Clamp(y, 12, Math.Max(12, Bounds.Height - bubbleSize.Height - 12));
            _bubbleHost.HorizontalAlignment = HorizontalAlignment.Left; _bubbleHost.VerticalAlignment = VerticalAlignment.Top;
            _bubbleHost.Margin = new Thickness(x, y, 0, 0);
            return;
        }
        _card.MaxHeight = Math.Max(100, Bounds.Height - 40);
        _card.HorizontalAlignment = HorizontalAlignment.Left; _card.VerticalAlignment = VerticalAlignment.Top;
        Rect placement;
        if (_vm.Phase == WorkbenchGuideViewModel.GuidePhase.Intro || highlights.Count == 0)
        {
            placement = new Rect(new Point(Math.Max(18, (Bounds.Width - width) / 2), Math.Max(18, (Bounds.Height - height) / 2)), new Size(width, height));
        }
        else
        {
            var candidates = new[]
            {
                new Point(24, Bounds.Height - height - 24),
                new Point(Bounds.Width - width - 24, Bounds.Height - height - 24),
                new Point(rect.Right + 20, rect.Y),
                new Point(rect.Left - width - 20, rect.Y)
            }.Select(point => new Rect(
                Math.Clamp(point.X, 20, Math.Max(20, Bounds.Width - width - 20)),
                Math.Clamp(point.Y, 20, Math.Max(20, Bounds.Height - height - 20)), width, height));
            placement = candidates
                .OrderBy(candidate => highlights.Sum(hole => { var overlap = candidate.Intersect(hole); return overlap.Width * overlap.Height; }))
                .First();
        }
        _card.Margin = new Thickness(placement.X, placement.Y, 0, 0);
        _mask.Card = placement;

        void AddTarget(Control control, List<Rect> list)
        {
            var point = control.TranslatePoint(default, this);
            if (point is not { } origin)
            {
                // 弹出层（如菜单子项）与引导不在同一棵可视树，退回到最近的可视宿主。
                var fallback = control.GetVisualAncestors().OfType<Control>().FirstOrDefault(item => item.TranslatePoint(default, this) is not null)
                    ?? _focusTargets.FirstOrDefault(item => item != control && item.TranslatePoint(default, this) is not null);
                if (fallback is null) return;
                origin = fallback.TranslatePoint(default, this)!.Value;
            }
            var area = new Rect(origin, control.Bounds.Size).Inflate(4).Intersect(new Rect(Bounds.Size));
            if (area.Width > 0 && area.Height > 0) list.Add(area);
        }
    }

    private void CloseMenus()
    {
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView") is not { } menu) return;
        if (menu.FileMenuTarget is { } file) file.IsSubMenuOpen = false;
        if (menu.FindControl<MenuItem>("ToolsMenuItem") is { } tools) tools.IsSubMenuOpen = false;
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
