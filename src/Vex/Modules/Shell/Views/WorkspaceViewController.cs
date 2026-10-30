using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Controls.Controls;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>每种视图只创建一次；独立窗口迁移同一控件，共享主窗口的文档和命令。</summary>
public sealed class WorkspaceViewController
{
    private readonly MainWindow _window;
    private readonly Grid _surface;
    private readonly Dictionary<string, Border> _frames = [];
    private readonly Dictionary<string, Border> _headers = [];
    private readonly Dictionary<string, CodeWFWindow> _floating = [];
    private readonly StackPanel _shelf;
    private MainWindowViewModel? _vm;
    private Vex.Core.Services.EditorPreferences? _pinPreferences;
    private bool _arranging;
    private bool _closing;
    private StackPanel? _renderCaption;
    private TextBlock? _renderLabel;
    private TextBlock? _renderTypographyPrefix;
    private TextBlock? _renderTypographyName;
    private Button? _renderTypographyTrigger;
    private Grid? _editorLayout;
    private Control? _editorOptions;

    /// <summary>原型 #edit-options 是编辑块的第一个子级：钉住=占位行，取消钉住=悬浮在正文顶端。</summary>
    private void ApplyEditorPin()
    {
        if (_editorOptions is null || _editorLayout is null) return;
        var pinned = _pinPreferences?.PinEditorToolbar ?? true;
        Grid.SetRow(_editorOptions, pinned ? 0 : 1);
        _editorOptions.VerticalAlignment = pinned ? VerticalAlignment.Stretch : VerticalAlignment.Top;
    }

    /// <summary>阅读窗格头（控制器自建），承载「钉」按钮；钉住状态由偏好驱动。</summary>
    public Border ReadingHeader => _headers["render"];
    public ToggleButton? ReadingPin { get; private set; }

    /// <summary>阅读窗格头居中的排版入口。主窗体拿它豁免「点外面即关」，第二次点击才能回到开合语义。</summary>
    public Control? RenderTypographyTrigger => _renderTypographyTrigger;

    /// <summary>钉住 = 窗格头占位行；取消钉住 = 悬浮于阅读区顶端，静默至悬停或聚焦。</summary>
    private void ApplyReadingPin()
    {
        if (ReadingPin is null) return;
        var pinned = _pinPreferences?.PinReadingToolbar ?? true;
        var header = ReadingHeader;
        Grid.SetRow(header, pinned ? 0 : 1);
        header.VerticalAlignment = pinned ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        header.Classes.Set("floating", !pinned);
    }

    private void OnPinPreferenceChanged(object? sender, EventArgs e) { ApplyReadingPin(); ApplyEditorPin(); }

