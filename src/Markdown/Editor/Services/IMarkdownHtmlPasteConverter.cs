namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>
/// 粘贴富 HTML 的可选接缝：编辑器包不依赖导出包，宿主若引用了
/// <c>CodeWF.Avalonia.Markdown.Export</c>，把 <c>MarkdownHtmlClipboard.Html2Markdown</c> 接进来即可获得
/// 「粘贴网页内容自动转 Markdown」；未接入时粘贴走普通文本。
/// </summary>
public interface IMarkdownHtmlPasteConverter
{
    /// <summary>把剪贴板里的 HTML 片段转成 Markdown；无法转换时返回 null。</summary>
    string? Html2Markdown(string html);
}

/// <summary>剪贴板 HTML 格式名（平台约定字符串，避免依赖导出包的类型）。</summary>
internal static class MarkdownHtmlPasteFormats
{
    public const string HtmlMime = "text/html";

    public const string MacHtml = "public.html";

    public const string WindowsHtml = "HTML Format";
}
