using System.Globalization;
using System.Net;

using CodeWF.Toolkit.Lang;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// HTML 导出选项。默认输出自包含(内联样式、本地图片内嵌为 data URI)的
/// 单文件文档，可直接用于分享、打印预览或二次处理。
/// </summary>
public sealed record MarkdownHtmlDocumentOptions
{
    public MarkdownSyntaxOptions? SyntaxOptions { get; init; }
	/// <summary>文档语言（html lang 属性）；为空时使用当前 UI 区域。</summary>
	public string? Language { get; init; }

	/// <summary>文档标题；为空时取文件名（去掉扩展名）。</summary>
	public string? Title { get; init; }

	/// <summary>是否输出 CF_HTML 片段标记（剪贴板场景需要）。</summary>
	public bool IncludeFragmentMarkers { get; init; }

	/// <summary>是否包裹完整 HTML 文档；false 时只输出正文片段。</summary>
	public bool IncludeDocumentWrapper { get; init; } = true;

	/// <summary>是否输出响应式 viewport 元信息（仅完整文档模式可用）。</summary>
	public bool IncludeViewportMeta { get; init; } = true;

	/// <summary>是否追加打印友好样式（分页避让、保留背景色）。</summary>
	public bool IsPrintFriendly { get; init; }

	/// <summary>本地图片是否内嵌为 data URI；false 时保留原始相对路径。</summary>
	public bool EmbedLocalImages { get; init; } = true;
}

/// <summary>
/// HTML 导出：与社交平台复制共用同一 <see cref="MarkdownExportStyle"/> 与图片加载链路，
/// 宿主无需再自备 HTML 模板。
/// </summary>
public static class MarkdownHtmlExporter
{
	public static void Export(
		MarkdownExportDocument document,
		string savePath,
		MarkdownExportStyle? style = null,
		MarkdownHtmlDocumentOptions? options = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(savePath);

		var html = RenderDocument(document, style, options);
		File.WriteAllText(savePath, html, new System.Text.UTF8Encoding(false));
	}

	public static void ExportMarkdown(
		string markdown,
		string savePath,
		string? themeName = null,
		string? typographySize = null,
		MarkdownHtmlDocumentOptions? options = null)
	{
		Export(
			new MarkdownExportDocument(markdown),
			savePath,
			MarkdownExportStyle.Resolve(themeName, typographySize),
			options);
	}

	public static string RenderDocument(
		MarkdownExportDocument document,
		MarkdownExportStyle? style = null,
		MarkdownHtmlDocumentOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(document);

		var resolvedOptions = options ?? new MarkdownHtmlDocumentOptions();
		var resolvedStyle = style ?? MarkdownExportStyle.Resolve(null, null);
		var content = CreateContent(
			document,
			resolvedOptions with
			{
				IncludeFragmentMarkers = resolvedOptions.IncludeDocumentWrapper && resolvedOptions.IncludeFragmentMarkers
			},
			resolvedStyle);
		return resolvedOptions.IncludeDocumentWrapper
			? content.Html
			: ExtractFragment(content.Html);
	}

	public static string RenderBody(
		MarkdownExportDocument document,
		MarkdownExportStyle? style = null,
		MarkdownHtmlDocumentOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(document);

		var resolvedOptions = options ?? new MarkdownHtmlDocumentOptions();
		if (resolvedOptions.IncludeFragmentMarkers)
		{
			return CreateContent(
				document,
				resolvedOptions,
				style ?? MarkdownExportStyle.Resolve(null, null)).Html;
		}

		return RenderDocument(
			document,
			style,
			resolvedOptions with { IncludeDocumentWrapper = false, IncludeFragmentMarkers = false });
	}

	/// <summary>
	/// 生成剪贴板/打印预览可直接使用的 HTML 内容（含纯文本回退）。
	/// 内容由社交复制链路产出：自带内联样式，本地图片按需内嵌为 data URI。
	/// </summary>
	public static MarkdownHtmlCopyContent CreateContent(
		MarkdownExportDocument document,
		MarkdownHtmlDocumentOptions? options = null,
		MarkdownExportStyle? style = null,
		CopyKind target = CopyKind.Wechat)
	{
		ArgumentNullException.ThrowIfNull(document);

		var resolvedOptions = options ?? new MarkdownHtmlDocumentOptions();
		var resolvedStyle = style ?? MarkdownExportStyle.Resolve(null, null);
		var copyOptions = new MarkdownSocialCopyOptions
		{
            SyntaxOptions = resolvedOptions.SyntaxOptions,
			Title = resolvedOptions.Title,
			Language = ResolveLanguage(resolvedOptions.Language),
			IncludeFragmentMarkers = resolvedOptions.IncludeFragmentMarkers
		};
		var content = MarkdownHtmlClipboard.CreateHtmlCopyContent(
			document,
			target,
			resolvedStyle,
			copyOptions);
		if (!resolvedOptions.EmbedLocalImages)
		{
			content = new MarkdownHtmlCopyContent(
				content.Text,
				RemoveEmbeddedImageDataUris(content.Html));
		}

		return content;
	}

	private static string ResolveLanguage(string? language)
	{
		if (!string.IsNullOrWhiteSpace(language))
		{
			return language;
		}

		try
		{
			return I18nManager.Instance.Culture?.Name ?? CultureInfo.CurrentUICulture.Name;
		}
		catch (InvalidOperationException)
		{
			return CultureInfo.CurrentUICulture.Name;
		}
	}

	private static string ExtractFragment(string html)
	{
		var start = html.IndexOf(MarkdownHtmlClipboard.StartFragmentMarker, StringComparison.Ordinal);
		if (start >= 0)
		{
			start += MarkdownHtmlClipboard.StartFragmentMarker.Length;
			var end = html.IndexOf(MarkdownHtmlClipboard.EndFragmentMarker, start, StringComparison.Ordinal);
			return (end < 0 ? html[start..] : html[start..end]).Trim();
		}

		var bodyStart = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
		var bodyTagEnd = bodyStart < 0 ? -1 : html.IndexOf('>', bodyStart);
		if (bodyTagEnd >= 0)
		{
			bodyStart = bodyTagEnd + 1;
			var bodyEnd = html.IndexOf("</body", bodyStart, StringComparison.OrdinalIgnoreCase);
			return (bodyEnd < 0 ? html[bodyStart..] : html[bodyStart..bodyEnd]).Trim();
		}

		return html.Trim();
	}

	private static string RemoveEmbeddedImageDataUris(string html)
	{
		const string Marker = "src=\"data:";
		var builder = new System.Text.StringBuilder(html.Length);
		var index = 0;
		while (index < html.Length)
		{
			var start = html.IndexOf(Marker, index, StringComparison.OrdinalIgnoreCase);
			if (start < 0)
			{
				builder.Append(html, index, html.Length - index);
				break;
			}

			var valueStart = start + Marker.Length;
			var end = html.IndexOf('"', valueStart);
			if (end < 0)
			{
				builder.Append(html, index, html.Length - index);
				break;
			}

			builder.Append(html, index, start - index);
			builder.Append("src=\"\"");
			index = end + 1;
		}

		return builder.ToString();
	}
}
