using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>选择立即生效，面板保持展开供连续比较。</summary>
public sealed class WorkspaceLayoutGallery : Border
{
    protected override Type StyleKeyOverride => typeof(Border);
    private ShellWorkbenchViewModel? _workbench;
    public WorkspaceLayoutGallery()
    {
        Classes.Add("wb-popover"); Width = 568; Padding = new Thickness(18); CornerRadius = new CornerRadius(8);
        AttachedToVisualTree += (_, _) => { if (TopLevel.GetTopLevel(this) is { } window) { window.SizeChanged += WindowResized; Fit(window); } };
        DetachedFromVisualTree += (_, _) => { if (TopLevel.GetTopLevel(this) is { } window) window.SizeChanged -= WindowResized; };
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged += CultureChanged; Attach(); };
        DetachedFromVisualTree += (_, _) => { CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged -= CultureChanged; if (_workbench is not null) _workbench.PropertyChanged -= Changed; };
    }
    private void WindowResized(object? sender, SizeChangedEventArgs e) { if (sender is TopLevel window) Fit(window); }
    private void Fit(TopLevel window) { Width = Math.Min(568, window.ClientSize.Width - 24); MaxHeight = Math.Max(160, window.ClientSize.Height - 120); Margin = new Thickness(12, 12, 24, 44); }
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
        var stack = new StackPanel { Spacing = 0 };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new TextBlock { Text = WorkbenchText.Get("WSLayoutTitle"), FontWeight = FontWeight.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Classes = { "vex-dismiss" } };
        AutomationProperties.SetName(close, CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(VexL.Close));
        close.Click += (_, _) => _workbench.ShowLayouts = false; Grid.SetColumn(close, 1); heading.Children.Add(close); stack.Children.Add(heading);
        var note = new TextBlock { Text = WorkbenchText.Get("WSLayoutHint"), FontSize = 12, LineHeight = 20.4, Margin = new Thickness(0, 3, 0, 18), TextWrapping = TextWrapping.Wrap };
        note.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexMutedBrush")); stack.Children.Add(note);
        var options = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 8, RowSpacing = 8 };
        DesignSelectionControls.Navigation(options, () => 4);
        foreach (var (option, index) in WorkbenchLayoutOption.All.Select((value, index) => (value, index)))
        {
            var selected = option.Id == _workbench.WorkspaceLayout;
            var button = new Button { Padding = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), MinHeight = 0 };
            button.BorderBrush = selected ? null : Brushes.Transparent; if (selected) button.Bind(BorderBrushProperty, this.GetResourceObservable("VexAccentBrush"));
            button.Background = selected ? null : Brushes.Transparent; if (selected) button.Bind(BackgroundProperty, this.GetResourceObservable("VexAccentSoftBrush"));
            var content = new StackPanel { Spacing = 3 };
            content.Children.Add(Miniature(option.Diagram));
            content.Children.Add(new TextBlock { Text = option.Name, FontSize = 12, LineHeight = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MinHeight = 16 });
            var hint = new TextBlock { Text = option.Description, FontSize = 10, LineHeight = 15, TextWrapping = TextWrapping.Wrap };
            hint.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexMutedBrush")); content.Children.Add(hint);
            button.Content = content; button.Tag = option.Id; button.Click += (_, _) => _workbench.SelectLayout(option.Id);
            Grid.SetColumn(button, index % 4); Grid.SetRow(button, index / 4); options.Children.Add(button);
        }
        stack.Children.Add(options);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var noteLabel = new TextBlock { Text = DesignSelectionControls.Text("WSLayoutFooter"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        noteLabel.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexMutedBrush")); footer.Children.Add(noteLabel);
        var reset = new Button { Classes = { "wb-quiet" }, Content = WorkbenchText.Get("WSDefault"), FontSize = 11, Padding = new Thickness(0, 3) };
        reset.Click += (_, _) => _workbench.SelectLayout("instant"); Grid.SetColumn(reset, 1); footer.Children.Add(reset);
        var footerBorder = new Border { Child = footer, Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(0, 10, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0) };
        footerBorder.Bind(BorderBrushProperty, this.GetResourceObservable("VexPanelBorderBrush")); stack.Children.Add(footerBorder);
        Child = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
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
