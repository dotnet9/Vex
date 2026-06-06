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

    public bool? HasSeenOnboardingGuide { get; init; }

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }

    public bool? IsMcpServerEnabled { get; init; }

    public string? McpServerHost { get; init; }

    public int? McpServerPort { get; init; }

    public string? McpAuthorizationToken { get; init; }

    public string? McpAccessScope { get; init; }

    public string? McpAllowedWorkspacePath { get; init; }

    public bool? McpRequireConfirmation { get; init; }
}
