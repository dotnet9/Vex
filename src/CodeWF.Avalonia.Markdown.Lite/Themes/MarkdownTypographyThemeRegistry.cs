using global::Avalonia.Controls;

using CodeWF.Avalonia.Markdown.Themes;

namespace CodeWF.Avalonia.Markdown.Themes;

/// <summary>
/// Registry for built-in and application-defined Markdown typography themes.
/// </summary>
public static class MarkdownTypographyThemeRegistry
{
    private static readonly object SyncRoot = new();

    private static readonly Dictionary<string, Func<ResourceDictionary>> ThemeResourceFactories =
        new(StringComparer.OrdinalIgnoreCase);

    static MarkdownTypographyThemeRegistry()
    {
        Register(MarkdownTypographyThemes.Basic, static () => new BasicTypographyResources());
        Register(MarkdownTypographyThemes.OrangeHeart, static () => new OrangeHeartTypographyResources());
        Register(MarkdownTypographyThemes.InkBlack, static () => new InkBlackTypographyResources());
        Register(MarkdownTypographyThemes.ColorfulPurple, static () => new ColorfulPurpleTypographyResources());
        Register(MarkdownTypographyThemes.TenderGreen, static () => new TenderGreenTypographyResources());
        Register(MarkdownTypographyThemes.RedScarlet, static () => new RedScarletTypographyResources());
        Register(MarkdownTypographyThemes.TechnologyBlue, static () => new TechnologyBlueTypographyResources());
        Register(MarkdownTypographyThemes.LanQing, static () => new LanQingTypographyResources());
        Register(MarkdownTypographyThemes.Yamabuki, static () => new YamabukiTypographyResources());
        Register(MarkdownTypographyThemes.GeekBlack, static () => new GeekBlackTypographyResources());
        Register(MarkdownTypographyThemes.Simple, static () => new SimpleTypographyResources());
        Register(MarkdownTypographyThemes.WeChatFormat, static () => new WeChatFormatTypographyResources());
        foreach (var typography in ArticleTypographyCatalog.All)
            Register(typography.Key, typography.CreateResources);
    }

    public static IReadOnlyList<string> ThemeNames
    {
        get
        {
            lock (SyncRoot)
            {
                return ThemeResourceFactories.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
    }

    public static void Register(string themeName, Func<ResourceDictionary> resourceFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeName);
        ArgumentNullException.ThrowIfNull(resourceFactory);

        lock (SyncRoot)
        {
            ThemeResourceFactories[themeName.Trim()] = resourceFactory;
        }
    }

    public static bool Contains(string? themeName)
    {
        return !string.IsNullOrWhiteSpace(themeName)
               && ContainsCore(themeName.Trim());
    }

    public static string Normalize(string? themeName)
    {
        return !string.IsNullOrWhiteSpace(themeName)
               && ContainsCore(themeName.Trim())
            ? themeName.Trim()
            : MarkdownTypographyThemes.Basic;
    }

    public static bool TryCreate(string? themeName, out ResourceDictionary resources)
    {
        var factory = GetFactory(themeName);
        if (factory is null)
        {
            resources = null!;
            return false;
        }

        resources = factory();
        return true;
    }

    public static ResourceDictionary Create(string? themeName)
    {
        return TryCreate(themeName, out var resources)
            ? resources
            : Create(MarkdownTypographyThemes.Basic);
    }

    private static bool ContainsCore(string themeName)
    {
        lock (SyncRoot)
        {
            return ThemeResourceFactories.ContainsKey(themeName);
        }
    }

    private static Func<ResourceDictionary>? GetFactory(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return null;
        }

        lock (SyncRoot)
        {
            return ThemeResourceFactories.TryGetValue(themeName.Trim(), out var factory)
                ? factory
                : null;
        }
    }
}
