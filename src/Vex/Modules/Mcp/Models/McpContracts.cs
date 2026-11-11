using System.Text.Json.Serialization;

namespace Vex.Modules.Mcp.Models;

public sealed record EmptyInput;

public sealed record ReplaceCurrentDocumentInput(string Markdown, string? Reason);

public sealed record ReplaceTextInput(string Find, string Replacement, bool ReplaceAll);

public sealed record CreateDocumentInput(string Path, string Content, bool Overwrite);

public sealed record ResourceReadParams(string Uri);

public sealed record ApplyTextEditInput(int StartOffset, int Length, string Replacement, string? Reason);

public sealed record InsertTextInput(int? Offset, string Text);

public sealed record ReplaceSelectionInput(string Text, string? Reason);

public sealed record OpenDocumentInput(string Path, string? EncodingName);

public sealed record UiSetThemeInput(string ThemeKey);

public sealed record UiSetTypographyInput(string TypographyKey);

public sealed record UiSetLanguageInput(string CultureName);

public sealed record UiSetLayoutInput(bool? SidebarVisible, bool? PreviewVisible, bool? SourceMode, bool? LineNumbersVisible, bool? CompactLayout);

public sealed record UiShowSidebarTabInput(string Tab);

public sealed record UiOpenPanelInput(string Panel);

public sealed record UiApplyEditorCommandInput(string Command);

public sealed record ExportCurrentDocumentInput(string Format);

public sealed record CopyRenderedHtmlInput(string Target);

public sealed record CurrentDocumentResult(string? FilePath, string FileName, string Markdown, bool IsDirty, string Encoding, long Version);

public sealed record ListFilesResult(string? Root, IReadOnlyList<FileEntryResult> Files);

public sealed record FileEntryResult(string Path, string Name, bool IsCurrent);

public sealed record ResourceListResult(IReadOnlyList<ResourceEntryResult> Resources);

public sealed record ResourceEntryResult(string Uri, string Name, string MimeType, string? Description);

public sealed record ResourceReadResult(string Uri, string MimeType, string Text);

public sealed record OutlineResult(IReadOnlyList<OutlineItemResult> Items);

public sealed record OutlineItemResult(int Level, string Title, int Line);

public sealed record SelectionResult(string Text, int StartOffset, int Length);

public sealed record AppStatusResult(string Version, string? Theme, string? Typography, string? CurrentFilePath, bool IsDirty);

public sealed record UiStateResult(
    string? Theme,
    string? Typography,
    string? CultureName,
    bool IsCompactLayout,
    bool IsSidebarVisible,
    string SelectedSidebarTab,
    bool IsPreviewVisible,
    bool IsSourceMode,
    bool ShowLineNumbers);

public sealed record RenderedHtmlResult(string Html);

public sealed record OperationResult(string Status, string? Detail = null);

public sealed record OperationAuditResult(IReadOnlyList<McpOperationRecord> Items);
