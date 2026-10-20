using CodeWF.Avalonia.Controls.Themes;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vex.Core.Models;

public sealed record ThemeOption(string Key, StudioThemeMode Mode);

public sealed record PaletteOption(string Key, StudioPalette Palette)
{
    public StudioPaletteInfo Definition => StudioPaletteCatalog.All.First(item => item.Palette == Palette);
    public string NameKey => "Vex.VexL.Palette" + Palette;
    public string PurposeKey => "Vex.VexL.PalettePurpose" + Palette;
    public bool IsRecommended => Definition.Recommended;

    public IBrush Swatch => global::Avalonia.Application.Current is { } app
        && app.TryFindResource($"StudioSwatch{Palette}", out var resource) && resource is IBrush brush
            ? brush : Brushes.Transparent;
}
