using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IThemeService
{
    IReadOnlyList<ThemeOption> GetThemeOptions();

    IReadOnlyList<PaletteOption> GetPaletteOptions();

    void ApplyTheme(ThemeOption theme, PaletteOption palette);

    /// <summary>注册（新增或编辑）用户自定义配色，返回配色 Key（user: 前缀）。</summary>
    string RegisterCustomPalette(string key, string name, string note,
        string lightPaper, string lightSurface, string lightInk, string lightAccent,
        string darkPaper, string darkSurface, string darkInk, string darkAccent);

    /// <summary>删除用户自定义配色（仅 user: 前缀可删）。</summary>
    bool RemoveCustomPalette(string key);

    /// <summary>配色目录变化后失效缓存，下一次 GetPaletteOptions 重建。</summary>
    void InvalidatePaletteOptions();
}
