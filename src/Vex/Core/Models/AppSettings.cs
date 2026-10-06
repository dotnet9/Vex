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

    public bool? IsSourceMode { get; init; }

    public bool? IsAlwaysOnTop { get; init; }

    public int? SelectedSidebarTabIndex { get; init; }

    public double? EditorZoom { get; init; }

    public bool? ShowLineNumbers { get; init; }

    /// <summary>自动配对输入（成对符号插入 / 选区包裹 / 空配对退格删除）。</summary>
    public bool? EnableAutoPair { get; init; }

    public bool? HasSeenOnboardingGuide { get; init; }

    public string? LastWorkspaceFolderPath { get; init; }

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }

    public bool? IsMcpServerEnabled { get; init; }

    public string? McpServerHost { get; init; }

    public int? McpServerPort { get; init; }

    public string? McpAuthorizationToken { get; init; }

    public string? McpAccessScope { get; init; }

    public string? McpAllowedWorkspacePath { get; init; }

    public bool? McpRequireConfirmation { get; init; }

    /// <summary>界面动效总开关（关闭后所有过渡时长归零）。</summary>
    public bool? EnableMotion { get; init; }

    /// <summary>专注模式：隐藏侧栏与状态栏。</summary>
    public bool? IsFocusMode { get; init; }

    /// <summary>打字机模式：光标行垂直居中。</summary>
    public bool? IsTypewriterMode { get; init; }
}
