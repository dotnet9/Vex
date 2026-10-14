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
    private readonly TextBlock _visual = new() { FontSize = 30, TextAlignment = TextAlignment.Center, FontWeight = FontWeight.SemiBold };
    private readonly Button _next = new() { Content = "下一步 →", HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _previous = new() { Content = "上一步" };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 5, Height = 3 };
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

    public WorkbenchGuideView()
    {
        IsVisible = false;
        var stack = new StackPanel { Spacing = 16 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_counter);
        var skip = new Button { Content = "跳过", FontSize = 11, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), MinHeight = 0 };
        Grid.SetColumn(skip, 1); header.Children.Add(skip); skip.Click += (_, _) => Finish();
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
        _card.Child = stack;
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
        _sourceCaret = sourceView.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.Editor.CaretOffset;
        _sourceDocument = _sourceVm?.BeginTemporaryDocument();
        _mapState = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!
            .FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!.CaptureViewport();
        vm.BeginGuideDocument(Demo);
        vm.Workbench.SummaryLength = 30; vm.Workbench.ShowParagraphs = true; vm.Workbench.ShowLists = true;
        vm.Workbench.ShowCode = false; vm.Workbench.ShowTables = false;
        vm.Layout.IsFocusMode = false; vm.Layout.IsSidebarVisible = true; vm.Layout.IsStatusBarVisible = true;
        _changedTheme = _changedSummary = false;
        vm.Appearance.PropertyChanged += AppearanceChanged;
        vm.Workbench.PropertyChanged += WorkbenchChanged;
        vm.PropertyChanged += DocumentChanged;
        IsVisible = true;
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
        for (var i = 0; i < _steps.Length; i++) _steps[i].Opacity = i == StepIndex ? 1 : .5;
        _counter.Text = $"快速上手   {StepIndex + 1} / 5"; _progress.Value = StepIndex + 1;
        _previous.IsVisible = StepIndex > 0; _next.Content = StepIndex == 4 ? "开始创作 ✓" : "下一步 →";
        _vm.Workbench.ShowTypography = false; _vm.Workbench.ShowMapSettings = false;
        switch (StepIndex)
        {
            case 0:
                _vm.Workbench.ShowInstant();
                _title.Text = "一篇文档，三个视角"; _visual.Text = "编辑  ⇄  阅读  ⇄  导图"; _visual.FontSize = 16;
                _description.Text = "点击上方的视图切换：写作、检查排版、梳理结构共用同一份 Markdown。";
                break;
            case 1:
                _vm.Workbench.ShowInstant();
                _title.Text = "输入就能看到效果"; _visual.Text = "**重点** → 重点"; _visual.FontSize = 22;
                _description.Text = "点击段落进入编辑，试着加上 **重点**。输入时即时渲染；Ctrl+Enter 完成当前块，Esc 撤销本块改动。源码对照适合精细调整。";
                break;
            case 2:
                _vm.Workbench.ShowRead(); _vm.Workbench.ShowTypography = true;
                _title.Text = "给文字选一套排版"; _visual.Text = "Aa  标题 · 正文 · 引用"; _visual.FontSize = 21;
                _description.Text = "点击任意主题预览真实文档。Markdown 排版与界面主题分别设置，编辑、阅读、导出采用相同排版。";
                break;
            case 3:
                _vm.Workbench.ShowMapSource = false; _vm.Workbench.ShowMindMap(); _vm.Workbench.ShowMapSettings = true;
                _title.Text = "用导图整理结构"; _visual.Text = "标题 ─ 分支 ─ 摘要"; _visual.FontSize = 19;
                _description.Text = "把摘要字数调小，点击段落节点查看完整正文。双击节点定位源码；分支旁按钮用于折叠，拖动画布平移，Ctrl+滚轮缩放。";
                break;
            case 4:
                _vm.Workbench.ShowInstant();
                if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem menu) menu.IsSubMenuOpen = true;
                _title.Text = "保存与分享"; _visual.Text = "Markdown  →  HTML / PDF"; _visual.FontSize = 17;
                _description.Text = "在“文件”菜单保存 Markdown，或导出 HTML、PDF、PNG、Word。Ctrl+S 保存，Ctrl+P 快速打开，F8 专注。演示结束会恢复你原来的文档。";
                break;
        }
        UpdateFeedback();
        Dispatcher.UIThread.Post(PositionCard, DispatcherPriority.Render);
    }
    private void UpdateFeedback()
    {
        if (_vm is null) return;
        _feedback.Text = StepIndex switch
        {
            0 => _vm.Workbench.IsMindMap ? "✓ 你已切换到导图" : _vm.Workbench.IsReading ? "✓ 你已切换到阅读" : "试试点击“阅读”或“导图”",
            1 => _vm.Markdown != Demo ? "✓ 实际内容已经更新" : "直接在演示段落里输入即可",
            2 => _changedTheme ? "✓ 已应用新的排版主题" : "从右侧卡片选择一套主题",
            3 => _changedSummary ? $"✓ 当前摘要最多 {_vm.Workbench.SummaryLength} 字" : "默认 30 字；完整正文始终保留",
            _ => "此处是临时演示文档，保存和导出由你主动触发"
        };
    }
    private void PositionCard()
    {
        if (!IsVisible || _targets.Length < 5) return;
        var target = _targets[StepIndex];
        var origin = target.TranslatePoint(new Point(0, 0), this);
        var rect = origin is { } point ? new Rect(point, target.Bounds.Size) : new Rect(20, 20, 250, 40);
        if (StepIndex == 1) rect = new Rect(rect.X + 28, rect.Y + 70, Math.Min(rect.Width - 56, 560), 220);
        rect = rect.Intersect(new Rect(Bounds.Size)).Inflate(6);
        _mask.Target = rect;
        _mask.Accent = FindBrush("VexAccentBrush", "#7457E8");
        _card.Background = FindBrush("VexPanelBackgroundBrush", "#FFFFFF");
        _card.BorderBrush = FindBrush("VexPanelBorderBrush", "#DFE1EB");
        _title.Foreground = FindBrush("VexEditorForegroundBrush", "#333344");
        _description.Foreground = _title.Foreground;
        _counter.Foreground = _feedback.Foreground = FindBrush("VexMutedBrush", "#77778A");
        _card.HorizontalAlignment = rect.Center.X > Bounds.Width / 2 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _card.VerticalAlignment = StepIndex == 1 || StepIndex == 4 ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        _card.Margin = new Thickness(24);
    }
    private IBrush FindBrush(string key, string fallback) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brush.Parse(fallback);
    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsVisible) return false;
        if (e.Key == Key.Escape && e.Source is not TextBox) { Finish(); return true; }
        if (e.Source is TextBox || e.KeyModifiers != KeyModifiers.None) return false;
        if (e.Key == Key.Right) { NextStep(); return true; }
        if (e.Key == Key.Left) { ShowStep(StepIndex - 1); return true; }
        return false;
    }
    public void Finish()
    {
        if (!IsVisible || _vm is null) return;
        IsVisible = false; _animation?.Stop(); _animation = null;
        if (_window?.FindControl<ShellTitleMenuView>("TitleMenuView")?.FileMenuTarget is MenuItem menu) menu.IsSubMenuOpen = false;
        _vm.Appearance.PropertyChanged -= AppearanceChanged;
        _vm.Workbench.PropertyChanged -= WorkbenchChanged;
        _vm.PropertyChanged -= DocumentChanged;
        _vm.EndGuideDocument();
        _sourceVm?.RestoreDocument(_sourceDocument);
        if (_liveState is { } live && _vm.Workbench.LiveEditor is { } editor) editor.RestoreState(live);
        _vm.Appearance.SelectedTypography = _savedTypography;
        _vm.Workbench.RestorePreferences(_savedSettings, _savedView, _savedEdit);
        _vm.Workbench.ShowMapSource = _source; _vm.Workbench.ShowMapSettings = _mapSettings; _vm.Workbench.ShowTypography = _typography;
        _vm.Layout.IsFocusMode = _focus; _vm.Layout.IsSidebarVisible = _sidebar; _vm.Layout.IsStatusBarVisible = _status;
        if (_savedSettings is { } saved) _settings?.Update(_ => saved with { HasSeenOnboardingGuide = true });
        if (_window is not null) _window.FindControl<ShellWorkbenchToolbarView>("WorkbenchToolbar")!.QuickStartButton.IsVisible = false;
        var window = _window;
        var mapState = _mapState;
        var focus = _savedFocus;
        var caret = _sourceCaret;
        Dispatcher.UIThread.Post(() =>
        {
            if (window is null) return;
            window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!
                .FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.Editor.CaretOffset = caret;
            if (mapState is { } state) window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!
                .FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!.RestoreViewport(state, onNextRoot: true);
            focus?.Focus();
        }, DispatcherPriority.Background);
        _vm = null;
    }

    private sealed class Spotlight : Control
    {
        private Rect _target;
        public Rect Target { get => _target; set { _target = value; InvalidateVisual(); } }
        public IBrush Accent { get; set; } = Brushes.MediumPurple;
        public double Glow { get; set; } = .5;
        public override void Render(DrawingContext context)
        {
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry(new Rect(Bounds.Size)));
            geometry.Children.Add(new RectangleGeometry(Target, 12, 12));
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#24243E"), .52), null, geometry);
            using (context.PushOpacity(Glow)) context.DrawRectangle(null, new Pen(Accent, 9), Target.Inflate(2), 14, 14);
            context.DrawRectangle(null, new Pen(Accent, 2), Target, 12, 12);
        }
    }
}
