using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.Lang;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

public sealed class ShellTypographyGallery : UserControl
{
    private MainWindowViewModel? _vm;
    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<TypographyOption, (Button Button, MarkdownViewer Preview, TextBlock Check)> _items = [];
    private bool _all;
    private int _columns = 3;
    private TopLevel? _window;
    private ScrollViewer? _scroll;
    public ShellTypographyGallery()
    {
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { Attach(); _window = TopLevel.GetTopLevel(this); if (_window is not null) _window.SizeChanged += WindowSizeChanged; Resize(); };
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) _vm.Appearance.PropertyChanged -= Changed; I18nManager.Instance.CultureChanged -= CultureChanged; if (_window is not null) _window.SizeChanged -= WindowSizeChanged; _vm = null; _window = null; };
        Navigation(_cards, () => _columns);
    }
    private void WindowSizeChanged(object? sender, SizeChangedEventArgs e) => Resize();
    private void Attach()
    {
        var vm = DataContext as MainWindowViewModel;
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null) _vm.Appearance.PropertyChanged -= Changed;
        I18nManager.Instance.CultureChanged -= CultureChanged;
        _vm = vm;
        if (vm is null) return;
        vm.Appearance.PropertyChanged += Changed; I18nManager.Instance.CultureChanged += CultureChanged; Build();
    }
    private void CultureChanged(object? sender, EventArgs e) => Build();
    private void Resize()
    {
        Width = Math.Min(880, (_window?.ClientSize.Width ?? 1280) - 24);
        Height = Math.Min(680, (_window?.ClientSize.Height ?? 820) - 116);
        _columns = Width >= 820 ? 3 : Width >= 550 ? 2 : 1;
        foreach (var item in _items.Values) item.Button.Width = (Width - 34) / _columns - 12;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        foreach (var (option, item) in _items)
        {
            var selected = option == _vm.Appearance.SelectedTypography;
            item.Button.Classes.Set("selected-choice", selected);
            item.Check.Text = selected ? "✓ " + Text("ChoiceCurrent") : "";
            item.Button.IsVisible = _all || option.IsRecommended || selected;
            item.Preview.TypographySize = _vm.Appearance.CurrentTypographySize;
        }
    }
    private void Build()
    {
        if (_vm is null) return;
        if (_scroll is not null) _scroll.Content = null;
        _items.Clear(); _cards.Children.Clear();
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label(Text("WBTypographyTitle"), 15));
        var close = Button("×", () => { _vm.Workbench.ShowTypography = false; }); Grid.SetColumn(close, 1); header.Children.Add(close);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        filters.Children.Add(Button(Text("ChoiceRecommended"), () => { _all = false; Changed(null, new("")); }));
        filters.Children.Add(Button(Text("AllTypographies"), () => { _all = true; Changed(null, new("")); }));
        var compact = new CheckBox { Content = Text("CompactLayout"), IsChecked = _vm.Appearance.IsCompactLayout, FontSize = 13 };
        compact.IsCheckedChanged += (_, _) => _vm.Appearance.IsCompactLayout = compact.IsChecked == true;
        filters.Children.Add(compact);
        foreach (var option in _vm.Appearance.TypographyOptions)
        {
            var heading = new Grid { ColumnDefinitions = new("*,Auto") };
            heading.Children.Add(Label(Text(option.NameKey), 13)); var check = Label(""); Grid.SetColumn(check, 1); heading.Children.Add(check);
            var preview = new MarkdownViewer { Markdown = Text("TypographyPreviewMarkdown"), TypographyTheme = option.Key,
                FontWeight = FontWeight.Normal,
                TypographySize = _vm.Appearance.CurrentTypographySize, IsHitTestVisible = false, Margin = new Thickness(16),
                DocumentBottomPadding = 0, EnableVirtualization = false };
            var previewBorder = new Border { Child = preview, MinHeight = 274, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), ClipToBounds = true };
            Resource(previewBorder, Border.BackgroundProperty, this, "VexDocumentPaperBrush");
            Resource(previewBorder, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(heading); var purpose = Label(Text(option.PurposeKey)); purpose.Classes.Add("muted"); stack.Children.Add(purpose); stack.Children.Add(previewBorder);
            var button = Button("", () => _vm.Appearance.SelectedTypography = option);
            button.Name = "Typography_" + option.Key; button.Content = stack; button.Padding = new Thickness(12); button.MinHeight = 332;
            button.VerticalAlignment = VerticalAlignment.Top;
            button.Margin = new Thickness(0, 0, 12, 12); button.VerticalContentAlignment = VerticalAlignment.Top; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.BorderThickness = new Thickness(1); button.CornerRadius = new CornerRadius(8);
            Resource(button, TemplatedControl.BorderBrushProperty, this, "VexPanelBorderBrush");
            _items[option] = (button, preview, check); _cards.Children.Add(button);
        }
        var grid = new Grid { RowDefinitions = new("Auto,Auto,Auto,*"), RowSpacing = 10 };
        grid.Children.Add(header); var hint = Label(Text("WBTypographyHint")); hint.Classes.Add("muted"); Grid.SetRow(hint, 1); grid.Children.Add(hint);
        Grid.SetRow(filters, 2); grid.Children.Add(filters);
        _scroll = new ScrollViewer { Content = _cards, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(_scroll, 3); grid.Children.Add(_scroll);
        var border = new Border { Child = grid, Padding = new Thickness(16), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 8 30 0 #20000000") };
        Resource(border, Border.BackgroundProperty, this, "VexPanelBackgroundBrush"); Resource(border, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        Content = border; Resize(); Changed(null, new(""));
    }
}
