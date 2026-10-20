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
    private readonly Dictionary<ThemeOption, Button> _modeButtons = [];
    private readonly Dictionary<LanguageOption, Button> _languageButtons = [];
    public Button AppearanceButton { get; private set; } = null!;
    public Button LanguageButton { get; private set; } = null!;
    public const double MenuWidth = 248;

    public ShellAppearanceMenuView()
    {
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
        Build();
    }

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
        foreach (var (option, button) in _modeButtons) button.Classes.Set("selected-choice", option == _vm.SelectedTheme);
        foreach (var (option, button) in _languageButtons) button.Content = option.NativeName + (option == _vm.SelectedLanguage ? "  ✓" : "");
    }

    private void Build()
    {
        if (_vm is null) return;
        if (_palettes.Parent is Panel previous) previous.Children.Remove(_palettes);
        _paletteButtons.Clear(); _modeButtons.Clear(); _languageButtons.Clear(); _palettes.Children.Clear();
        AppearanceButton = Button("", () => { });
        AppearanceButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children =
        {
            new PathIcon { Width = 14, Height = 14, Data = Geometry.Parse("M8 1a7 7 0 1 0 0 14h1a2 2 0 0 0 1-3.7.8.8 0 0 1 .4-1.5H12a3 3 0 0 0 3-3C15 3.5 12 1 8 1Z M4.5 6h.1 M7 3.5h.1 M10.5 4h.1 M12 6.5h.1") },
            Label(Text("Appearance"), 12)
        }};
        AppearanceButton.Name = "TitleBarAppearanceButton";
        LanguageButton = Button("文ᴬ", () => { }); LanguageButton.Name = "TitleBarLanguageButton";
        var modes = new UniformGrid { Columns = 3 };
        foreach (var mode in _vm.ThemeOptions)
        {
            var button = Button(Text(mode.Key switch { "light" => "ThemeLight", "dark" => "ThemeDark", _ => "ThemeSystem" }), () => _vm.SelectedTheme = mode);
            _modeButtons[mode] = button; modes.Children.Add(button);
        }
        var filters = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8) };
        var recommended = Button(Text("ChoiceRecommended"), () => { _all = false; Changed(null, new("")); });
        var all = Button(Text("AllPalettes"), () => { _all = true; Changed(null, new("")); });
        filters.Children.Add(recommended); filters.Children.Add(all);
        foreach (var palette in _vm.PaletteOptions)
        {
            var labels = new StackPanel { Spacing = 2 };
            labels.Children.Add(Label(Text(palette.NameKey), 13));
            var purpose = Label(Text(palette.PurposeKey)); purpose.Classes.Add("muted"); labels.Children.Add(purpose);
            var grid = new Grid { ColumnDefinitions = new("16,*,18"), ColumnSpacing = 8 };
            grid.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = palette.Swatch, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);
            var check = Label(""); Grid.SetColumn(check, 2); grid.Children.Add(check);
            var button = Button("", () => _vm.SelectedPalette = palette);
            button.Content = grid; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Name = "Palette_" + palette.Palette; button.MinHeight = 46;
            _paletteButtons[palette] = button; _palettes.Children.Add(button);
        }
        Navigation(_palettes, () => 1);
        var content = new StackPanel { Spacing = 6, Margin = new Thickness(8) };
        content.Children.Add(Label(Text("Appearance"), 13)); content.Children.Add(modes); content.Children.Add(filters); content.Children.Add(_palettes);
        var scroll = new ScrollViewer { Content = content, MaxHeight = Math.Max(180, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 820) - 80), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AppearanceButton.Flyout = new Flyout { Content = new Border { Width = MenuWidth - 24, Child = scroll }, Placement = PlacementMode.BottomEdgeAlignedRight };
        var languages = new StackPanel { Width = MenuWidth - 24, Spacing = 4 };
        foreach (var language in _vm.LanguageOptions)
        {
            var button = Button(language.NativeName, () => { LanguageButton.Flyout?.Hide(); _vm.SelectedLanguage = language; });
            button.HorizontalAlignment = HorizontalAlignment.Stretch; _languageButtons[language] = button; languages.Children.Add(button);
        }
        Navigation(languages, () => 1);
        LanguageButton.Flyout = new Flyout { Content = languages, Placement = PlacementMode.BottomEdgeAlignedRight };
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { AppearanceButton, LanguageButton } };
        Changed(null, new(""));
    }
}