    private static Panel BuildPinIcon(double size)
    {
        static Viewbox State(string className)
        {
            var canvas = new Canvas { Width = 24, Height = 24 };
            canvas.Children.Add(new Avalonia.Controls.Shapes.Path { Classes = { "pin-head" }, Data = Geometry.Parse("M8 3h8l-1 8H9l-1-8z") });
            canvas.Children.Add(new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M12 11v9") });
            if (className == "pin-off") canvas.RenderTransform = new RotateTransform(45, 12, 11);
            return new Viewbox { Classes = { className }, Child = canvas };
        }
        return new Panel { Width = size, Height = size, Children = { State("pin-on"), State("pin-off") } };
    }
    public sealed record FloatingSession(IReadOnlyDictionary<string, CodeWFWindow> Windows);
    private FloatingSession? _presentationSession;
    private (CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.ViewportState Viewport, object? Root)? _presentationMapState;
    private readonly TextBlock _empty = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    // 原型 .view-split：分栏把手本体即 4px 间隙，块由间隙两侧各自留出边框。
    private readonly GridSplitter _horizontalSplit = new() { Classes = { "workspace-gap", "gap-horizontal" }, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
    private readonly GridSplitter _verticalSplit = new() { Classes = { "workspace-gap", "gap-vertical" }, ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
    private static GridLength Gap => new(ShellWindowLayoutViewModel.BlockGap);
    private static GridLength NoGap => new(0);
    public IReadOnlyDictionary<string, CodeWFWindow> FloatingWindows => _floating;
    // 原型 makeHandle() 的 key = 「视图预设 : 轴向」：同一预设下左右与上下各存一份比例。
    private string SplitKey(bool vertical) => (_vm?.Workbench.WorkspaceLayout ?? "edit") + (vertical ? ":v" : ":h");
    private static GridLength Star(double value) => new(value, GridUnitType.Star);

    /// <summary>未分栏的一侧收成 0、另一侧独占；分栏时按持久化比例分配星值。</summary>
    private void ApplySplit(bool vertical, bool enabled)
    {
        var ratio = enabled && _vm is not null ? _vm.Layout.SplitRatio(SplitKey(vertical)) : 1d;
        if (vertical)
        {
            _surface.RowDefinitions[0].Height = Star(enabled ? ratio : 1);
            _surface.RowDefinitions[2].Height = enabled ? Star(1 - ratio) : NoGap;
        }
        else
        {
            _surface.ColumnDefinitions[0].Width = Star(enabled ? ratio : 1);
            _surface.ColumnDefinitions[2].Width = enabled ? Star(1 - ratio) : NoGap;
        }
    }

    /// <summary>把手聚焦时方向键按 5% 步进，Home/End 撞到 20%/80% 边界（原型 keydown 分支）。</summary>
    private void OnSplitKeyDown(bool vertical, KeyEventArgs e)
    {
        if (_vm is null) return;
        var step = (vertical, e.Key) switch
        {
            (false, Key.Left) => -ShellWindowLayoutViewModel.SplitRatioStep,
            (false, Key.Right) => ShellWindowLayoutViewModel.SplitRatioStep,
            (true, Key.Up) => -ShellWindowLayoutViewModel.SplitRatioStep,
            (true, Key.Down) => ShellWindowLayoutViewModel.SplitRatioStep,
            _ => 0d,
        };
        if (step == 0 && e.Key is not (Key.Home or Key.End)) return;
        var key = SplitKey(vertical);
        _vm.Layout.SetSplitRatio(key, step != 0
            ? _vm.Layout.SplitRatio(key) + step
            : e.Key == Key.Home ? ShellWindowLayoutViewModel.SplitRatioMin : ShellWindowLayoutViewModel.SplitRatioMax);
        e.Handled = true;
        Arrange();
    }

    private string[] _topIds = [];

    /// <summary>拖拽落点后按实测块宽反推比例：GridSplitter 会把星值改写成像素值，只有量块不受单位影响。</summary>
    private void CaptureSplitRatio(bool vertical)
    {
        if (_vm is null) return;
        _surface.UpdateLayout();
        double first, second;
        if (vertical)
        {
            if (_topIds.Length < 1 || !_frames.TryGetValue("map", out var map)) return;
            first = _frames[_topIds[0]].Bounds.Height; second = map.Bounds.Height;
        }
        else
        {
            if (_topIds.Length < 2) return;
            first = _frames[_topIds[0]].Bounds.Width; second = _frames[_topIds[1]].Bounds.Width;
        }
        if (first > 0 && second > 0) _vm.Layout.SetSplitRatio(SplitKey(vertical), first / (first + second));
    }

    private static void NameSplit(GridSplitter splitter, string key)
    {
        var text = WorkbenchText.Get(key);
        ToolTip.SetTip(splitter, text);
        Avalonia.Automation.AutomationProperties.SetName(splitter, text);
    }
    public WorkspaceViewController(MainWindow window, Grid surface, StackPanel shelf)
    {
        _window = window; _surface = surface; _shelf = shelf;
        var source = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!;
        var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
        var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
        var map = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!;
        surface.Children.Clear();
        // 原型 .view-split{gap:1px;background:var(--border)}：面板齐平拼贴，缝即 1px 发丝线。
        surface.ColumnDefinitions = new ColumnDefinitions("*,1,*");
        surface.RowDefinitions = new RowDefinitions("*,1,*");
        _horizontalSplit.ZIndex = 9; _verticalSplit.ZIndex = 9;
        _horizontalSplit.Focusable = _verticalSplit.Focusable = true;
        _horizontalSplit.DragCompleted += (_, _) => CaptureSplitRatio(false);
        _verticalSplit.DragCompleted += (_, _) => CaptureSplitRatio(true);
        _horizontalSplit.AddHandler(InputElement.KeyDownEvent, (_, e) => OnSplitKeyDown(false, e), global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _verticalSplit.AddHandler(InputElement.KeyDownEvent, (_, e) => OnSplitKeyDown(true, e), global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        NameSplit(_horizontalSplit, "ViewSplitHorizontal"); NameSplit(_verticalSplit, "ViewSplitVertical");
        Grid.SetColumn(_horizontalSplit, 1); Grid.SetColumnSpan(_horizontalSplit, 1); Grid.SetRow(_horizontalSplit, 0);
        Grid.SetColumn(_verticalSplit, 0); Grid.SetColumnSpan(_verticalSplit, 3); Grid.SetRow(_verticalSplit, 1);
        surface.Children.Add(_horizontalSplit); surface.Children.Add(_verticalSplit);
        source.FindControl<Border>("SourceHeader")!.IsVisible = false;
        preview.FindControl<Border>("PaneHeader")!.IsVisible = false;
        var editors = new Grid(); editors.Children.Add(source); editors.Children.Add(instant);
        Add("editor", editors); Add("render", preview); Add("map", map);
        // 原型把格式条移进编辑块（panels.editor.insertBefore(..., .view-body)）：块内才能被圆角裁切，
        // 也让浮动窗格带走工具条。声明仍在 MainWindow 命名空间里，这里只迁走宿主。
        _editorOptions = window.FindControl<Control>("EditorOptions");
        if (_editorOptions is not null && _editorLayout is not null)
        {
            (_editorOptions.Parent as Panel)?.Children.Remove(_editorOptions);
            _editorOptions.ZIndex = 2;
            _editorLayout.Children.Add(_editorOptions);
        }
        window.DataContextChanged += (_, _) => Attach();
        window.Closed += (_, _) =>
        {
            _closing = true;
            CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged -= CultureChanged;
            if (_vm is not null) { _vm.Workbench.PropertyChanged -= Changed; _vm.DocumentInfo.PropertyChanged -= DocumentChanged; }
            foreach (var floating in _floating.Values.ToArray()) floating.Close();
            if (_presentationSession is { } session) foreach (var floating in session.Windows.Values) floating.Close();
        };
        CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged += CultureChanged;
        Attach();
    }
    // 语言切换可能来自后台线程，把手文案与 ToolTip 都必须在 UI 线程写入。
    private void CultureChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        NameSplit(_horizontalSplit, "ViewSplitHorizontal"); NameSplit(_verticalSplit, "ViewSplitVertical");
        Arrange();
    });
    private void Attach()
    {
        if (_vm is not null) { _vm.Workbench.PropertyChanged -= Changed; _vm.DocumentInfo.PropertyChanged -= DocumentChanged; }
        if (_pinPreferences is not null) _pinPreferences.PreferencesChanged -= OnPinPreferenceChanged;
        _vm = _window.DataContext as MainWindowViewModel;
        if (_vm is not null) { _vm.Workbench.PropertyChanged += Changed; _vm.DocumentInfo.PropertyChanged += DocumentChanged; }
        _pinPreferences = _vm?.Workbench.Preferences;
        if (_pinPreferences is not null)
        {
            _pinPreferences.PreferencesChanged += OnPinPreferenceChanged;
            ReadingPin?.Bind(ToggleButton.IsCheckedProperty, new Binding("PinReadingToolbar") { Source = _pinPreferences });
        }
        Arrange();
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.IsPresenting))
        {
            var map = _window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
            if (_vm?.Workbench.IsPresenting == true) _presentationMapState = (map.CaptureViewport(), _vm.Workbench.Root);
            if (_vm?.Workbench.IsPresenting == true && _presentationSession is null) _presentationSession = SuspendFloating();
            else if (_vm?.Workbench.IsPresenting == false && _presentationSession is { } session) { _presentationSession = null; RestoreFloating(session); }
            if (_vm?.Workbench.IsPresenting == false && _presentationMapState is { } state)
            {
                _presentationMapState = null;
                if (ReferenceEquals(state.Root, _vm.Workbench.Root)) Dispatcher.UIThread.Post(() => map.RestoreViewport(state.Viewport), DispatcherPriority.Loaded);
            }
        }
        if (e.PropertyName is nameof(ShellWorkbenchViewModel.WorkspaceLayout) or nameof(ShellWorkbenchViewModel.IsPresenting) or nameof(ShellWorkbenchViewModel.PresentationMap)) Arrange();
    }
    private void DocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateTitles();
    private void UpdateTitles()
    { foreach (var (id, floating) in _floating) floating.Title = Label(id) + " · " + _vm?.DocumentInfo.PropertyNameText + " · Vex"; }
    private string Label(string id) => WorkbenchText.Get(id == "editor" ? _vm?.Workbench.EditMode == "source" ? "WSSource" : "WSInstant" : id == "map" ? "WSMap" : "WSRead");
    private void Add(string id, Control content)
    {
        content.ClearValue(Grid.ColumnProperty); content.ClearValue(Grid.ColumnSpanProperty); content.ClearValue(Grid.RowProperty);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        if (id == "editor") _editorLayout = layout;
        // Border 的圆角不裁子级：窗格自绘底色（编辑器/预览纸面）会顶穿块的四角，内层按内圆角 9px 裁切。
        // 原型面板直角（.view-panel radius 0），不再圆角裁切。
        layout.SizeChanged += (_, e) => layout.Clip = _vm?.Workbench.IsPresenting == true
            ? null
            : new RectangleGeometry(new Rect(default, e.NewSize), 0, 0);
        if (id == "render")
        {
            // 原型 .view-heading：36px、随块底透明、65% 底边；题注居左、排版入口居中、钉按钮居右。编辑器与导图窗格按原型不设标题行。
            var header = new Border { Height = 36, BorderThickness = new Thickness(0, 0, 0, 1) };
            header.Bind(Border.BorderBrushProperty, _window.GetResourceObservable("VexPaneHeadBorderBrush"));
            var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), VerticalAlignment = VerticalAlignment.Center };
            var caption = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var icon = new global::Avalonia.Controls.Shapes.Path { Classes = { "wb-icon" }, Width = 12, Height = 12,
                Data = Geometry.Parse("M2 12C7 3 17 3 22 12C17 21 7 21 2 12Z M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12"), VerticalAlignment = VerticalAlignment.Center };
            caption.Children.Add(icon);
            var label = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Tag = id };
            label.Bind(TextBlock.ForegroundProperty, _window.GetResourceObservable("VexMutedBrush"));
            caption.Children.Add(label);
            Grid.SetColumn(caption, 0); bar.Children.Add(caption);
            _renderCaption = caption; _renderLabel = label;
            var typography = new Button { Classes = { "wb-quiet" }, VerticalAlignment = VerticalAlignment.Center };
            // 反射方法绑定（Binding("Workbench.ToggleTypography")）在裁剪后的发布产物里解析不出命令，改直调视图模型。
            typography.Click += (_, _) => _vm?.Workbench.ToggleTypography();
            _renderTypographyTrigger = typography;
            _renderTypographyPrefix = new TextBlock { Text = WorkbenchText.Get("Typography"), VerticalAlignment = VerticalAlignment.Center };
            _renderTypographyName = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var trigger = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            trigger.Children.Add(_renderTypographyPrefix); trigger.Children.Add(_renderTypographyName);
            trigger.Children.Add(new global::Avalonia.Controls.Shapes.Path { Classes = { "expand-chevron" }, Data = Geometry.Parse("M6 9L12 15L18 9") });
            typography.Content = trigger;
            Grid.SetColumn(typography, 1); bar.Children.Add(typography);
            var pin = new ToggleButton { Classes = { "toolbar-pin" }, Padding = new Thickness(4, 2), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            pin.Content = BuildPinIcon(14);
            ToolTip.SetTip(pin, WorkbenchText.Get("WBPinToolbar"));
            Avalonia.Automation.AutomationProperties.SetName(pin, WorkbenchText.Get("WBPinToolbar"));
            ReadingPin = pin;
            Grid.SetColumn(pin, 2); bar.Children.Add(pin);
            header.ZIndex = 1;
            header.Child = bar; _headers[id] = header; layout.Children.Add(header); Grid.SetRow(content, 1);
        }
        layout.Children.Add(content);
        // 窗格内容一律占第二行（*）：Auto 行只留给题注，否则无期望高度的窗格（导图）会被压扁。
        Grid.SetRow(content, 1);
        var frame = new Border { Classes = { "workspace-block" }, Child = layout, Tag = id };
        frame.Bind(Border.BorderBrushProperty, _window.GetResourceObservable("VexPanelBorderBrush")); _frames[id] = frame;
    }
    public void Arrange()
    {
        if (_vm is null || _arranging || _closing) return;
        _arranging = true;
        try
        {
            var state = _vm.Workbench; var option = state.LayoutOption;
            var wanted = state.IsPresenting ? new[] { state.PresentationMap ? "map" : "render" } : new[] { option.Editor is null ? null : "editor", option.Render ? "render" : null, option.Map ? "map" : null }.OfType<string>().ToArray();
            var viewports = _frames.Values.SelectMany(frame => frame.GetVisualDescendants().OfType<ScrollViewer>()).Select(scroll => (scroll, scroll.Offset)).ToArray();
            foreach (var id in _floating.Keys.Where(id => !wanted.Contains(id)).ToArray()) Return(id);
            _surface.Children.Remove(_empty);
            foreach (var (id, frame) in _frames)
            {
                frame.DataContext = _vm;
                frame.IsVisible = wanted.Contains(id);
                // 原型 .deck-stage 是整幅纸面：演示时窗格不带块描边、圆角与投影，内层裁切也一并放开，
                // 退出演示还权给 .workspace-block 样式（本地值会盖住样式，所以必须 ClearValue 而不是设回字面量）。
                if (state.IsPresenting)
                {
                    frame.CornerRadius = new CornerRadius(0);
                    frame.BorderThickness = new Thickness(0);
                    frame.BoxShadow = default;
                    if (frame.Child is Grid bleed) bleed.Clip = null;
                }
                else
                {
                    frame.ClearValue(Border.CornerRadiusProperty);
                    frame.ClearValue(Border.BorderThicknessProperty);
                    frame.ClearValue(Border.BoxShadowProperty);
                    if (frame.Child is Grid bleed && bleed.Bounds.Size != default)
                        bleed.Clip = new RectangleGeometry(new Rect(default, bleed.Bounds.Size), 9, 9);
                }
                if (!_floating.ContainsKey(id) && frame.Parent is null) _surface.Children.Add(frame);
            }
            // 原型只为阅读窗格保留标题行：多窗格带题注与底边，单窗格题注隐藏、无边框右对齐。
            if (_headers.TryGetValue("render", out var renderHeader) && renderHeader.Child is Grid renderBar)
            {
                _renderLabel!.Text = Label("render");
                _renderTypographyPrefix!.Text = WorkbenchText.Get("Typography");
                // NameKey 已含完整 Vex.VexL. 前缀，直接查资源，避免 WorkbenchText.Get 二次拼接。
                _renderTypographyName!.Text = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(_vm.Appearance.SelectedTypography?.NameKey ?? "Vex.VexL.TypographyBasic") ?? string.Empty;
                renderHeader.IsVisible = wanted.Contains("render") && !state.IsPresenting && !_floating.ContainsKey("render");
                _renderCaption!.IsVisible = wanted.Length > 1;
                renderHeader.BorderThickness = wanted.Length > 1 ? new Thickness(0, 0, 0, 1) : new Thickness(0);
                // 多窗格 space-between（入口居中）；单窗格 justify-end（入口与钉一起靠右）。
                renderBar.ColumnDefinitions[2].Width = wanted.Length > 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Auto);
            }
            // 阅读正文留白/限宽由 MarkdownPreviewView.ApplyDocumentGeometry 统一处理。
            ApplyReadingPin(); ApplyEditorPin();
            var local = wanted.Where(id => !_floating.ContainsKey(id)).ToArray();
            // 原型视图网格 = */间隙/*：上下两行时导图横向铺满，未启用的槽位与间隙收成 0。
            var top = local.Length == 3 ? local[..2] : local;
            var twoUp = local.Length >= 2;
            var stacked = local.Length == 3;
            _topIds = top;
            // 原型 makeHandle().apply()：比例来自持久化的「预设 : 轴向」记录，未分栏的一侧收成 0。
            ApplySplit(false, twoUp); ApplySplit(true, stacked);
            _surface.ColumnDefinitions[1].Width = twoUp ? Gap : NoGap;
            _surface.RowDefinitions[1].Height = stacked ? Gap : NoGap;
            _horizontalSplit.IsVisible = twoUp;
            _verticalSplit.IsVisible = stacked;
            for (var i = 0; i < top.Length; i++) Place(top[i], i * 2, 0);
            if (stacked) Place("map", 0, 2, 3);
            if (local.Length == 0) { _empty.Text = WorkbenchText.Get("WSSeparate"); _surface.Children.Add(_empty); }
            _shelf.Children.Clear(); _shelf.IsVisible = _floating.Count > 0;
            foreach (var (id, floating) in _floating)
            {
                floating.Title = Label(id) + " · " + _vm.DocumentInfo.PropertyNameText + " · Vex";
                var back = new Button { Classes = { "wb-quiet" }, Content = Label(id) + " · " + WorkbenchText.Get("WSReturn") };
                back.Click += (_, _) => Return(id); _shelf.Children.Add(back);
            }
            Dispatcher.UIThread.Post(() => { foreach(var (scroll,offset) in viewports) scroll.Offset = offset; },DispatcherPriority.Loaded);
        }
        finally { _arranging = false; }
    }
    private void Place(string id,int column,int row,int span=1)
    {
        var frame=_frames[id]; Grid.SetColumn(frame,column); Grid.SetRow(frame,row); Grid.SetColumnSpan(frame,span);
        if (!_surface.Children.Contains(frame)) _surface.Children.Add(frame);
    }
    public void Float(string id)
    {
        if (_vm is null || !_frames.ContainsKey(id)) return;
        var option = _vm.Workbench.LayoutOption;
        if (id == "editor" && option.Editor is null || id == "render" && !option.Render || id == "map" && !option.Map) return;
        if (_floating.TryGetValue(id,out var existing)) { existing.Activate(); return; }
        var frame=_frames[id]; if(frame.Parent is Panel panel) panel.Children.Remove(frame);
        _window.UpdateLayout();
        var floating=new CodeWFWindow { Width=920,Height=740,MinWidth=540,MinHeight=360,TitleBarHeight=40,CaptionButtonWidth=46,CaptionGlyphSize=14,DataContext=_vm,Content=frame,Icon=_window.Icon };
        var occupied = _floating.Values.Select(view => _window.Screens.ScreenFromWindow(view)).Append(_window.Screens.ScreenFromWindow(_window)).ToArray();
        var screen = _window.Screens.All.FirstOrDefault(candidate => !occupied.Any(used => Equals(used, candidate)));
        if (screen is not null)
        {
            var area = screen.WorkingArea; var scaling = screen.Scaling;
            floating.Width = Math.Min(floating.Width, area.Width / scaling); floating.Height = Math.Min(floating.Height, area.Height / scaling);
            floating.Position = new PixelPoint(area.X + (int)((area.Width - floating.Width * scaling) / 2), area.Y + (int)((area.Height - floating.Height * scaling) / 2));
        }
        else floating.Position = new PixelPoint(_window.Position.X + 36 * (_floating.Count + 1), _window.Position.Y + 36 * (_floating.Count + 1));
        var header=new StackPanel { Orientation=Orientation.Horizontal,Spacing=12,Margin=new Thickness(16,0),VerticalAlignment=VerticalAlignment.Center };
        var name=new TextBlock {FontSize=12,VerticalAlignment=VerticalAlignment.Center}; name.Bind(TextBlock.TextProperty,new Binding("DocumentInfo.PropertyNameText")); header.Children.Add(name);
        var back=new Button {Content=WorkbenchText.Get("WSReturn"),Classes={"wb-quiet"}}; back.Click+=(_,_)=>Return(id); header.Children.Add(back); floating.LeftContent=header;
        _floating[id]=floating;
        floating.Closed+=(_,_)=>{if(_floating.Remove(id)){floating.Content=null;Arrange();}};
        floating.AddHandler(InputElement.KeyDownEvent, (_,e)=>_window.ForwardWorkspaceShortcut(e),global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        floating.Show(_window); Arrange();
    }
    public void Separate()
    {
        if(_vm is null)return;
        if(_vm.Workbench.LayoutOption.Render)Float("render");
        if(_vm.Workbench.LayoutOption.Map)Float("map");
    }
    public void Return(string id)
    {
        if(!_floating.Remove(id,out var floating))return;
        floating.Content=null; floating.UpdateLayout(); floating.Close(); Arrange();
    }
    public FloatingSession SuspendFloating()
    {
        var session = new FloatingSession(new Dictionary<string, CodeWFWindow>(_floating));
        _floating.Clear();
        foreach (var floating in session.Windows.Values) { floating.Content = null; floating.UpdateLayout(); floating.Hide(); }
        Arrange(); return session;
    }
    public void RestoreFloating(FloatingSession session)
    {
        foreach (var (id, floating) in session.Windows)
        {
            var frame = _frames[id]; if (frame.Parent is Panel panel) panel.Children.Remove(frame);
            _window.UpdateLayout();
            _floating[id] = floating; floating.Content = frame; floating.Show(_window);
        }
        Arrange();
    }
}
