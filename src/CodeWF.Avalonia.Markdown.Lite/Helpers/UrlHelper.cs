using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Helpers;

internal static class UrlHelper
{
    internal static bool IsAllowedExternalUri(string? url)
    {
		return MarkdownUrlPolicy.IsAllowedExternalUri(url);
	}

    /// <summary>
    /// 使用系统默认方式打开链接，控件层只负责触发，不内置浏览器。
    /// </summary>
    public static void Open(string url)
    {
		MarkdownUrlPolicy.Open(url);
    }
}
