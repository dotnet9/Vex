using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using CodeWF.EventBus;
using Markdig;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Serialization;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Mcp.Services;

public sealed class McpToolDispatcher : IMcpToolDispatcher
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private static readonly McpToolDescription[] Tools =
    [
        Tool("vex_get_current_document", "读取当前文档。"),
        Tool("vex_get_document_outline", "读取当前文档大纲。"),
        Tool("vex_get_selection", "读取当前选区。"),
        Tool("vex_replace_current_document", "整体替换当前 Markdown。", ReplaceDocumentSchema),
        Tool("vex_apply_text_edit", "按 offset 应用文本编辑。", ApplyTextEditSchema),
        Tool("vex_insert_text", "插入文本。", InsertTextSchema),
        Tool("vex_replace_selection", "替换当前选区。", ReplaceSelectionSchema),
        Tool("vex_open_document", "打开授权范围内的文档。", OpenDocumentSchema),
        Tool("vex_save_current_document", "保存当前文档。"),
        Tool("vex_refresh_preview", "刷新预览。"),
        Tool("vex_get_rendered_html", "返回当前 Markdown 渲染后的 HTML。"),
        Tool("vex_get_app_status", "读取应用状态。"),
        Tool("vex_get_operation_audit", "读取最近 MCP 操作审计记录。"),
        Tool("vex_ui_get_state", "读取界面状态。"),
        Tool("vex_ui_set_theme", "设置主题色。", UiSetThemeSchema),
        Tool("vex_ui_set_typography", "设置 Markdown 排版主题。", UiSetTypographySchema),
        Tool("vex_ui_set_language", "设置界面语言。", UiSetLanguageSchema),
        Tool("vex_ui_set_layout", "设置基础布局状态。", UiSetLayoutSchema),
        Tool("vex_ui_show_sidebar_tab", "切换侧边栏页签。", UiShowSidebarTabSchema),
        Tool("vex_ui_open_panel", "打开基础面板。", UiOpenPanelSchema),
        Tool("vex_ui_refresh_preview", "刷新当前预览。"),
        Tool("vex_ui_apply_editor_command", "执行常用编辑命令。", UiApplyEditorCommandSchema),
        Tool("vex_ui_export_current_document", "导出当前文档，输出位置仍由用户选择。", ExportCurrentDocumentSchema),
        Tool("vex_ui_copy_rendered_html", "复制面向平台的富 HTML。", CopyRenderedHtmlSchema)
    ];

    private const string EmptySchema = """{"type":"object","properties":{},"additionalProperties":false}""";
    private const string ReplaceDocumentSchema = """{"type":"object","properties":{"markdown":{"type":"string"},"reason":{"type":"string"}},"required":["markdown"],"additionalProperties":false}""";
    private const string ApplyTextEditSchema = """{"type":"object","properties":{"startOffset":{"type":"integer","minimum":0},"length":{"type":"integer","minimum":0},"replacement":{"type":"string"},"reason":{"type":"string"}},"required":["startOffset","length","replacement"],"additionalProperties":false}""";
    private const string InsertTextSchema = """{"type":"object","properties":{"offset":{"type":"integer","minimum":0},"text":{"type":"string"}},"required":["text"],"additionalProperties":false}""";
    private const string ReplaceSelectionSchema = """{"type":"object","properties":{"text":{"type":"string"},"reason":{"type":"string"}},"required":["text"],"additionalProperties":false}""";
    private const string OpenDocumentSchema = """{"type":"object","properties":{"path":{"type":"string"},"encodingName":{"type":"string"}},"required":["path"],"additionalProperties":false}""";
    private const string UiSetThemeSchema = """{"type":"object","properties":{"themeKey":{"type":"string","enum":["system","light","dark","aquatic","desert","dusk","night-sky"]}},"required":["themeKey"],"additionalProperties":false}""";
    private const string UiSetTypographySchema = """{"type":"object","properties":{"typographyKey":{"type":"string"}},"required":["typographyKey"],"additionalProperties":false}""";
    private const string UiSetLanguageSchema = """{"type":"object","properties":{"cultureName":{"type":"string","enum":["zh-CN","zh-Hant","en-US","ja-JP"]}},"required":["cultureName"],"additionalProperties":false}""";
    private const string UiSetLayoutSchema = """{"type":"object","properties":{"sidebarVisible":{"type":"boolean"},"statusBarVisible":{"type":"boolean"},"previewVisible":{"type":"boolean"},"sourceMode":{"type":"boolean"},"lineNumbersVisible":{"type":"boolean"},"compactLayout":{"type":"boolean"}},"additionalProperties":false}""";
    private const string UiShowSidebarTabSchema = """{"type":"object","properties":{"tab":{"type":"string","enum":["files","outline"]}},"required":["tab"],"additionalProperties":false}""";
    private const string UiOpenPanelSchema = """{"type":"object","properties":{"panel":{"type":"string","enum":["find","replace","properties","wordCount","mcpSettings","changelog","thanks","about"]}},"required":["panel"],"additionalProperties":false}""";
    private const string UiApplyEditorCommandSchema = """{"type":"object","properties":{"command":{"type":"string","enum":["undo","redo","copy","selectAll","paragraph","heading1","heading2","heading3","bold","italic","inlineCode","link","image","quote","orderedList","unorderedList","taskList","codeFence","mathBlock","table","horizontalRule","clearFormatting"]}},"required":["command"],"additionalProperties":false}""";
    private const string ExportCurrentDocumentSchema = """{"type":"object","properties":{"format":{"type":"string","enum":["HTML","PDF","PNG","Word"]}},"required":["format"],"additionalProperties":false}""";
    private const string CopyRenderedHtmlSchema = """{"type":"object","properties":{"target":{"type":"string","enum":["wechat","zhihu","juejin"]}},"required":["target"],"additionalProperties":false}""";

    private readonly MainWindowViewModel _shell;
    private readonly IMarkdownOutlineService _outlineService;
    private readonly IDocumentService _documentService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IMcpOperationConfirmationService _confirmationService;
    private readonly IMcpOperationAuditService _auditService;

    public McpToolDispatcher(
        MainWindowViewModel shell,
        IMarkdownOutlineService outlineService,
        IDocumentService documentService,
        IAppSettingsStore settingsStore,
        IMcpOperationConfirmationService confirmationService,
        IMcpOperationAuditService auditService)
    {
        _shell = shell;
        _outlineService = outlineService;
        _documentService = documentService;
        _settingsStore = settingsStore;
        _confirmationService = confirmationService;
        _auditService = auditService;
    }

    public McpToolsListResult ListTools() => new(Tools);

    public async Task<McpToolCallResult> CallToolAsync(string name, JsonElement? arguments)
    {
        var toolName = NormalizeToolName(name);
        try
        {
            var result = await Dispatcher.UIThread.InvokeAsync(async () => await CallToolOnUiThreadAsync(toolName, arguments));
            RecordAudit(toolName, result, error: null);
            return TextResult(SerializeToolResult(result));
        }
        catch (Exception exception)
        {
            RecordAudit(toolName, result: null, exception.Message);
            return TextResult(exception.Message, isError: true);
        }
    }

    private async Task<object> CallToolOnUiThreadAsync(string name, JsonElement? arguments)
    {
        switch (name)
        {
            case "vex_get_current_document":
                return GetCurrentDocument();
            case "vex_get_document_outline":
                return GetOutline();
            case "vex_get_selection":
                return GetSelection();
            case "vex_replace_current_document":
                return await ReplaceCurrentDocumentAsync(Read(arguments, McpJsonContext.Default.ReplaceCurrentDocumentInput));
            case "vex_apply_text_edit":
                return await ApplyTextEditAsync(Read(arguments, McpJsonContext.Default.ApplyTextEditInput));
            case "vex_insert_text":
                return await InsertTextAsync(Read(arguments, McpJsonContext.Default.InsertTextInput));
            case "vex_replace_selection":
                return await ReplaceSelectionAsync(Read(arguments, McpJsonContext.Default.ReplaceSelectionInput));
            case "vex_open_document":
                return await OpenDocumentAsync(Read(arguments, McpJsonContext.Default.OpenDocumentInput));
            case "vex_save_current_document":
                await _shell.SaveAsync();
                return new OperationResult("ok", "saved");
            case "vex_refresh_preview":
            case "vex_ui_refresh_preview":
                _shell.RefreshPreview();
                return new OperationResult("ok", "preview refreshed");
            case "vex_get_rendered_html":
                return new RenderedHtmlResult(Markdig.Markdown.ToHtml(_shell.Markdown, MarkdownPipeline));
            case "vex_get_app_status":
                return GetAppStatus();
            case "vex_get_operation_audit":
                return GetOperationAudit();
            case "vex_ui_get_state":
                return GetUiState();
            case "vex_ui_set_theme":
                return SetTheme(Read(arguments, McpJsonContext.Default.UiSetThemeInput));
            case "vex_ui_set_typography":
                return SetTypography(Read(arguments, McpJsonContext.Default.UiSetTypographyInput));
            case "vex_ui_set_language":
                return SetLanguage(Read(arguments, McpJsonContext.Default.UiSetLanguageInput));
            case "vex_ui_set_layout":
                return SetLayout(Read(arguments, McpJsonContext.Default.UiSetLayoutInput));
            case "vex_ui_show_sidebar_tab":
                return ShowSidebarTab(Read(arguments, McpJsonContext.Default.UiShowSidebarTabInput));
            case "vex_ui_open_panel":
                return await OpenPanelAsync(Read(arguments, McpJsonContext.Default.UiOpenPanelInput));
            case "vex_ui_apply_editor_command":
                return ApplyEditorCommand(Read(arguments, McpJsonContext.Default.UiApplyEditorCommandInput));
            case "vex_ui_export_current_document":
                return await ExportCurrentDocumentAsync(Read(arguments, McpJsonContext.Default.ExportCurrentDocumentInput));
            case "vex_ui_copy_rendered_html":
                return await CopyRenderedHtmlAsync(Read(arguments, McpJsonContext.Default.CopyRenderedHtmlInput));
            default:
                throw new InvalidOperationException($"Unknown tool: {name}");
        }
    }

    private CurrentDocumentResult GetCurrentDocument()
    {
        var document = _shell.GetCurrentDocumentSnapshot();
        return new CurrentDocumentResult(
            document.FilePath,
            document.FileName,
            _shell.Markdown,
            _shell.DocumentInfo.IsModified,
            GetEncodingDisplayName(document.Encoding));
    }

    private OutlineResult GetOutline()
    {
        return new OutlineResult(_outlineService.BuildOutline(_shell.Markdown)
            .Select(item => new OutlineItemResult(item.Level, item.Title, item.Line))
            .ToArray());
    }

    private static SelectionResult GetSelection()
    {
        var selection = CodeWF.EventBus.EventBus.Default.Query(new EditorSelectionQuery());
        return new SelectionResult(selection.Text, selection.StartOffset, selection.Length);
    }

    private async Task<OperationResult> ReplaceCurrentDocumentAsync(ReplaceCurrentDocumentInput input)
    {
        if (!await ConfirmIfRequiredAsync(
                "vex_replace_current_document",
                GetCurrentTargetName(),
                input.Reason ?? "replace current markdown"))
        {
            return new OperationResult("canceled", "operation rejected");
        }

        _shell.ReplaceMarkdownFromMcp(input.Markdown);
        return new OperationResult("ok", "document replaced");
    }

    private async Task<OperationResult> ApplyTextEditAsync(ApplyTextEditInput input)
    {
        if (!await ConfirmIfRequiredAsync(
                "vex_apply_text_edit",
                GetCurrentTargetName(),
                input.Reason ?? $"offset {input.StartOffset}, length {input.Length}"))
        {
            return new OperationResult("canceled", "operation rejected");
        }

        _shell.ApplyTextEditFromMcp(input.StartOffset, input.Length, input.Replacement);
        return new OperationResult("ok", "text edit applied");
    }

    private async Task<OperationResult> InsertTextAsync(InsertTextInput input)
    {
        if (!await ConfirmIfRequiredAsync(
                "vex_insert_text",
                GetCurrentTargetName(),
                $"insert {input.Text.Length} characters"))
        {
            return new OperationResult("canceled", "operation rejected");
        }

        var offset = input.Offset ?? _shell.Markdown.Length;
        _shell.ApplyTextEditFromMcp(offset, 0, input.Text);
        return new OperationResult("ok", "text inserted");
    }

    private async Task<OperationResult> ReplaceSelectionAsync(ReplaceSelectionInput input)
    {
        var selection = CodeWF.EventBus.EventBus.Default.Query(new EditorSelectionQuery());
        if (selection.Length <= 0)
        {
            throw new InvalidOperationException("No active editor selection.");
        }

        if (!await ConfirmIfRequiredAsync(
                "vex_replace_selection",
                GetCurrentTargetName(),
                input.Reason ?? $"replace selection at offset {selection.StartOffset}"))
        {
            return new OperationResult("canceled", "operation rejected");
        }

        _shell.ApplyTextEditFromMcp(selection.StartOffset, selection.Length, input.Text);
        return new OperationResult("ok", "selection replaced");
    }

    private async Task<OperationResult> OpenDocumentAsync(OpenDocumentInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Path))
        {
            throw new InvalidOperationException("Document path is required.");
        }

        if (!_documentService.IsSupportedDocumentPath(input.Path))
        {
            throw new InvalidOperationException("Unsupported document path.");
        }

        var fullPath = ResolveAuthorizedDocumentPath(input.Path);
        if (!await ConfirmIfRequiredAsync("vex_open_document", fullPath, "open document"))
        {
            return new OperationResult("canceled", "operation rejected");
        }

        await _shell.OpenPathFromMcpAsync(fullPath, input.EncodingName);
        return new OperationResult("ok", "document opened");
    }

    private string ResolveAuthorizedDocumentPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UNC paths are not allowed.");
        }

        var settings = _settingsStore.Current;
        var scope = settings.McpAccessScope;
        if (scope == McpAccessScope.CurrentFolder)
        {
            var currentFolder = _shell.DocumentInfo.CurrentFilePath is { Length: > 0 } currentPath
                ? Path.GetDirectoryName(currentPath)
                : null;
            EnsureUnderRoot(fullPath, currentFolder, "Current document folder is unavailable.");
            return fullPath;
        }

        if (scope == McpAccessScope.CustomFolder)
        {
            EnsureUnderRoot(fullPath, settings.McpAllowedWorkspacePath, "Custom MCP folder is unavailable.");
            return fullPath;
        }

        var currentDocumentPath = _shell.DocumentInfo.CurrentFilePath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath) || !PathComparer.Equals(fullPath, Path.GetFullPath(currentDocumentPath)))
        {
            throw new InvalidOperationException("Path is outside the current document scope.");
        }

        return fullPath;
    }

    private static void EnsureUnderRoot(string fullPath, string? rootPath, string missingRootMessage)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException(missingRootMessage);
        }

        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("Authorized folder does not exist.");
        }

        var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!PathComparer.Equals(fullPath, root) && !fullPath.StartsWith(normalizedRoot, PathComparison))
        {
            throw new InvalidOperationException("Path is outside the authorized folder.");
        }
    }

    private Task<bool> ConfirmIfRequiredAsync(string toolName, string target, string summary)
    {
        return _settingsStore.Current.McpRequireConfirmation == false
            ? Task.FromResult(true)
            : _confirmationService.ConfirmAsync(toolName, target, summary);
    }

    private void RecordAudit(string toolName, object? result, string? error)
    {
        var operationResult = result as OperationResult;
        var succeeded = error is null && operationResult?.Status != "canceled";
        var confirmed = error is null && operationResult?.Status != "canceled";
        var requiresConfirmation = ToolRequiresConfirmation(toolName);
        _auditService.Record(new McpOperationRecord(
            DateTimeOffset.Now,
            toolName,
            ResolveAuditTarget(toolName),
            ResolveOperationType(toolName),
            requiresConfirmation,
            !requiresConfirmation || confirmed,
            succeeded,
            error ?? (operationResult?.Status == "canceled" ? operationResult.Detail : null)));
    }

    private string ResolveAuditTarget(string toolName)
    {
        return toolName switch
        {
            "vex_ui_copy_rendered_html" => "clipboard",
            "vex_ui_set_theme" or "vex_ui_set_typography" or "vex_ui_set_language" or "vex_ui_set_layout"
                or "vex_ui_show_sidebar_tab" or "vex_ui_open_panel" or "vex_ui_refresh_preview"
                or "vex_ui_apply_editor_command" => "ui",
            _ => GetCurrentTargetName()
        };
    }

    private static string ResolveOperationType(string toolName)
    {
        return toolName switch
        {
            "vex_get_current_document" or "vex_get_document_outline" or "vex_get_selection"
                or "vex_get_rendered_html" or "vex_get_app_status" or "vex_ui_get_state" => "read",
            "vex_replace_current_document" or "vex_apply_text_edit" or "vex_insert_text"
                or "vex_replace_selection" => "edit",
            "vex_open_document" => "open",
            "vex_save_current_document" => "save",
            "vex_ui_export_current_document" => "export",
            "vex_ui_copy_rendered_html" => "clipboard",
            _ => "ui"
        };
    }

    private static bool ToolRequiresConfirmation(string toolName)
    {
        return toolName is "vex_replace_current_document"
            or "vex_apply_text_edit"
            or "vex_insert_text"
            or "vex_replace_selection"
            or "vex_open_document"
            or "vex_ui_copy_rendered_html";
    }

    private string GetCurrentTargetName()
    {
        var document = _shell.GetCurrentDocumentSnapshot();
        return document.FilePath ?? document.FileName;
    }

    private AppStatusResult GetAppStatus()
    {
        return new AppStatusResult(
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            _shell.Appearance.SelectedTheme?.Key,
            _shell.Appearance.SelectedTypography?.Key,
            _shell.DocumentInfo.CurrentFilePath,
            _shell.DocumentInfo.IsModified);
    }

    private OperationAuditResult GetOperationAudit()
    {
        return new OperationAuditResult(_auditService.GetRecent());
    }

    private UiStateResult GetUiState()
    {
        return new UiStateResult(
            _shell.Appearance.SelectedTheme?.Key,
            _shell.Appearance.SelectedTypography?.Key,
            _shell.Appearance.SelectedLanguage?.CultureName,
            _shell.Appearance.IsCompactLayout,
            _shell.Layout.IsSidebarVisible,
            _shell.Navigation.SelectedSideTabIndex == 1 ? "outline" : "files",
            _shell.Layout.IsPreviewVisible,
            _shell.Layout.IsSourceMode,
            _shell.EditorDisplay.ShowLineNumbers,
            _shell.Layout.IsStatusBarVisible);
    }

    private OperationResult SetTheme(UiSetThemeInput input)
    {
        _shell.Appearance.SelectThemeByKey(input.ThemeKey);
        return new OperationResult("ok", "theme updated");
    }

    private OperationResult SetTypography(UiSetTypographyInput input)
    {
        _shell.Appearance.SelectTypographyByKey(input.TypographyKey);
        return new OperationResult("ok", "typography updated");
    }

    private OperationResult SetLanguage(UiSetLanguageInput input)
    {
        _shell.Appearance.SelectLanguageByCulture(input.CultureName);
        return new OperationResult("ok", "language updated");
    }

    private OperationResult SetLayout(UiSetLayoutInput input)
    {
        if (input.SidebarVisible is { } sidebar)
        {
            _shell.Layout.IsSidebarVisible = sidebar;
        }

        if (input.StatusBarVisible is { } statusBar)
        {
            _shell.Layout.IsStatusBarVisible = statusBar;
        }

        if (input.PreviewVisible is { } preview)
        {
            _shell.Layout.IsPreviewVisible = preview;
        }

        if (input.LineNumbersVisible is { } lineNumbers)
        {
            _shell.EditorDisplay.ShowLineNumbers = lineNumbers;
        }

        if (input.CompactLayout is { } compact)
        {
            _shell.Appearance.IsCompactLayout = compact;
        }

        if (input.SourceMode is { } sourceMode && sourceMode != _shell.Layout.IsSourceMode)
        {
            _shell.Layout.ToggleSourceMode();
        }

        return new OperationResult("ok", "layout updated");
    }

    private OperationResult ShowSidebarTab(UiShowSidebarTabInput input)
    {
        if (input.Tab.Equals("outline", StringComparison.OrdinalIgnoreCase))
        {
            _shell.Layout.ShowOutline();
        }
        else
        {
            _shell.Layout.ShowFiles();
        }

        return new OperationResult("ok", "sidebar tab updated");
    }

    private async Task<OperationResult> OpenPanelAsync(UiOpenPanelInput input)
    {
        switch (NormalizeToolToken(input.Panel))
        {
            case "find":
                _shell.ShowFindPanel();
                break;
            case "replace":
                _shell.ShowReplacePanel();
                break;
            case "properties":
                _shell.ShowProperties();
                break;
            case "wordcount":
                _shell.WordCount();
                break;
            case "changelog":
                await _shell.Help.OpenHelpTopic("changelog");
                break;
            case "thanks":
                await _shell.Help.OpenHelpTopic("thanks");
                break;
            case "about":
                await _shell.Help.OpenHelpTopic("about");
                break;
            case "mcpsettings":
                CodeWF.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.ShowMcpSettings));
                break;
            default:
                throw new InvalidOperationException($"Unsupported panel: {input.Panel}");
        }

        return new OperationResult("ok", "panel opened");
    }

    private async Task<OperationResult> ExportCurrentDocumentAsync(ExportCurrentDocumentInput input)
    {
        if (NormalizeToolToken(input.Format) is not ("html" or "pdf" or "png" or "word"))
        {
            throw new InvalidOperationException($"Unsupported export format: {input.Format}");
        }

        await _shell.Export(input.Format);
        return new OperationResult("ok", "export requested");
    }

    private async Task<OperationResult> CopyRenderedHtmlAsync(CopyRenderedHtmlInput input)
    {
        if (NormalizeToolToken(input.Target) is not ("wechat" or "zhihu" or "juejin"))
        {
            throw new InvalidOperationException($"Unsupported copy target: {input.Target}");
        }

        if (!await ConfirmIfRequiredAsync("vex_ui_copy_rendered_html", "clipboard", $"copy rendered HTML for {input.Target}"))
        {
            return new OperationResult("canceled", "operation rejected");
        }

        await _shell.CopyHtml(input.Target);
        return new OperationResult("ok", "rendered HTML copied");
    }

    private static OperationResult ApplyEditorCommand(UiApplyEditorCommandInput input)
    {
        if (!TryMapEditorAction(input.Command, out var action))
        {
            throw new InvalidOperationException($"Unsupported editor command: {input.Command}");
        }

        CodeWF.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
        return new OperationResult("ok", "editor command applied");
    }

    private static bool TryMapEditorAction(string command, out EditorActionKind action)
    {
        var normalized = NormalizeToolToken(command);
        action = normalized switch
        {
            "undo" => EditorActionKind.Undo,
            "redo" => EditorActionKind.Redo,
            "copy" => EditorActionKind.Copy,
            "selectall" => EditorActionKind.SelectAll,
            "paragraph" => EditorActionKind.Paragraph,
            "heading1" => EditorActionKind.Heading1,
            "heading2" => EditorActionKind.Heading2,
            "heading3" => EditorActionKind.Heading3,
            "bold" => EditorActionKind.Bold,
            "italic" => EditorActionKind.Italic,
            "inlinecode" => EditorActionKind.InlineCode,
            "link" => EditorActionKind.Link,
            "image" => EditorActionKind.Image,
            "quote" => EditorActionKind.Quote,
            "orderedlist" => EditorActionKind.OrderedList,
            "unorderedlist" => EditorActionKind.UnorderedList,
            "tasklist" => EditorActionKind.TaskList,
            "codefence" => EditorActionKind.CodeFence,
            "mathblock" => EditorActionKind.MathBlock,
            "table" => EditorActionKind.Table,
            "horizontalrule" => EditorActionKind.HorizontalRule,
            "clearformatting" => EditorActionKind.ClearFormatting,
            _ => default
        };
        return normalized is "undo" or "redo" or "copy" or "selectall" or "paragraph"
            or "heading1" or "heading2" or "heading3" or "bold" or "italic" or "inlinecode"
            or "link" or "image" or "quote" or "orderedlist" or "unorderedlist" or "tasklist"
            or "codefence" or "mathblock" or "table" or "horizontalrule" or "clearformatting";
    }

    private static T Read<T>(JsonElement? arguments, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (arguments is null || arguments.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return JsonSerializer.Deserialize("{}", typeInfo)!;
        }

        return arguments.Value.Deserialize(typeInfo)!;
    }

    private static McpToolCallResult TextResult(string text, bool isError = false)
    {
        return new McpToolCallResult([new McpContentItem("text", text)], isError);
    }

    private static string SerializeToolResult(object result)
    {
        return result switch
        {
            CurrentDocumentResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.CurrentDocumentResult),
            OutlineResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.OutlineResult),
            SelectionResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.SelectionResult),
            AppStatusResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.AppStatusResult),
            UiStateResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.UiStateResult),
            RenderedHtmlResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.RenderedHtmlResult),
            OperationResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.OperationResult),
            OperationAuditResult value => JsonSerializer.Serialize(value, McpJsonContext.Default.OperationAuditResult),
            _ => JsonSerializer.Serialize(new OperationResult("ok", result.ToString()), McpJsonContext.Default.OperationResult)
        };
    }

    private static McpToolDescription Tool(string name, string description, string inputSchema = EmptySchema)
    {
        using var document = JsonDocument.Parse(inputSchema);
        return new McpToolDescription(name, description, document.RootElement.Clone());
    }

    private static string NormalizeToolName(string name)
    {
        return name.Replace('.', '_');
    }

    private static string NormalizeToolToken(string value)
    {
        return value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static string GetEncodingDisplayName(Encoding encoding)
    {
        if (encoding is UTF8Encoding { Preamble.Length: > 0 })
        {
            return "UTF-8 BOM";
        }

        if (encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return "UTF-8";
        }

        return encoding.WebName.ToUpperInvariant();
    }
}
