using Avalonia;
using Avalonia.Styling;
using Semi.Avalonia;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Appearance.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly ThemeVariant Aquatic = new(nameof(SemiTheme.Aquatic), ThemeVariant.Light);
    private static readonly ThemeVariant Dusk = new(nameof(SemiTheme.Dusk), ThemeVariant.Light);

    private static readonly ThemeOption[] Themes =
    [
        new("System", "system", ThemeVariant.Default),
        new("Light", "light", ThemeVariant.Light),
        new("Dark", "dark", ThemeVariant.Dark),
        new("Aquatic", "aquatic", Aquatic),
        new("Desert", "desert", SemiTheme.Desert),
        new("Dusk", "dusk", Dusk),
        new("NightSky", "night-sky", SemiTheme.NightSky)
    ];

    public IReadOnlyList<ThemeOption> GetThemeOptions() => Themes;

    public void ApplyTheme(ThemeOption theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme.ThemeVariant;
        }
    }
}
