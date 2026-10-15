using Avalonia;
using Avalonia.Animation;
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

/// <summary>在独立演示文档中完成五项真实操作；结束后恢复未保存文档与显示偏好。</summary>
public sealed class WorkbenchGuideView : Panel
{
    private const string Demo = "# 从文字到结构\n\n在这里输入一句话，观察 Markdown 即时渲染。\n\n## 把想法整理清楚\n\n这是一段保留完整正文的演示内容。导图默认最多显示三十个字，你可以调整摘要长度，再点击节点查看全文。\n\n- 写下想法\n  - 补充细节\n- 梳理结构\n- 分享成果\n\n## 下一步\n\n**重点**、[链接](https://codewf.com)与引用，都可以在编辑模式中调整。\n\n> 保存 Markdown，或者导出带排版的文档。";
    private readonly Spotlight _mask = new() { IsHitTestVisible = false };
    private readonly Border _card = new() { Width = 330, Padding = new Thickness(24), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 16 60 0 #40000000") };
    private readonly TextBlock _counter = new() { FontSize = 11 };
    private readonly TextBlock _title = new() { FontSize = 22, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
    private readonly TextBlock _feedback = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _visual = new() { FontSize = 30, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontWeight = FontWeight.SemiBold };
    private readonly Button _skip = new() { FontSize = 11, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), MinHeight = 0 };
    private readonly Button _next = new() { Content = L(VexL.GuideNext), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _previous = new() { Content = L(VexL.GuidePrevious) };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 5, Height = 3 };
    private readonly List<(ScrollViewer View, Vector Offset)> _scrollPositions = [];
    private (int Start, int Length) _sourceSelection;
    public IReadOnlyList<Rect> SpotlightTargets => _mask.Targets;
    private readonly Button[] _steps = new Button[5];
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
    private bool _source;
    private bool _mapSettings;
    private bool _typography;
    private DispatcherTimer? _animation;
    private bool _changedTheme;
    private bool _changedSummary;
    private CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownLiveEditorState? _liveState;
    private AvaloniaEdit.Document.TextDocument? _sourceDocument;
    private int _sourceCaret;
    private CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.ViewportState? _mapState;
    private IInputElement? _savedFocus;
    private Vex.Modules.Workspace.ViewModels.MarkdownEditorViewModel? _sourceVm;
    public int StepIndex { get; private set; }

    private static string L(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(key) ?? string.Empty;

    public WorkbenchGuideView()
    {
        IsVisible = false;
        var stack = new StackPanel { Spacing = 16 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_counter);
        Grid.SetColumn(_skip, 1); header.Children.Add(_skip); _skip.Click += (_, _) => Finish();
        stack.Children.Add(header);
        stack.Children.Add(new Border { Child = _visual, Padding = new Thickness(18), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.Parse("#7457E8"), .12) });
        stack.Children.Add(_title); stack.Children.Add(_description); stack.Children.Add(_feedback); stack.Children.Add(_progress);
        var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            var button = new Button { Content = (i + 1).ToString(), Width = 28, Height = 28, MinHeight = 0, MinWidth = 0,
                Padding = new Thickness(0), CornerRadius = new CornerRadius(14), FontSize = 11 };
            button.Click += (_, _) => ShowStep(index);
            _steps[i] = button; steps.Children.Add(button);
        }
        stack.Children.Add(steps);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        actions.Children.Add(_previous); Grid.SetColumn(_next, 1); actions.Children.Add(_next); stack.Children.Add(actions);
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
        _savedMarkdown = vm.Markdown; _savedView = vm.Workbench.View; _savedEdit = vm.Workbench.EditMode;
        _savedTypography = vm.Appearance.SelectedTypography;
        _sidebar = vm.Layout.IsSidebarVisible; _status = vm.Layout.IsStatusBarVisible; _focus = vm.Layout.IsFocusMode;
        _source = vm.Workbench.ShowMapSource; _mapSettings = vm.Workbench.ShowMapSettings; _typography = vm.Workbench.ShowTypography;
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
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Vex.Core.Messaging.MarkdownTextChangedCommand(Demo, 1, 1, Demo.Count(c => c == '\n') + 1));
        vm.Workbench.SummaryLength = 30; vm.Workbench.ShowParagraphs = true; vm.Workbench.ShowLists = true;
        vm.Workbench.ShowCode = false; vm.Workbench.ShowTables = false;
        vm.Layout.IsFocusMode = false; vm.Layout.IsSidebarVisible = true; vm.Layout.IsStatusBarVisible = true;
        _changedTheme = _changedSummary = false;
        vm.Appearance.PropertyChanged += AppearanceChanged;
        vm.Workbench.PropertyChanged += WorkbenchChanged;
        vm.PropertyChanged += DocumentChanged;
        vm.Layout.PropertyChanged += LayoutChanged;
        IsVisible = true;
        _card.Transitions = null; _visual.Opacity = 1; _mask.Glow = 0;
        if (vm.Layout.EnableMotion)
        {
            _card.Transitions = new Transitions { new ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromMilliseconds(260) }, new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(180) } };
            _animation = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            var phase = 0d;
            _animation.Tick += (_, _) => { phase += .1; _visual.Opacity = .78 + Math.Sin(phase) * .2; _mask.Glow = .4 + Math.Sin(phase) * .2; _mask.InvalidateVisual(); };
            _animation.Start();
        }
        ShowStep(0);
    }
    private void LayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellWindowLayoutViewModel.EnableMotion) && _vm?.Layout.EnableMotion == false)
        { _animation?.Stop(); _card.Transitions = null; _visual.Opacity = 1; _mask.Glow = 0; _mask.InvalidateVisual(); }
    }
    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellAppearanceViewModel.SelectedTypography)) { _changedTheme = true; UpdateFeedback(); }
    }
    private void WorkbenchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.SummaryLength)) _changedSummary = true;
        UpdateFeedback();
    }
    private void DocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Markdown)) UpdateFeedback();
    }
    public void NextStep() { if (StepIndex >= 4) Finish(); else ShowStep(StepIndex + 1); }
    public void ShowStep(int index)
    {
        if (_vm is null || !IsVisible) return;
        StepIndex = Math.Clamp(index, 0, 4);
        _skip.Content = L(VexL.GuideSkip); _previous.Content = L(VexL.GuidePrevious);
        for (var i = 0; i < _steps.Length; i++) _steps[i].Opacity = i == StepIndex ? 1 : .5;
        _counter.Text = string.Format(L(VexL.GuideStepFormat), StepIndex + 1); _progress.Value = StepIndex + 1;
        _previous.IsVisible = StepIndex > 0; _next.Content = StepIndex == 4 ? L(VexL.GuideStart) : L(VexL.GuideNext);
        _vm.Workbench.ShowTypography = false; _vm.Workbench.ShowMapSettings = false;
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem activeMenu) activeMenu.IsSubMenuOpen = false;
        switch (StepIndex)
        {
            case 0:
                _vm.Workbench.ShowInstant();
                _title.Text = L(VexL.GuideTitle0); _visual.Text = L(VexL.GuideVisual0); _visual.FontSize = 16;
                _description.Text = L(VexL.GuideDescription0);
                break;
            case 1:
                _vm.Workbench.ShowInstant();
                _title.Text = L(VexL.GuideTitle1); _visual.Text = L(VexL.GuideVisual1); _visual.FontSize = 22;
                _description.Text = L(VexL.GuideDescription1);
                break;
            case 2:
                _vm.Workbench.ShowRead(); _vm.Workbench.ShowTypography = true;
                _title.Text = L(VexL.GuideTitle2); _visual.Text = L(VexL.GuideVisual2); _visual.FontSize = 21;
                _description.Text = L(VexL.GuideDescription2);
                break;
            case 3:
                _vm.Workbench.ShowMapSource = false; _vm.Workbench.ShowMindMap(); _vm.Workbench.ShowMapSettings = true;
                _title.Text = L(VexL.GuideTitle3); _visual.Text = L(VexL.GuideVisual3); _visual.FontSize = 19;
                _description.Text = L(VexL.GuideDescription3);
                break;
            case 4:
                _vm.Workbench.ShowInstant();
                if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem menu) menu.IsSubMenuOpen = true;
                _title.Text = L(VexL.GuideTitle4); _visual.Text = L(VexL.GuideVisual4); _visual.FontSize = 17;
                _description.Text = L(VexL.GuideDescription4);
                break;
        }
        UpdateFeedback();
        Dispatcher.UIThread.Post(() =>
        {
            if (StepIndex <= 2 && IsVisible && _window is not null)
            {
                var name = StepIndex == 2 ? "PreviewGuideTarget" : "InstantGuideTarget";
                if (_window.FindControl<Control>(name) is { } content)
                    foreach (var scroll in content.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
                _window.UpdateLayout();
            }
            PositionCard();
        }, DispatcherPriority.Render);
    }
    private void UpdateFeedback()
    {
        if (_vm is null) return;
        _feedback.Text = StepIndex switch
        {
            0 => _vm.Workbench.IsMindMap ? L(VexL.GuideFeedbackMap) : _vm.Workbench.IsReading ? L(VexL.GuideFeedbackRead) : L(VexL.GuideFeedbackView),
            1 => _vm.Markdown != Demo ? L(VexL.GuideFeedbackEdited) : L(VexL.GuideFeedbackType),
            2 => _changedTheme ? L(VexL.GuideFeedbackTheme) : L(VexL.GuideFeedbackChooseTheme),
            3 => _changedSummary ? string.Format(L(VexL.GuideFeedbackSummary), _vm.Workbench.SummaryLength) : L(VexL.GuideFeedbackSummaryDefault),
            _ => L(VexL.GuideFeedbackTemporary)
        };
    }
    private void PositionCard()
    {
        if (!IsVisible || _targets.Length < 5) return;
        var target = _targets[StepIndex];
        var origin = target.TranslatePoint(new Point(0, 0), this);
        var rect = origin is { } point ? new Rect(point, target.Bounds.Size) : new Rect(20, 20, 250, 40);
        if (StepIndex == 1 && _vm?.Workbench.LiveEditor?.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>().FirstOrDefault(v => v.Markdown?.Contains("在这里输入一句话", StringComparison.Ordinal) == true) is { } paragraph && paragraph.TranslatePoint(default, this) is { } paragraphPoint)
            rect = new Rect(paragraphPoint, paragraph.Bounds.Size).Inflate(new Thickness(12, 16));
        rect = rect.Intersect(new Rect(Bounds.Size)).Inflate(6);
        var highlights = new List<Rect> { rect };
        if (StepIndex is 1 or 3 && _window?.FindControl<ShellEditorOptionsView>("EditorOptions") is { IsEffectivelyVisible: true } options && options.TranslatePoint(default, this) is { } optionPoint)
            highlights.Add(new Rect(optionPoint, options.Bounds.Size).Inflate(4));
        if (StepIndex == 3 && _window?.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget") is { } map && map.TranslatePoint(default, this) is { } mapPoint)
        {
            var rootCard = map.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => ToolTip.GetTip(b)?.ToString() == _vm?.Workbench.Root?.FullText);
            if (rootCard?.TranslatePoint(default, this) is { } rootPoint && rootCard.TranslatePoint(new Point(rootCard.Bounds.Width, rootCard.Bounds.Height), this) is { } rootEnd)
                highlights.Add(new Rect(rootPoint, rootEnd).Inflate(8));
        }
        _mask.Targets = highlights;
        _mask.Accent = FindBrush("VexAccentBrush", "#7457E8");
        _card.Background = FindBrush("VexPanelBackgroundBrush", "#FFFFFF");
        _card.BorderBrush = FindBrush("VexPanelBorderBrush", "#DFE1EB");
        _title.Foreground = FindBrush("VexEditorForegroundBrush", "#333344");
        _description.Foreground = _title.Foreground;
        _counter.Foreground = _feedback.Foreground = FindBrush("VexMutedBrush", "#77778A");
        _card.MaxHeight = Math.Max(100, Bounds.Height - 40);
        _card.Measure(new Size(_card.Width, _card.MaxHeight));
        var width = _card.Width; var height = Math.Min(_card.DesiredSize.Height, Bounds.Height - 40);
        var candidates = new[] { new Point(rect.Right + 20, rect.Y), new Point(rect.Left - width - 20, rect.Y), new Point(rect.Center.X - width / 2, rect.Bottom + 20), new Point(rect.Center.X - width / 2, rect.Top - height - 20), new Point(24, Bounds.Height - height - 24), new Point(Bounds.Width - width - 24, Bounds.Height - height - 24) }
            .Select(p => new Rect(Math.Clamp(p.X, 20, Math.Max(20, Bounds.Width - width - 20)), Math.Clamp(p.Y, 20, Math.Max(20, Bounds.Height - height - 20)), width, height));
        var placement = candidates.OrderBy(candidate => highlights.Sum(h => { var overlap = candidate.Intersect(h); return overlap.Width * overlap.Height; })).ThenBy(candidate => Math.Abs(candidate.Center.X - rect.Center.X) + Math.Abs(candidate.Center.Y - rect.Center.Y)).First();
        _card.HorizontalAlignment = HorizontalAlignment.Left; _card.VerticalAlignment = VerticalAlignment.Top;
        _card.Margin = new Thickness(placement.X, placement.Y, 0, 0);
        _mask.Card = placement;
    }
    private IBrush FindBrush(string key, string fallback) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brush.Parse(fallback);
    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsVisible) return false;
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
        IsVisible = false; _animation?.Stop(); _animation = null;
        _card.Transitions = null; _visual.Opacity = 1; _mask.Glow = 0;
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem menu) menu.IsSubMenuOpen = false;
        _vm.Appearance.PropertyChanged -= AppearanceChanged;
        _vm.Workbench.PropertyChanged -= WorkbenchChanged;
        _vm.PropertyChanged -= DocumentChanged;
        _vm.Layout.PropertyChanged -= LayoutChanged;
        _vm.EndGuideDocument();
        _sourceVm?.RestoreDocument(_sourceDocument);
        _vm.Appearance.SelectedTypography = _savedTypography;
        if (_liveState is { } live && _vm.Workbench.LiveEditor is { } editor) editor.RestoreState(live);
        _vm.Workbench.RestorePreferences(_savedSettings, _savedView, _savedEdit);
        _vm.Workbench.ShowMapSource = _source; _vm.Workbench.ShowMapSettings = _mapSettings; _vm.Workbench.ShowTypography = _typography;
        _vm.Layout.IsFocusMode = _focus; _vm.Layout.IsSidebarVisible = _sidebar; _vm.Layout.IsStatusBarVisible = _status;
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
        public double Glow { get; set; } = .18;
        public override void Render(DrawingContext context)
        {
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry(new Rect(Bounds.Size)));
            foreach (var target in Targets) geometry.Children.Add(new RectangleGeometry(target, 12, 12));
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#24243E"), .52), null, geometry);
            foreach (var target in Targets)
            {
                using (context.PushOpacity(Glow)) context.DrawRectangle(null, new Pen(Accent, 9), target.Inflate(2), 14, 14);
                context.DrawRectangle(null, new Pen(Accent, 2), target, 12, 12);
            }
            if (Targets.Count > 0)
            {
                var target = Targets[0];
                var start = new Point(Math.Clamp(Card.Center.X, target.Left, target.Right), Math.Clamp(Card.Center.Y, target.Top, target.Bottom));
                var end = new Point(Math.Clamp(start.X, Card.Left, Card.Right), Math.Clamp(start.Y, Card.Top, Card.Bottom));
                context.DrawLine(new Pen { Brush = Accent, Thickness = 1.5, DashStyle = new DashStyle(new double[] { 4, 4 }, 0) }, start, end);
                context.DrawEllipse(Accent, null, start, 3, 3);
            }
        }
    }
}
