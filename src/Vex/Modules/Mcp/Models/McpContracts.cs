using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vex.Modules.Mcp.Models;

public sealed record JsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string? JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("method")] string? Method,
    [property: JsonPropertyName("params")] JsonElement? Params);

public sealed record JsonRpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("result")] JsonElement? Result = null,
    [property: JsonPropertyName("error")] JsonRpcError? Error = null);

public sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record McpInitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("serverInfo")] McpServerInfo ServerInfo,
    [property: JsonPropertyName("capabilities")] McpCapabilities Capabilities);

public sealed record McpServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);

public sealed record McpCapabilities(
    [property: JsonPropertyName("tools")] McpToolsCapability Tools);

public sealed record McpToolsCapability(
    [property: JsonPropertyName("listChanged")] bool ListChanged);

public sealed record McpToolsListResult(
    [property: JsonPropertyName("tools")] IReadOnlyList<McpToolDescription> Tools);

public sealed record McpToolDescription(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);

public sealed record McpToolCallParams(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("arguments")] JsonElement? Arguments);

public sealed record McpToolCallResult(
    [property: JsonPropertyName("content")] IReadOnlyList<McpContentItem> Content,
    [property: JsonPropertyName("isError")] bool IsError = false);

public sealed record McpContentItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

public sealed record EmptyInput;

public sealed record ReplaceCurrentDocumentInput(string Markdown, string? Reason);

public sealed record ApplyTextEditInput(int StartOffset, int Length, string Replacement, string? Reason);

public sealed record InsertTextInput(int? Offset, string Text);

public sealed record ReplaceSelectionInput(string Text, string? Reason);

public sealed record OpenDocumentInput(string Path, string? EncodingName);

public sealed record UiSetThemeInput(string ThemeKey);

public sealed record UiSetTypographyInput(string TypographyKey);

public sealed record UiSetLanguageInput(string CultureName);

public sealed record UiSetLayoutInput(bool? SidebarVisible, bool? StatusBarVisible, bool? PreviewVisible, bool? SourceMode, bool? LineNumbersVisible, bool? CompactLayout);

public sealed record UiShowSidebarTabInput(string Tab);

public sealed record UiOpenPanelInput(string Panel);

public sealed record UiApplyEditorCommandInput(string Command);

public sealed record ExportCurrentDocumentInput(string Format);

public sealed record CopyRenderedHtmlInput(string Target);

public sealed record CurrentDocumentResult(string? FilePath, string FileName, string Markdown, bool IsDirty, string Encoding);

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
    bool ShowLineNumbers,
    bool IsStatusBarVisible);

public sealed record RenderedHtmlResult(string Html);

public sealed record OperationResult(string Status, string? Detail = null);

public sealed record OperationAuditResult(IReadOnlyList<McpOperationRecord> Items);
