using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>原型 .workspace-layout-menu：4 列 8 张布局预设卡，点一张即切换并收起。</summary>
public sealed class WorkspaceLayoutGallery : Border
{
    protected override Type StyleKeyOverride => typeof(Border);
    private ShellWorkbenchViewModel? _workbench;
    public WorkspaceLayoutGallery()
    {
        // 原型 .workspace-layout-menu：568 宽、14 内距；面/描边/圆角/投影都吃 wb-popover 的浮层一档。
        Classes.Add("wb-popover"); Width = 568; Padding = new Thickness(14);
        AttachedToVisualTree += (_, _) => { if (TopLevel.GetTopLevel(this) is { } window) { window.SizeChanged += WindowResized; Fit(window); } };
        DetachedFromVisualTree += (_, _) => { if (TopLevel.GetTopLevel(this) is { } window) window.SizeChanged -= WindowResized; };
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged += CultureChanged; Attach(); };
        DetachedFromVisualTree += (_, _) => { CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged -= CultureChanged; if (_workbench is not null) _workbench.PropertyChanged -= Changed; };
    }
    private void WindowResized(object? sender, SizeChangedEventArgs e) { if (sender is TopLevel window) Fit(window); }
    // 原型 .workspace-layout-menu{width:568px;max-width:calc(100vw - 24px);height:fit-content}：
    // 8 张卡两行装得下，同文件 153 把 max-height 归 none、overflow 归 visible，不再滚动；底距交给宿主 XAML。
    private void Fit(TopLevel window) => Width = Math.Min(568, window.ClientSize.Width - 24);
    private void CultureChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(Build);
    private void Attach()
    {
        if (_workbench is not null) _workbench.PropertyChanged -= Changed;
        _workbench = (DataContext as MainWindowViewModel)?.Workbench;
        if (_workbench is not null) _workbench.PropertyChanged += Changed;
        Build();
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ShellWorkbenchViewModel.WorkspaceLayout)) Build(); }
    private void Build()
    {
        if (_workbench is null) return;
        // 原型 .workspace-layout-menu 里只有 .layout-preset-grid 这一个子级（workspace.js:103）：
        // 4 列 8 张预设卡，无标题、无说明段、无页脚，选中态由卡片自身的 accent 描边 + 右上角 ✓ 表达。
        var options = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 8, RowSpacing = 8 };
        DesignSelectionControls.Navigation(options, () => 4);
        foreach (var (option, index) in WorkbenchLayoutOption.All.Select((value, index) => (value, index)))
        {
            var selected = option.Id == _workbench.WorkspaceLayout;
            var button = new Button { Classes = { "layout-preset" }, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            if (selected) button.Classes.Add("preset-on");
            var body = new StackPanel { Spacing = 3 };
            body.Children.Add(Miniature(option.Diagram));
            body.Children.Add(new TextBlock { Text = option.Name, FontSize = 12, LineHeight = 16, FontWeight = FontWeight.Normal, TextWrapping = TextWrapping.Wrap, MinHeight = 16 });
            // 原型 .preset-check{position:absolute;top:3px;right:5px}：负外边距把 ✓ 从 10 内距里拔回描边内侧。
            var check = new TextBlock { Text = "✓", FontSize = 10, LineHeight = 10, IsVisible = selected, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -7, -5, 0) };
            check.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexAccentBrush"));
            var face = new Grid();
            face.Children.Add(body); face.Children.Add(check);
            button.Content = face; button.Tag = option.Id;
            // 原型 preset.onclick = setLayout + closeMenus(true)：选一张即收，不留连续比较的展开态。
            button.Click += (_, _) => { _workbench.SelectLayout(option.Id); _workbench.ShowLayouts = false; };
            Grid.SetColumn(button, index % 4); Grid.SetRow(button, index / 4); options.Children.Add(button);
        }
        Child = options;
    }

    private Control Miniature(string diagram)
    {
        var rows = diagram.Split(';');
        var grid = new Grid { RowDefinitions = new RowDefinitions(string.Join(',', rows.Select(_ => "*"))), RowSpacing = 3 };
        foreach (var (row, rowIndex) in rows.Select((value,index)=>(value,index)))
        {
            var parts = row.Split(','); var line = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(',', parts.Select(_ => "*"))), ColumnSpacing = 3 };
            foreach (var (part, index) in parts.Select((value,index)=>(value,index)))
            {
                var pane = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
                pane.Bind(BorderBrushProperty, this.GetResourceObservable("VexAccentBrush")); pane.Bind(BackgroundProperty, this.GetResourceObservable("VexAccentSoftBrush"));
                var drawing = new LayoutMiniature(part); drawing.Bind(LayoutMiniature.ForegroundProperty, this.GetResourceObservable("VexAccentBrush")); pane.Child = drawing;
                Grid.SetColumn(pane,index); line.Children.Add(pane);
            }
            Grid.SetRow(line,rowIndex); grid.Children.Add(line);
        }
        var frame = new Border { Height = 44, Padding = new Thickness(5), Child = grid, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 3) };
        frame.Bind(BorderBrushProperty, this.GetResourceObservable("VexPanelBorderBrush"));
        frame.Bind(BackgroundProperty, this.GetResourceObservable("VexPanelBackgroundBrush"));
        return frame;
    }

    private sealed class LayoutMiniature(string kind) : Control
    {
        public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<LayoutMiniature, IBrush?>(nameof(Foreground));
        public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public override void Render(DrawingContext context)
        {
            var width = Bounds.Width; var height = Bounds.Height; var brush = Foreground ?? Brushes.MediumPurple;
            var pen = new Pen(brush, 1);
            if (kind == "map")
            {
                context.DrawRectangle(null, pen, new Rect(width * .35, height * .3, width * .3, height * .35), 2, 2);
            }
        }
    }
}
