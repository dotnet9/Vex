using CodeWF.Avalonia.Controls.Themes;

namespace Vex.Core.Models;

public sealed record ThemeOption(string Key, StudioThemeMode Mode);

public sealed record PaletteOption(string Key, StudioPalette Palette);
