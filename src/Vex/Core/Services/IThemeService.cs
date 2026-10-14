using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IThemeService
{
    IReadOnlyList<ThemeOption> GetThemeOptions();

    IReadOnlyList<PaletteOption> GetPaletteOptions();

    void ApplyTheme(ThemeOption theme, PaletteOption palette);
}
