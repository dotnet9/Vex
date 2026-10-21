using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.Lang;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

public sealed class ShellTypographyGallery : UserControl
{
    private MainWindowViewModel? _vm;
    private readonly Grid _cards = new() { ColumnSpacing = 12, RowSpacing = 12 };
    private readonly Dictionary<TypographyOption, (Button Button, MarkdownViewer Preview, TextBlock Check)> _items = [];
    private bool _all;
    private int _columns = 3;
    private TopLevel? _window;
    private ScrollViewer? _scroll;
    private Button? _recommended;
    private Button? _allChoices;
    private CheckBox? _compact;
    public ShellTypographyGallery()
    {
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { Attach(); _window = TopLevel.GetTopLevel(this); if (_window is not null) _window.SizeChanged += WindowSizeChanged; Resize(); };
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) { _vm.Appearance.PropertyChanged -= Changed; _vm.Workbench.PropertyChanged -= WorkbenchChanged; } I18nManager.Instance.CultureChanged -= CultureChanged; if (_window is not null) _window.SizeChanged -= WindowSizeChanged; _vm = null; _window = null; };
        Navigation(_cards, () => _columns);
    }
    private void WindowSizeChanged(object? sender, SizeChangedEventArgs e) => Resize();
    private void Attach()
    {
        var vm = DataContext as MainWindowViewModel;
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null) { _vm.Appearance.PropertyChanged -= Changed; _vm.Workbench.PropertyChanged -= WorkbenchChanged; }
        I18nManager.Instance.CultureChanged -= CultureChanged;
        _vm = vm;
        if (vm is null) return;
        vm.Appearance.PropertyChanged += Changed; vm.Workbench.PropertyChanged += WorkbenchChanged; I18nManager.Instance.CultureChanged += CultureChanged; Build();
    }
    private void WorkbenchChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ShellWorkbenchViewModel.ShowTypography) && _vm?.Workbench.ShowTypography == true) Resize(); }
    private void CultureChanged(object? sender, EventArgs e)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_vm is not null) Build(); });
            return;
        }
        Build();
    }
    private void Resize()
    {
        Width = Math.Min(880, (_window?.ClientSize.Width ?? 1280) - 24);
        var anchor = (_window as MainWindow)?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "TypographyTarget" && b.IsEffectivelyVisible);
        var top = anchor?.TranslatePoint(new Point(0, anchor.Bounds.Height + 8), _window!)?.Y ?? 126;
        Margin = new Thickness(12, top, 12, 12);
        Height = Math.Max(140, Math.Min(680, (_window?.ClientSize.Height ?? 820) - top - 12));
        _columns = Width >= 820 ? 3 : Width >= 550 ? 2 : 1;
        ArrangeCards();
    }
    private void ArrangeCards()
    {
        _cards.ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", _columns)));
        var visible = _items.Values.Where(item => item.Button.IsVisible).ToArray();
        _cards.RowDefinitions = new RowDefinitions(string.Join(',', Enumerable.Repeat("Auto", (visible.Length + _columns - 1) / _columns)));
        for (var i = 0; i < visible.Length; i++) { Grid.SetColumn(visible[i].Button, i % _columns); Grid.SetRow(visible[i].Button, i / _columns); }
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
        if (_recommended is not null) { _recommended.Content = (!_all ? "✓ " : "") + Text("ChoiceRecommended"); _recommended.Classes.Set("choice-filter-selected", !_all); }
        if (_allChoices is not null) { _allChoices.Content = (_all ? "✓ " : "") + Text("AllTypographies"); _allChoices.Classes.Set("choice-filter-selected", _all); }
        if (_compact is not null) _compact.IsChecked = _vm.Appearance.IsCompactLayout;
        ArrangeCards();
    }
    private void Build()
    {
        if (_vm is null) return;
        if (_scroll is not null) _scroll.Content = null;
        _items.Clear(); _cards.Children.Clear();
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        var title = Label(Text("WBTypographyTitle"), 15); title.FontWeight = FontWeight.SemiBold; title.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(title);
        var close = new Button { Classes = { "vex-dismiss" }, Width = 30, Height = 30 };
        Avalonia.Automation.AutomationProperties.SetName(close, Text("Close"));
        close.Click += (_, _) => { _vm.Workbench.ShowTypography = false; (_window as MainWindow)?.FindControl<ShellEditorOptionsView>("EditorOptions")?.FindControl<Button>("TypographyTarget")?.Focus(); };
        Grid.SetColumn(close, 1); header.Children.Add(close);
        var filters = new UniformGrid { Columns = 2 };
        _recommended = Button(Text("ChoiceRecommended"), () => { _all = false; Changed(null, new("")); if (_scroll is not null) _scroll.Offset = default; });
        _allChoices = Button(Text("AllTypographies"), () => { _all = true; Changed(null, new("")); if (_scroll is not null) _scroll.Offset = default; });
        _recommended.HorizontalAlignment = HorizontalAlignment.Stretch; _allChoices.HorizontalAlignment = HorizontalAlignment.Stretch;
        filters.Children.Add(_recommended); filters.Children.Add(_allChoices);
        var filterFrame = new Border { Classes = { "choice-filter" }, Width = 240, HorizontalAlignment = HorizontalAlignment.Left, Child = filters, Margin = new Thickness(0, 0, 0, 12) };
        _compact = new CheckBox { IsChecked = _vm.Appearance.IsCompactLayout, FontSize = 13, MinHeight = 20 };
        Avalonia.Automation.AutomationProperties.SetName(_compact, Text("CompactLayout"));
        _compact.IsCheckedChanged += (_, _) => _vm.Appearance.IsCompactLayout = _compact.IsChecked == true;
        var footer = new Grid { ColumnDefinitions = new("*,Auto") };
        var compactLabel = Label(Text("CompactLayout"), 13); compactLabel.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(compactLabel);
        Grid.SetColumn(_compact, 1); footer.Children.Add(_compact);
        var footerFrame = new Border { Child = footer, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(0, 12, 0, 0) };
        Resource(footerFrame, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        foreach (var option in _vm.Appearance.TypographyOptions)
        {
            var heading = new Grid { ColumnDefinitions = new("*,Auto") };
            heading.MinHeight = 22; var name = Label(Text(option.NameKey), 13); name.FontWeight = FontWeight.SemiBold;
            heading.Children.Add(name); var check = Label("", 11); Resource(check, TextBlock.ForegroundProperty, this, "VexAccentBrush"); Grid.SetColumn(check, 1); heading.Children.Add(check);
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
            button.VerticalAlignment = VerticalAlignment.Stretch; button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Top; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.BorderThickness = new Thickness(1); button.CornerRadius = new CornerRadius(8);
            Resource(button, TemplatedControl.BorderBrushProperty, this, "VexPanelBorderBrush");
            _items[option] = (button, preview, check); _cards.Children.Add(button);
        }
        var grid = new Grid { RowDefinitions = new("Auto,Auto,Auto,*,Auto") };
        grid.Children.Add(header); var hint = Label(Text("WBTypographyHint")); hint.Classes.Add("muted"); hint.LineHeight = 19.2; hint.Margin = new Thickness(0, 4, 0, 12); Grid.SetRow(hint, 1); grid.Children.Add(hint);
        Grid.SetRow(filterFrame, 2); grid.Children.Add(filterFrame);
        _scroll = new ScrollViewer { Content = _cards, Padding = new Thickness(2, 2, 4, 2), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(_scroll, 3); grid.Children.Add(_scroll);
        Grid.SetRow(footerFrame, 4); grid.Children.Add(footerFrame);
        var border = new Border { Classes = { "wb-popover" }, Child = grid, Padding = new Thickness(16), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 8 30 0 #20000000") };
        Resource(border, Border.BackgroundProperty, this, "VexPanelBackgroundBrush"); Resource(border, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        Content = border; Resize(); Changed(null, new(""));
    }
}
