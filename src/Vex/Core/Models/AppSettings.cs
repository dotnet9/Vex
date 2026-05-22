namespace Vex.Core.Models;

public sealed record AppSettings
{
    public string? ThemeKey { get; init; }

    public string? TypographyKey { get; init; }

    public bool? IsCompactLayout { get; init; }

    public string? CultureName { get; init; }

    public bool? IsSidebarVisible { get; init; }

    public bool? IsStatusBarVisible { get; init; }

    public bool? IsPreviewVisible { get; init; }

    public bool? IsAlwaysOnTop { get; init; }

    public int? SelectedSidebarTabIndex { get; init; }

    public double? EditorZoom { get; init; }

    public bool? ShowLineNumbers { get; init; }

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }
}
