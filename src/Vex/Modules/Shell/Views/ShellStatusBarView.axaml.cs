using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeWF.Avalonia.Lang;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellStatusBarView : UserControl
{
    private void AppearanceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ShellStatusBarViewModel vm) return;
        var flyout = new MenuFlyout();
        foreach (var option in vm.Appearance.ThemeOptions)
        {
            var key = option.Key switch { "light" => VexL.ThemeLight, "dark" => VexL.ThemeDark, _ => VexL.ThemeSystem };
            var item = new MenuItem { Header = I18nManager.Instance.GetResource(key), ToggleType = MenuItemToggleType.Radio, IsChecked = option == vm.Appearance.SelectedTheme };
            item.Click += (_, _) => vm.Appearance.SelectedTheme = option; flyout.Items.Add(item);
        }
        flyout.Items.Add(new Separator());
        foreach (var palette in vm.Appearance.PaletteOptions)
        {
            var key = palette.Key switch { "dotnet" => VexL.ThemeDotNet, "aurora" => VexL.ThemeAurora, "ember" => VexL.ThemeEmber, "aquatic" => VexL.ThemeAquatic, "desert" => VexL.ThemeDesert, "dusk" => VexL.ThemeDusk, "night-sky" => VexL.ThemeNightSky, _ => VexL.ThemeDefault };
            var item = new MenuItem { Header = I18nManager.Instance.GetResource(key) ?? palette.Key, ToggleType = MenuItemToggleType.Radio, IsChecked = palette == vm.Appearance.SelectedPalette };
            item.Click += (_, _) => vm.Appearance.SelectedPalette = palette; flyout.Items.Add(item);
        }
        flyout.ShowAt(button);
    }
    public ShellStatusBarView()
    {
        InitializeComponent();
    }
}
