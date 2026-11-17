namespace Vex.Core.Messaging;

/// <summary>
/// 命令面板键契约：与 I18n json 的 Vex.VexL.* 键一一对应（json 为真值源，本类是编译期常量镜像）。
/// json 增删命令键时同步本类；分组键与动作键分离书写。
/// </summary>
public static class VexCommandKeys
{
    // ---- 分组键（json: CommandGroup*）----
    public const string GroupFile = "Vex.VexL.CommandGroupFile";
    public const string GroupEdit = "Vex.VexL.CommandGroupEdit";
    public const string GroupInsert = "Vex.VexL.CommandGroupInsert";
    public const string GroupFormat = "Vex.VexL.CommandGroupFormat";
    public const string GroupView = "Vex.VexL.CommandGroupView";
    public const string GroupSettings = "Vex.VexL.CommandGroupSettings";
    public const string GroupHelp = "Vex.VexL.CommandGroupHelp";

    // ---- 文件 ----
    public const string NewDocument = "Vex.VexL.NewDocument";
    public const string NewWindow = "Vex.VexL.NewWindow";
    public const string Open = "Vex.VexL.OpenFile";
    public const string OpenFolder = "Vex.VexL.OpenFolder";
    public const string OpenFile = "Vex.VexL.OpenFile";
    public const string QuickOpen = "Vex.VexL.QuickOpen";
    public const string Import = "Vex.VexL.Import";
    public const string OpenRecent = "Vex.VexL.OpenRecent";
    public const string ReopenWithEncoding = "Vex.VexL.ReopenWithEncoding";
    public const string Save = "Vex.VexL.Save";
    public const string SaveAs = "Vex.VexL.SaveAs";
    public const string SaveAll = "Vex.VexL.SaveAll";
    public const string OpenFileLocation = "Vex.VexL.OpenFileLocation";
    public const string Delete = "Vex.VexL.Delete";
    public const string Export = "Vex.VexL.Export";
    public const string CopyHtml = "Vex.VexL.CopyToWechat";
    public const string CopyToWechat = "Vex.VexL.CopyToWechat";
    public const string Print = "Vex.VexL.Print";
    public const string CloseDocument = "Vex.VexL.MenuCloseDocument";
    public const string RefreshPreview = "Vex.VexL.RefreshPreview";
    public const string TemplateCenter = "Vex.VexL.TemplateCenter";
    public const string TemplateSave = "Vex.VexL.TemplateSave";
    public const string RefreshFiles = "Vex.VexL.RefreshFiles";
    public const string OpenPathLocation = "Vex.VexL.OpenPathLocation";
    public const string AddFolderToWorkspace = "Vex.VexL.AddFolderToWorkspace";
    public const string RemoveWorkspaceFolder = "Vex.VexL.RemoveWorkspaceFolder";

    // ---- 编辑 ----
    public const string Undo = "Vex.VexL.Undo";
    public const string Redo = "Vex.VexL.Redo";
    public const string Cut = "Vex.VexL.Cut";
    public const string Copy = "Vex.VexL.Copy";
    public const string Paste = "Vex.VexL.Paste";
    public const string SelectAll = "Vex.VexL.SelectAll";
    public const string CopyPlainText = "Vex.VexL.CopyPlainText";
    public const string Find = "Vex.VexL.Find";
    public const string Replace = "Vex.VexL.Replace";
    public const string SmartNewLine = "Vex.VexL.SmartNewLine";
    public const string Indent = "Vex.VexL.Indent";
    public const string Outdent = "Vex.VexL.Outdent";

    // ---- 插入 ----
    public const string Paragraph = "Vex.VexL.Paragraph";
    public const string Heading1 = "Vex.VexL.Heading1";
    public const string Heading2 = "Vex.VexL.Heading2";
    public const string Heading3 = "Vex.VexL.Heading3";
    public const string Heading4 = "Vex.VexL.Heading4";
    public const string Heading5 = "Vex.VexL.Heading5";
    public const string Heading6 = "Vex.VexL.Heading6";
    public const string Quote = "Vex.VexL.Quote";
    public const string UnorderedList = "Vex.VexL.UnorderedList";
    public const string OrderedList = "Vex.VexL.OrderedList";
    public const string TaskList = "Vex.VexL.TaskList";
    public const string CodeFence = "Vex.VexL.CodeBlock";
    public const string Table = "Vex.VexL.Table";
    public const string MathBlock = "Vex.VexL.MathBlock";
    public const string HorizontalRule = "Vex.VexL.HorizontalRule";
    public const string Link = "Vex.VexL.Link";
    public const string Image = "Vex.VexL.Image";

    // ---- 格式 ----
    public const string Bold = "Vex.VexL.Bold";
    public const string Italic = "Vex.VexL.Italic";
    public const string Strikethrough = "Vex.VexL.Strikethrough";
    public const string Highlight = "Vex.VexL.Highlight";
    public const string Superscript = "Vex.VexL.Superscript";
    public const string Subscript = "Vex.VexL.Subscript";
    public const string InlineCode = "Vex.VexL.InlineCode";
    public const string ClearFormatting = "Vex.VexL.ClearFormatting";
    public const string ClearSpanStyles = "Vex.VexL.ClearSpanStyles";

    // ---- 视图 ----
    public const string FocusEditor = "Vex.VexL.FocusEditor";

    // ---- 设置 ----
    public const string NavSettings = "Vex.VexL.NavSettings";
    public const string McpSettingsTitle = "Vex.VexL.McpSettingsTitle";
    public const string McpAudit = "Vex.VexL.McpAudit";
    public const string McpSettings = "Vex.VexL.McpSettings";
}
