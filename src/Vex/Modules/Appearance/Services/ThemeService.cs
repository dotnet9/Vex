using System.Linq;
using Avalonia;
using CodeWF.Avalonia.Controls.Themes;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Appearance.Services;

/// <summary>应用只保存选项，配色由字符串 Key 注册表（规则 §13）与派生引擎维护；用户配色随设置持久化。</summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private StudioThemeController? _controller;
    private PaletteOption[]? _palettes;
    private readonly IShellToastPublisher? _toastPublisher;
    private readonly IAppSettingsStore? _settings;
    private static readonly ThemeOption[] Modes =
    [
        new("system", StudioThemeMode.System),
        new("light", StudioThemeMode.Light),
        new("dark", StudioThemeMode.Dark),
    ];

    public ThemeService(IAppSettingsStore settings, Modules.Shell.Services.IShellToastPublisher? toastPublisher = null)
    {
        _settings = settings;
        _toastPublisher = toastPublisher;
    }

    private StudioThemeController Controller => _controller ??= new StudioThemeController(Application.Current ?? throw new InvalidOperationException("主题控制器需要在应用启动后使用。"));

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
        if (palette.Key.StartsWith("user:", StringComparison.Ordinal)) AuditCustomPalette(palette, theme.Mode == StudioThemeMode.Dark);
    }

    /// <summary>明暗切换后复核用户自定义色板对比度（规则 §5），不达标经 Toast 提示。</summary>
    private void AuditCustomPalette(PaletteOption palette, bool dark)
    {
        if (_toastPublisher is null) return;
        var baseColors = dark ? palette.Definition.Dark : palette.Definition.Light;
        var paper = Avalonia.Media.Color.Parse(baseColors.Paper);
        var accent = Avalonia.Media.Color.Parse(baseColors.Accent);
        var ink = Avalonia.Media.Color.Parse(baseColors.Ink);
        var muted = CodeWF.Avalonia.Controls.Themes.StudioPaletteEngine.Derive(palette.Key, palette.Definition, dark)["StudioMutedColor"];
        var issues = new System.Collections.Generic.List<string>();
        if (CodeWF.Avalonia.Controls.Themes.StudioPaletteEngine.ContrastRatio(ink, paper) < 10) issues.Add("正文对比度不足");
        if (CodeWF.Avalonia.Controls.Themes.StudioPaletteEngine.ContrastRatio(muted, paper) < 4.5) issues.Add("次要文字对比度不足");
        if (CodeWF.Avalonia.Controls.Themes.StudioPaletteEngine.ContrastRatio(accent, paper) < 4.5) issues.Add("强调色对比度不足");
        if (issues.Count > 0)
        {
            _toastPublisher.Publish($"自定义配色「{palette.Name}」({(dark ? "深色" : "浅色")})：{string.Join("；", issues)}",
                Avalonia.Controls.Notifications.NotificationType.Warning);
        }
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
