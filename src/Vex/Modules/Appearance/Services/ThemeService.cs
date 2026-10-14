using Avalonia;
using CodeWF.Avalonia.Controls.Themes;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Appearance.Services;

/// <summary>应用只保存选项，配色与系统明暗变化统一交给控件库。</summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private StudioThemeController? _controller;
    private static readonly ThemeOption[] Modes =
    [
        new("system", StudioThemeMode.System),
        new("light", StudioThemeMode.Light),
        new("dark", StudioThemeMode.Dark),
    ];
    private static readonly PaletteOption[] Palettes =
    [
        new("dotnet", StudioPalette.DotNet), new("aurora", StudioPalette.Aurora),
        new("ember", StudioPalette.Ember), new("default", StudioPalette.Default),
        new("aquatic", StudioPalette.Aquatic), new("desert", StudioPalette.Desert),
        new("dusk", StudioPalette.Dusk), new("night-sky", StudioPalette.NightSky),
    ];

    public IReadOnlyList<ThemeOption> GetThemeOptions() => Modes;
    public IReadOnlyList<PaletteOption> GetPaletteOptions() => Palettes;

    public void ApplyTheme(ThemeOption theme, PaletteOption palette)
    {
        if (Application.Current is not { } app) return;
        _controller ??= new StudioThemeController(app);
        _controller.Palette = palette.Palette;
        _controller.Mode = theme.Mode;
    }

    public void Dispose() => _controller?.Dispose();
}
