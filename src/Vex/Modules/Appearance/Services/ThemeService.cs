using System.Linq;
using Avalonia;
using CodeWF.Avalonia.Controls.Themes;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Appearance.Services;

/// <summary>应用只保存选项，配色由字符串 Key 注册表（规则 §13）与派生引擎维护；用户配色随设置持久化。</summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private StudioThemeController? _controller;
    private PaletteOption[]? _palettes;
    private readonly IAppSettingsStore? _settings;
    private static readonly ThemeOption[] Modes =
    [
        new("system", StudioThemeMode.System),
        new("light", StudioThemeMode.Light),
        new("dark", StudioThemeMode.Dark),
    ];

    public ThemeService(IAppSettingsStore settings) => _settings = settings;

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

    public string RegisterCustomPalette(string key, string name, string note,
        string lightPaper, string lightSurface, string lightInk, string lightAccent,
        string darkPaper, string darkSurface, string darkInk, string darkAccent)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("user:", StringComparison.Ordinal))
            key = "user:" + Guid.NewGuid().ToString("N")[..8];
        key = Controller.Registry.Register(new StudioPaletteRegistry.UserPalette(key, name, note,
            lightPaper, lightSurface, lightInk, lightAccent, darkPaper, darkSurface, darkInk, darkAccent));
        InvalidatePaletteOptions();
        PersistCustomPalettes();
        return key;
    }

    public bool RemoveCustomPalette(string key)
    {
        if (!Controller.Registry.Remove(key)) return false;
        InvalidatePaletteOptions();
        PersistCustomPalettes();
        return true;
    }

    public void InvalidatePaletteOptions() => _palettes = null;

    /// <summary>启动时恢复持久化的用户配色（竖线分字段的紧凑记录，避免 AOT 反射序列化）。</summary>
    public void LoadCustomPalettes(string? persisted)
    {
        if (string.IsNullOrWhiteSpace(persisted)) return;
        foreach (var record in persisted.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = record.Split('|');
            if (fields.Length != 11 || !fields[0].StartsWith("user:", StringComparison.Ordinal)) continue;
            try
            {
                Controller.Registry.Register(new StudioPaletteRegistry.UserPalette(fields[0], fields[1], fields[2],
                    fields[3], fields[4], fields[5], fields[6], fields[7], fields[8], fields[9], fields[10]));
            }
            catch { /* 单条损坏不阻塞其余配色 */ }
        }
    }

    private void PersistCustomPalettes()
    {
        if (_settings is null) return;
        var records = Controller.Registry.List().Where(entry => entry.IsUser).Select(entry =>
        {
            var definition = Controller.Registry.Find(entry.Key)!;
            return string.Join('|', definition.Key, definition.Name, definition.Note,
                definition.Light.Paper, definition.Light.Surface, definition.Light.Ink, definition.Light.Accent,
                definition.Dark.Paper, definition.Dark.Surface, definition.Dark.Ink, definition.Dark.Accent);
        });
        _settings.Update(settings => settings with { CustomPalettes = string.Join(';', records) });
    }

    public void Dispose() => _controller?.Dispose();
}
