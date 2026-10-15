using CodeWF.Avalonia.Controls.Themes;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vex.Core.Models;

public sealed record ThemeOption(string Key, StudioThemeMode Mode);

public sealed record PaletteOption(string Key, StudioPalette Palette)
{
    public string NameKey => Palette switch
    {
        StudioPalette.DotNet => VexL.ThemeDotNet,
        StudioPalette.Aurora => VexL.ThemeAurora,
        StudioPalette.Ember => VexL.ThemeEmber,
        StudioPalette.Default => VexL.ThemeDefault,
        StudioPalette.Aquatic => VexL.ThemeAquatic,
        StudioPalette.Desert => VexL.ThemeDesert,
        StudioPalette.Dusk => VexL.ThemeDusk,
        _ => VexL.ThemeNightSky,
    };

    public IBrush Swatch => global::Avalonia.Application.Current is { } app
        && app.TryFindResource($"StudioSwatch{Palette}", out var resource) && resource is IBrush brush
            ? brush : Brushes.Transparent;
}
