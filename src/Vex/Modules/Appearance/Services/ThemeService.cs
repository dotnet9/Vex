using System.Linq;
using Avalonia;
using CodeWF.Avalonia.Controls.Themes;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Appearance.Services;

/// <summary>应用只保存选项，配色由字符串 Key 注册表（规则 §13）与派生引擎维护。</summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private StudioThemeController? _controller;
    private PaletteOption[]? _palettes;
    private static readonly ThemeOption[] Modes =
    [
        new("system", StudioThemeMode.System),
        new("light", StudioThemeMode.Light),
        new("dark", StudioThemeMode.Dark),
    ];

    private StudioThemeController Controller => _controller ??= new StudioThemeController(Application.Current);

    public IReadOnlyList<ThemeOption> GetThemeOptions() => Modes;

    public IReadOnlyList<PaletteOption> GetPaletteOptions()
    {
        if (_palettes is not null) return _palettes;
        _palettes = Controller.Registry.List()
            .Select(entry =>
            {
                var definition = Controller.Registry.Find(entry.Key)!;
                return new PaletteOption(
                    entry.Key, definition.Name, definition.Note, definition.Recommended,
                    Controller.GetSwatch(entry.Key), definition);
            })
            .ToArray();
        return _palettes;
    }

    public void ApplyTheme(ThemeOption theme, PaletteOption palette)
    {
        var controller = Controller;
        controller.PaletteKey = palette.Key;
        controller.Mode = theme.Mode;
    }

    public void Dispose() => _controller?.Dispose();
}
