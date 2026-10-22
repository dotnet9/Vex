using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CodeWF.Toolkit.Lang;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

public sealed class ShellAppearanceMenuView : UserControl
{
    private ShellAppearanceViewModel? _vm;
    private bool _all;
    private readonly StackPanel _palettes = new() { Spacing = 4 };
    private readonly Dictionary<PaletteOption, Button> _paletteButtons = [];
    private readonly Dictionary<ThemeOption, RadioButton> _modeButtons = [];
    private readonly Dictionary<LanguageOption, Button> _languageButtons = [];
    private Button? _recommended;
    private Button? _allChoices;
    private TextBlock? _selection;
    private ScrollViewer? _scroll;
    public Button AppearanceButton { get; private set; } = null!;
    public Button LanguageButton { get; private set; } = null!;
    public const double MenuWidth = 248;

    public ShellAppearanceMenuView()
    {
        Navigation(_palettes, () => 1);
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) _vm.PropertyChanged -= Changed; I18nManager.Instance.CultureChanged -= CultureChanged; _vm = null; };
    }

    private void Attach()
    {
        var vm = (DataContext as MainWindowViewModel)?.Appearance;
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null) _vm.PropertyChanged -= Changed;
        I18nManager.Instance.CultureChanged -= CultureChanged;
        _vm = vm;
        if (vm is null) return;
        vm.PropertyChanged += Changed; I18nManager.Instance.CultureChanged += CultureChanged;
        if (DataContext is MainWindowViewModel shell)
        {
            shell.Workbench.AppearanceMenuRequested -= OnAppearanceMenuRequested;
            shell.Workbench.AppearanceMenuRequested += OnAppearanceMenuRequested;
        }
        Build();
    }

    /// <summary>新增/编辑用户配色：对话框确认后注册并应用（对应原型 openPaletteDialog）。</summary>
    private async void OpenPaletteDialog(Vex.Core.Models.PaletteOption? editing)
    {
        var definition = editing?.Definition;
        var colors = definition is null
            ? null
            : new[] { definition.Light.Paper, definition.Light.Surface, definition.Light.Ink, definition.Light.Accent,
                      definition.Dark.Paper, definition.Dark.Surface, definition.Dark.Ink, definition.Dark.Accent };
        var model = new ShellPaletteEditModel(editing?.Key, definition?.Name ?? "", definition?.Note ?? "", colors);
        var applied = await Ursa.Controls.OverlayDialog.ShowCustomAsync<bool>(new ShellPaletteEditView(model), model,
            options: new Ursa.Controls.OverlayDialogOptions { CanLightDismiss = true, CanDragMove = false, CanResize = false, IsCloseButtonVisible = false });
        if (!applied || model.Result is not { } result) return;
        var key = editing?.Key ?? "user:" + Guid.NewGuid().ToString("N")[..8];
        _vm.RegisterCustomPalette(key, result.Name, result.Note, result.Colors[0], result.Colors[1], result.Colors[2], result.Colors[3],
            result.Colors[4], result.Colors[5], result.Colors[6], result.Colors[7]);
    }

    /// <summary>弹出层开合状态回写给工作台，引导门控据此判定「外观已展开」。</summary>
    private void SetAppearanceMenuOpen(bool open)
    {
        if (DataContext is MainWindowViewModel shell) shell.Workbench.IsAppearanceMenuOpen = open;
    }

    /// <summary>引导「继续」代点开合外观弹出层。</summary>
    private void OnAppearanceMenuRequested(object? sender, EventArgs e)
    {
        var flyout = AppearanceButton.Flyout;
        if (flyout is null) return;
        if (flyout.IsOpen) flyout.Hide(); else flyout.ShowAt(AppearanceButton);
    }

    /// <summary>弹出层内容区，作为引导步骤的高亮目标。</summary>
    public Control MenuContent => _scroll ?? throw new InvalidOperationException("Appearance menu is not built yet");

    private void CultureChanged(object? sender, EventArgs e)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_vm is not null) Build(); });
            return;
        }
        Build();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        foreach (var (option, button) in _paletteButtons)
        {
            var selected = option == _vm.SelectedPalette;
            button.Classes.Set("selected-choice", selected);
            if (button.Content is Grid grid && grid.Children[^1] is TextBlock check) check.Text = selected ? "✓" : "";
            button.IsVisible = _all || option.IsRecommended || selected;
        }
        foreach (var (option, button) in _modeButtons) button.IsChecked = option == _vm.SelectedTheme;
        foreach (var (option, button) in _languageButtons)
            if (button.Content is Grid grid && grid.Children[^1] is TextBlock check) check.Text = option == _vm.SelectedLanguage ? "✓" : "";
        if (_recommended is not null) { _recommended.Classes.Set("choice-filter-selected", !_all); _recommended.Content = (!_all ? "✓ " : "") + Text("ChoiceRecommended"); }
        if (_allChoices is not null) { _allChoices.Classes.Set("choice-filter-selected", _all); _allChoices.Content = (_all ? "✓ " : "") + Text("AllPalettes"); }
        if (_selection is not null) _selection.Text = string.Format(Text("PaletteSelectionSummary"), _vm.SelectedPalette!.Name, _vm.PaletteOptions.Count(p => p.IsRecommended), _vm.PaletteOptions.Count);
    }

    private void Build()
    {
        if (_vm is null) return;
        if (_palettes.Parent is Panel previous) previous.Children.Remove(_palettes);
        _paletteButtons.Clear(); _modeButtons.Clear(); _languageButtons.Clear(); _palettes.Children.Clear();
        AppearanceButton = Button("", () => { });
        AppearanceButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children =
        {
            new PathIcon { Width = 15, Height = 15, Data = Geometry.Parse("M8 1a7 7 0 1 0 0 14h1a2 2 0 0 0 1-3.7.8.8 0 0 1 .4-1.5H12a3 3 0 0 0 3-3C15 3.5 12 1 8 1Z M4.5 6h.1 M7 3.5h.1 M10.5 4h.1 M12 6.5h.1") },
            Label(Text("Appearance"), 11)
        }};
        // 原型 .studio-title-controls：外观摘要为 56×32 / 11px / 左右 7px 的扁平按钮。
        AppearanceButton.Height = 32; AppearanceButton.MinHeight = 0; AppearanceButton.MinWidth = 0;
        AppearanceButton.Padding = new Thickness(7, 0); AppearanceButton.CornerRadius = new CornerRadius(5);
        AppearanceButton.FontSize = 11;
        AppearanceButton.Name = "TitleBarAppearanceButton";
        LanguageButton = Button("文ᴬ", () => { });
        LanguageButton.Width = 32; LanguageButton.Height = 32; LanguageButton.MinHeight = 0;
        LanguageButton.Padding = new Thickness(0); LanguageButton.CornerRadius = new CornerRadius(4);
        LanguageButton.FontSize = 12;
        LanguageButton.Name = "TitleBarLanguageButton";
        var modes = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 16) };
        foreach (var mode in _vm.ThemeOptions.OrderBy(m => m.Key == "light" ? 0 : m.Key == "dark" ? 1 : 2))
        {
            var button = new RadioButton { Content = Text(mode.Key switch { "light" => "ThemeLight", "dark" => "ThemeDark", _ => "ThemeSystem" }), GroupName = "TitleThemeMode", FontSize = 12, MinHeight = 32, Padding = new Thickness(4, 3), Margin = new Thickness(0, 0, 4, 0) };
            button.IsCheckedChanged += (_, _) => { if (button.IsChecked == true && _vm is not null) _vm.SelectedTheme = mode; };
            _modeButtons[mode] = button; modes.Children.Add(button);
        }
        var filters = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8) };
        _recommended = Button(Text("ChoiceRecommended"), () => { _all = false; Changed(null, new("")); });
        _allChoices = Button(Text("AllPalettes"), () => { _all = true; Changed(null, new("")); });
        _recommended.HorizontalAlignment = HorizontalAlignment.Stretch; _allChoices.HorizontalAlignment = HorizontalAlignment.Stretch;
        filters.Children.Add(_recommended); filters.Children.Add(_allChoices);
        filters.Margin = default;
        var filterFrame = new Border { Classes = { "choice-filter" }, Child = filters, Margin = new Thickness(0, 8) };
        foreach (var palette in _vm.PaletteOptions)
        {
            var labels = new StackPanel { Spacing = 2 };
            var name = Label(palette.Name, 13); name.FontWeight = FontWeight.SemiBold; labels.Children.Add(name);
            var purpose = Label(palette.Purpose); purpose.LineHeight = 16.8; purpose.Classes.Add("muted"); labels.Children.Add(purpose);
            var grid = new Grid { ColumnDefinitions = new("12,*,13"), ColumnSpacing = 9 };
            grid.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = palette.Swatch, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);
            var check = Label(""); check.VerticalAlignment = VerticalAlignment.Center; Resource(check, TextBlock.ForegroundProperty, this, "VexAccentBrush"); Grid.SetColumn(check, 2); grid.Children.Add(check);
            var button = Button("", () => _vm.SelectedPalette = palette);
            button.Content = grid; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Name = "Palette_" + palette.Key; button.MinHeight = 42; button.Padding = new Thickness(10, 4);
            _paletteButtons[palette] = button; _palettes.Children.Add(button);
        }
        // 用户配色卡带编辑/删除工具；目录尾部「＋ 新增配色」（对应原型 palette-card-tools / palette-add）。
        foreach (var palette in _vm.PaletteOptions.Where(option => option.Key.StartsWith("user:", StringComparison.Ordinal)).ToArray())
        {
            var button = _paletteButtons[palette];
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            var edit = new Button { Classes = { "wb-quiet" }, Padding = new Thickness(4, 1), MinHeight = 22, FontSize = 12, Content = "✎", Tag = palette };
            ToolTip.SetTip(edit, Text("EditCustomPalette"));
            edit.Click += (_, _) => OpenPaletteDialog(palette);
            var remove = new Button { Classes = { "wb-quiet" }, Padding = new Thickness(4, 1), MinHeight = 22, FontSize = 12, Content = "×", Tag = palette };
            ToolTip.SetTip(remove, Text("DeleteCustomPalette"));
            remove.Click += (_, _) => _vm.RemoveCustomPalette(palette.Key);
            tools.Children.Add(edit); tools.Children.Add(remove);
            if (button.Content is Grid grid)
            {
                grid.ColumnDefinitions = new("12,*,Auto");
                Grid.SetColumn(tools, 2);
                grid.Children.Add(tools);
            }
        }
        var addCard = Button("", () => OpenPaletteDialog(null));
        addCard.Name = "AddCustomPalette";
        addCard.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        addCard.HorizontalAlignment = HorizontalAlignment.Stretch;
        addCard.MinHeight = 38; addCard.Padding = new Thickness(10, 6);
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        addRow.Children.Add(new TextBlock { Text = "＋", FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var addLabels = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        addLabels.Children.Add(Label(Text("AddCustomPalette"), 13));
        addLabels.Children.Add(Label(Text("AddCustomPaletteNote")));
        addRow.Children.Add(addLabels);
        addCard.Content = addRow;
        _palettes.Children.Add(addCard);
        var content = new StackPanel { Width = MenuWidth - 34 };
        var modeTitle = Label(Text("AppearanceModeTitle")); modeTitle.FontWeight = FontWeight.SemiBold;
        var paletteTitle = Label(Text("WBTheme")); paletteTitle.FontWeight = FontWeight.SemiBold;
        content.Children.Add(modeTitle); content.Children.Add(modes); content.Children.Add(paletteTitle); content.Children.Add(filterFrame); content.Children.Add(_palettes);
        _selection = Label(""); _selection.Classes.Add("muted"); _selection.LineHeight = 18; _selection.Margin = new Thickness(0, 10, 0, 0); content.Children.Add(_selection);
        _scroll = new ScrollViewer { Content = content, MaxHeight = 646, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var appearanceFlyout = new Flyout { Content = _scroll, Placement = PlacementMode.BottomEdgeAlignedRight };
        appearanceFlyout.FlyoutPresenterClasses.Add("design-title-picker");
        appearanceFlyout.Opening += (_, _) => _scroll.MaxHeight = Math.Max(140, Math.Min(646, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 820) - 114));
        appearanceFlyout.Opened += (_, _) => SetAppearanceMenuOpen(true);
        appearanceFlyout.Closed += (_, _) => SetAppearanceMenuOpen(false);
        AppearanceButton.Flyout = appearanceFlyout;
        var languages = new StackPanel { Width = MenuWidth - 12 };
        foreach (var language in _vm.LanguageOptions)
        {
            var grid = new Grid { ColumnDefinitions = new("*,Auto") };
            grid.Children.Add(Label(language.CultureName switch { "zh-CN" => "简体中文", "zh-Hant" => "繁體中文", "ja-JP" => "日本語", _ => "English" }));
            var check = Label(""); Resource(check, TextBlock.ForegroundProperty, this, "VexAccentBrush"); Grid.SetColumn(check, 1); grid.Children.Add(check);
            var button = Button("", () => { LanguageButton.Flyout?.Hide(); _vm.SelectedLanguage = language; });
            button.Content = grid; button.Height = 30; button.Padding = new Thickness(10, 6);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.HorizontalAlignment = HorizontalAlignment.Stretch; _languageButtons[language] = button; languages.Children.Add(button);
        }
        Navigation(languages, () => 1);
        var languageFlyout = new Flyout { Content = languages, Placement = PlacementMode.BottomEdgeAlignedRight };
        languageFlyout.FlyoutPresenterClasses.Add("design-language-picker"); LanguageButton.Flyout = languageFlyout;
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { AppearanceButton, LanguageButton } };
        Changed(null, new(""));
    }
}
