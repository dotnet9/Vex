namespace Vex.Core.Models;

public sealed record AppSettings
{
    public string? WorkbenchView { get; init; }
    public string? WorkspaceLayout { get; init; }
    public string? EditMode { get; init; }
    public int? MindMapSummaryLength { get; init; }
    public bool? MindMapShowParagraphs { get; init; }
    public bool? MindMapShowCode { get; init; }
    public bool? MindMapShowTables { get; init; }
    public bool? MindMapShowLists { get; init; }
    public string? ThemeKey { get; init; }

    /// <summary>配色独立于浅色、深色与跟随系统模式。</summary>
    public string? PaletteKey { get; init; }

    public string? TypographyKey { get; init; }

    public bool? IsCompactLayout { get; init; }

    public string? CultureName { get; init; }

    public bool? IsSidebarVisible { get; init; }

    /// <summary>侧栏宽度（可拖分隔条调整并记忆）。</summary>
    public double? SidebarWidth { get; init; }

    public bool? IsOutlineVisible { get; init; }
    public double? OutlineWidth { get; init; }
    public string? AuxiliaryTab { get; init; }
    public bool? FileListView { get; init; }
    public bool? FileSortDescending { get; init; }
    public string? CollapsedFolders { get; init; }
    public string? CollapsedOutlineSections { get; init; }

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

    // 统一偏好设置；空值沿用升级前默认行为。
    public bool? RestoreWorkspace { get; init; }
    public bool? AutoSave { get; init; }
    public int? AutoSaveDelay { get; init; }
    public bool? RecoveryDrafts { get; init; }
    public string? DefaultEncoding { get; init; }
    public string? DefaultNewline { get; init; }
    public double? BodyFontSize { get; init; }
    public double? CodeFontSize { get; init; }
    public int? IndentSize { get; init; }
    public bool? CodeWrap { get; init; }
    public string? BodyFont { get; init; }
    public bool? SyntaxMath { get; init; }
    public bool? SyntaxChemistry { get; init; }
    public bool? SyntaxDiagrams { get; init; }
    public bool? SyntaxSubscript { get; init; }
    public bool? SyntaxHighlight { get; init; }
    public bool? SyntaxAutoLinks { get; init; }
    public bool? SyntaxSoftBreaks { get; init; }
    public bool? ImageCopy { get; init; }
    public bool? ImageRelative { get; init; }
    public string? ImageFolder { get; init; }
    public bool? WordCountVisible { get; init; }
    public double? ContentZoom { get; init; }
    public string? ExportFormat { get; init; }
    public string? ExportFolder { get; init; }
    public bool? ExportOpen { get; init; }
    public bool? HtmlStyle { get; init; }
    public bool? WatermarkPreview { get; init; }
    public bool? WatermarkExport { get; init; }
    public string? WatermarkText { get; init; }
    public double? WatermarkOpacity { get; init; }
    public string? PdfSize { get; init; }
    public double? PdfMargin { get; init; }
}
