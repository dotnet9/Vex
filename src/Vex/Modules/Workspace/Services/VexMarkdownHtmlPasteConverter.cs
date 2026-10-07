using CodeWF.Markdown;
using CodeWF.Markdown.Editor.Services;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// 把导出包（CodeWF.Markdown.Export）的 HTML → Markdown 转换接到编辑器动作服务上，
/// 保留 Vex「粘贴网页内容自动转 Markdown」的行为。
/// </summary>
public sealed class VexMarkdownHtmlPasteConverter : IMarkdownHtmlPasteConverter
{
    public string? Html2Markdown(string html) => MarkdownHtmlClipboard.Html2Markdown(html);
}
