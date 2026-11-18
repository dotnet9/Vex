using System.Globalization;
using System.Text;

using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;
using Markdig;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Creates rich HTML clipboard payloads that paste targets such as Chromium,
/// WeChat Official Account, Zhihu, and Juejin can recognize.
/// </summary>
public static class MarkdownHtmlClipboard
{
	private static readonly MarkdownPipeline ClipboardPipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.Build();

	public const string HtmlMimeFormatName = "text/html";
	public const string MacHtmlFormatName = "public.html";
	public const string WindowsHtmlFormatName = "HTML Format";
	public const string StartFragmentMarker = "<!--StartFragment-->";
	public const string EndFragmentMarker = "<!--EndFragment-->";

	public static readonly DataFormat<string> HtmlMimeFormat = DataFormat.CreateStringPlatformFormat(HtmlMimeFormatName);
	public static readonly DataFormat<string> MacHtmlFormat = DataFormat.CreateStringPlatformFormat(MacHtmlFormatName);
	public static readonly DataFormat<byte[]> WindowsHtmlFormat = DataFormat.CreateBytesPlatformFormat(WindowsHtmlFormatName);

	/// <summary>
	/// Writes HTML to the clipboard with native rich-text formats and a plain text fallback.
	/// </summary>
	public static async Task SetHtmlAsync(IClipboard clipboard, string html, string? text = null)
	{
		ArgumentNullException.ThrowIfNull(clipboard);

		await clipboard.SetDataAsync(CreateHtmlDataTransfer(html, text)).ConfigureAwait(false);
		await clipboard.FlushAsync().ConfigureAwait(false);
	}

	public static Task SetHtmlAsync(IClipboard clipboard, MarkdownHtmlCopyContent content)
	{
		ArgumentNullException.ThrowIfNull(content);

		return SetHtmlAsync(clipboard, content.Html, content.Text);
	}

	/// <summary>
	/// Converts HTML copied from a web page into Markdown text.
	/// </summary>
	public static string Html2Markdown(string htmlContent)
	{
		return MarkdownHtmlConverter.Html2Markdown(htmlContent);
	}

	public static Task SetHtmlAsync(string markdown, CopyKind kind)
	{
		return SetHtmlAsync(ResolveClipboard(), markdown, kind, (MarkdownExportStyle?)null);
	}

	public static Task<bool> TrySetHtmlAsync(
		string markdown,
		string? targetName,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return TrySetHtmlAsync(ResolveClipboard(), markdown, targetName, themeName, typographySize, options);
	}

	public static Task<bool> TrySetHtmlAsync(
		string markdown,
		string? targetName,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return TrySetHtmlAsync(ResolveClipboard(), markdown, targetName, theme, options);
	}

	public static Task SetHtmlAsync(
		string markdown,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(ResolveClipboard(), markdown, kind, themeName, typographySize, options);
	}

	public static Task SetHtmlAsync(
		string markdown,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(ResolveClipboard(), markdown, kind, theme, options);
	}

	public static Task SetHtmlAsync(string markdown, MarkdownSocialCopyProfile profile)
	{
		return SetHtmlAsync(ResolveClipboard(), markdown, profile, (MarkdownExportStyle?)null);
	}

	public static Task SetHtmlAsync(
		string markdown,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(ResolveClipboard(), markdown, profile, themeName, typographySize, options);
	}

	public static Task SetHtmlAsync(
		string markdown,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(ResolveClipboard(), markdown, profile, theme, options);
	}

	public static Task SetHtmlAsync(IClipboard clipboard, string markdown, CopyKind kind)
	{
		return SetHtmlAsync(clipboard, markdown, kind, (MarkdownExportStyle?)null);
	}

	public static async Task<bool> TrySetHtmlAsync(
		IClipboard clipboard,
		string markdown,
		string? targetName,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!TryCreateHtmlCopyContent(markdown, targetName, out var content, themeName, typographySize, options))
		{
			return false;
		}

		await SetHtmlAsync(clipboard, content).ConfigureAwait(false);
		return true;
	}

	public static async Task<bool> TrySetHtmlAsync(
		IClipboard clipboard,
		string markdown,
		string? targetName,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!TryCreateHtmlCopyContent(markdown, targetName, out var content, theme, options))
		{
			return false;
		}

		await SetHtmlAsync(clipboard, content).ConfigureAwait(false);
		return true;
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		string markdown,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(markdown, kind, themeName, typographySize, options));
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		string markdown,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(markdown, kind, theme, options));
	}

	public static Task SetHtmlAsync(IClipboard clipboard, string markdown, MarkdownSocialCopyProfile profile)
	{
		return SetHtmlAsync(clipboard, markdown, profile, (MarkdownExportStyle?)null);
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		string markdown,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(markdown, profile, themeName, typographySize, options));
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		string markdown,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(markdown, profile, theme, options));
	}

	public static Task SetHtmlAsync(IClipboard clipboard, MarkdownExportDocument document, CopyKind kind)
	{
		return SetHtmlAsync(clipboard, document, kind, (MarkdownExportStyle?)null);
	}

	public static async Task<bool> TrySetHtmlAsync(
		IClipboard clipboard,
		MarkdownExportDocument document,
		string? targetName,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!TryCreateHtmlCopyContent(document, targetName, out var content, themeName, typographySize, options))
		{
			return false;
		}

		await SetHtmlAsync(clipboard, content).ConfigureAwait(false);
		return true;
	}

	public static async Task<bool> TrySetHtmlAsync(
		IClipboard clipboard,
		MarkdownExportDocument document,
		string? targetName,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!TryCreateHtmlCopyContent(document, targetName, out var content, theme, options))
		{
			return false;
		}

		await SetHtmlAsync(clipboard, content).ConfigureAwait(false);
		return true;
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		MarkdownExportDocument document,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(document, kind, themeName, typographySize, options));
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		MarkdownExportDocument document,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(document, kind, theme, options));
	}

	public static Task SetHtmlAsync(IClipboard clipboard, MarkdownExportDocument document, MarkdownSocialCopyProfile profile)
	{
		return SetHtmlAsync(clipboard, document, profile, (MarkdownExportStyle?)null);
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		MarkdownExportDocument document,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(document, profile, themeName, typographySize, options));
	}

	public static Task SetHtmlAsync(
		IClipboard clipboard,
		MarkdownExportDocument document,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateHtmlCopyContent(document, profile, theme, options));
	}

	public static Task SetFileHtmlAsync(string markdownFilePath, CopyKind kind)
	{
		return SetFileHtmlAsync(ResolveClipboard(), markdownFilePath, kind, (MarkdownExportStyle?)null);
	}

	public static Task<bool> TrySetFileHtmlAsync(
		string markdownFilePath,
		string? targetName,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return TrySetFileHtmlAsync(ResolveClipboard(), markdownFilePath, targetName, themeName, typographySize, options);
	}

	public static Task<bool> TrySetFileHtmlAsync(
		string markdownFilePath,
		string? targetName,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return TrySetFileHtmlAsync(ResolveClipboard(), markdownFilePath, targetName, theme, options);
	}

	public static Task SetFileHtmlAsync(
		string markdownFilePath,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetFileHtmlAsync(ResolveClipboard(), markdownFilePath, kind, themeName, typographySize, options);
	}

	public static Task SetFileHtmlAsync(
		string markdownFilePath,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetFileHtmlAsync(ResolveClipboard(), markdownFilePath, kind, theme, options);
	}

	public static Task SetFileHtmlAsync(string markdownFilePath, MarkdownSocialCopyProfile profile)
	{
		return SetFileHtmlAsync(ResolveClipboard(), markdownFilePath, profile, (MarkdownExportStyle?)null);
	}

	public static Task SetFileHtmlAsync(
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetFileHtmlAsync(ResolveClipboard(), markdownFilePath, profile, themeName, typographySize, options);
	}

	public static Task SetFileHtmlAsync(
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetFileHtmlAsync(ResolveClipboard(), markdownFilePath, profile, theme, options);
	}

	public static Task SetFileHtmlAsync(IClipboard clipboard, string markdownFilePath, CopyKind kind)
	{
		return SetFileHtmlAsync(clipboard, markdownFilePath, kind, (MarkdownExportStyle?)null);
	}

	public static async Task<bool> TrySetFileHtmlAsync(
		IClipboard clipboard,
		string markdownFilePath,
		string? targetName,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!TryCreateFileHtmlCopyContent(markdownFilePath, targetName, out var content, themeName, typographySize, options))
		{
			return false;
		}

		await SetHtmlAsync(clipboard, content).ConfigureAwait(false);
		return true;
	}

	public static async Task<bool> TrySetFileHtmlAsync(
		IClipboard clipboard,
		string markdownFilePath,
		string? targetName,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!TryCreateFileHtmlCopyContent(markdownFilePath, targetName, out var content, theme, options))
		{
			return false;
		}

		await SetHtmlAsync(clipboard, content).ConfigureAwait(false);
		return true;
	}

	public static Task SetFileHtmlAsync(
		IClipboard clipboard,
		string markdownFilePath,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateFileHtmlCopyContent(markdownFilePath, kind, themeName, typographySize, options));
	}

	public static Task SetFileHtmlAsync(
		IClipboard clipboard,
		string markdownFilePath,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateFileHtmlCopyContent(markdownFilePath, kind, theme, options));
	}

	public static Task SetFileHtmlAsync(IClipboard clipboard, string markdownFilePath, MarkdownSocialCopyProfile profile)
	{
		return SetFileHtmlAsync(clipboard, markdownFilePath, profile, (MarkdownExportStyle?)null);
	}

	public static Task SetFileHtmlAsync(
		IClipboard clipboard,
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateFileHtmlCopyContent(markdownFilePath, profile, themeName, typographySize, options));
	}

	public static Task SetFileHtmlAsync(
		IClipboard clipboard,
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return SetHtmlAsync(clipboard, CreateFileHtmlCopyContent(markdownFilePath, profile, theme, options));
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(string markdown, CopyKind kind)
	{
		return CreateHtmlCopyContent(markdown, kind, (MarkdownExportStyle?)null);
	}

	public static bool TryCreateHtmlCopyContent(
		string markdown,
		string? targetName,
		out MarkdownHtmlCopyContent content,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			content = EmptyContent;
			return false;
		}

		content = CreateHtmlCopyContent(markdown, profile, themeName, typographySize, options);
		return true;
	}

	public static bool TryCreateHtmlCopyContent(
		string markdown,
		string? targetName,
		out MarkdownHtmlCopyContent content,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			content = EmptyContent;
			return false;
		}

		content = CreateHtmlCopyContent(markdown, profile, theme, options);
		return true;
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		string markdown,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return CreateHtmlCopyContent(markdown, kind, MarkdownExportStyle.Resolve(themeName, typographySize), options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		string markdown,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return MarkdownSocialCopyRenderer.RenderMarkdown(markdown, kind, theme, options: options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(string markdown, MarkdownSocialCopyProfile profile)
	{
		return CreateHtmlCopyContent(markdown, profile, (MarkdownExportStyle?)null);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		string markdown,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return CreateHtmlCopyContent(markdown, profile, MarkdownExportStyle.Resolve(themeName, typographySize), options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		string markdown,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return MarkdownSocialCopyRenderer.RenderMarkdown(markdown, profile, theme, options: options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(MarkdownExportDocument document, CopyKind kind)
	{
		return CreateHtmlCopyContent(document, kind, (MarkdownExportStyle?)null);
	}

	public static bool TryCreateHtmlCopyContent(
		MarkdownExportDocument document,
		string? targetName,
		out MarkdownHtmlCopyContent content,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			content = EmptyContent;
			return false;
		}

		content = CreateHtmlCopyContent(document, profile, themeName, typographySize, options);
		return true;
	}

	public static bool TryCreateHtmlCopyContent(
		MarkdownExportDocument document,
		string? targetName,
		out MarkdownHtmlCopyContent content,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			content = EmptyContent;
			return false;
		}

		content = CreateHtmlCopyContent(document, profile, theme, options);
		return true;
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		MarkdownExportDocument document,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return CreateHtmlCopyContent(document, kind, MarkdownExportStyle.Resolve(themeName, typographySize), options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		MarkdownExportDocument document,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return MarkdownSocialCopyRenderer.Render(document, kind, theme, options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(MarkdownExportDocument document, MarkdownSocialCopyProfile profile)
	{
		return CreateHtmlCopyContent(document, profile, (MarkdownExportStyle?)null);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		MarkdownExportDocument document,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return CreateHtmlCopyContent(document, profile, MarkdownExportStyle.Resolve(themeName, typographySize), options);
	}

	public static MarkdownHtmlCopyContent CreateHtmlCopyContent(
		MarkdownExportDocument document,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return MarkdownSocialCopyRenderer.Render(document, profile, theme, options);
	}

	public static MarkdownHtmlCopyContent CreateFileHtmlCopyContent(string markdownFilePath, CopyKind kind)
	{
		return CreateFileHtmlCopyContent(markdownFilePath, kind, (MarkdownExportStyle?)null);
	}

	public static bool TryCreateFileHtmlCopyContent(
		string markdownFilePath,
		string? targetName,
		out MarkdownHtmlCopyContent content,
		string? themeName = null,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			content = EmptyContent;
			return false;
		}

		content = CreateFileHtmlCopyContent(markdownFilePath, profile, themeName, typographySize, options);
		return true;
	}

	public static bool TryCreateFileHtmlCopyContent(
		string markdownFilePath,
		string? targetName,
		out MarkdownHtmlCopyContent content,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			content = EmptyContent;
			return false;
		}

		content = CreateFileHtmlCopyContent(markdownFilePath, profile, theme, options);
		return true;
	}

	public static MarkdownHtmlCopyContent CreateFileHtmlCopyContent(
		string markdownFilePath,
		CopyKind kind,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return CreateFileHtmlCopyContent(markdownFilePath, kind, MarkdownExportStyle.Resolve(themeName, typographySize), options);
	}

	public static MarkdownHtmlCopyContent CreateFileHtmlCopyContent(
		string markdownFilePath,
		CopyKind kind,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return MarkdownSocialCopyRenderer.RenderFile(markdownFilePath, kind, theme, options);
	}

	public static MarkdownHtmlCopyContent CreateFileHtmlCopyContent(string markdownFilePath, MarkdownSocialCopyProfile profile)
	{
		return CreateFileHtmlCopyContent(markdownFilePath, profile, (MarkdownExportStyle?)null);
	}

	public static MarkdownHtmlCopyContent CreateFileHtmlCopyContent(
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		string? themeName,
		string? typographySize = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return CreateFileHtmlCopyContent(markdownFilePath, profile, MarkdownExportStyle.Resolve(themeName, typographySize), options);
	}

	public static MarkdownHtmlCopyContent CreateFileHtmlCopyContent(
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyOptions? options = null)
	{
		return MarkdownSocialCopyRenderer.RenderFile(markdownFilePath, profile, theme, options);
	}

	/// <summary>
	/// Builds an Avalonia data transfer object containing text/plain, text/html,
	/// macOS public.html, and Windows CF_HTML formats.
	/// </summary>
	public static DataTransfer CreateHtmlDataTransfer(string html, string? text = null)
	{
		ArgumentNullException.ThrowIfNull(html);

		var clipboardHtml = NormalizeHtmlForClipboard(html);
		var item = new DataTransferItem();
		item.SetText(text ?? MarkdownParser.Parse(MarkdownHtmlConverter.Html2Markdown(html), ClipboardPipeline).PlainText);
		item.Set(HtmlMimeFormat, clipboardHtml);
		item.Set(MacHtmlFormat, clipboardHtml);
		item.Set(WindowsHtmlFormat, BuildWindowsClipboardHtmlBytes(clipboardHtml));

		var transfer = new DataTransfer();
		transfer.Add(item);
		return transfer;
	}

	public static DataTransfer CreateHtmlDataTransfer(MarkdownHtmlCopyContent content)
	{
		ArgumentNullException.ThrowIfNull(content);

		return CreateHtmlDataTransfer(content.Html, content.Text);
	}

	/// <summary>
	/// Ensures the HTML document contains CF_HTML fragment markers.
	/// </summary>
	public static string NormalizeHtmlForClipboard(string html)
	{
		ArgumentNullException.ThrowIfNull(html);

		if (HasValidFragmentMarkers(html))
		{
			return html;
		}

		if (TryInsertBodyFragmentMarkers(html, out var markedHtml))
		{
			return markedHtml;
		}

		return $$"""
			<!doctype html>
			<html>
			<body>
			{{StartFragmentMarker}}
			{{html}}
			{{EndFragmentMarker}}
			</body>
			</html>
			""";
	}

	/// <summary>
	/// Builds the Windows CF_HTML string. Offsets are UTF-8 byte offsets from the
	/// beginning of the payload, as required by the native clipboard format.
	/// </summary>
	public static string BuildWindowsClipboardHtml(string html)
	{
		ArgumentNullException.ThrowIfNull(html);

		var clipboardHtml = NormalizeHtmlForClipboard(html);
		var startMarkerIndex = clipboardHtml.IndexOf(StartFragmentMarker, StringComparison.Ordinal);
		var endMarkerIndex = clipboardHtml.IndexOf(EndFragmentMarker, StringComparison.Ordinal);
		if (startMarkerIndex < 0 || endMarkerIndex < 0 || endMarkerIndex < startMarkerIndex)
		{
			throw new InvalidOperationException("HTML clipboard content must contain a valid fragment marker pair.");
		}

		const string HeaderFormat = "Version:1.0\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\nStartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";

		var blankHeader = string.Format(CultureInfo.InvariantCulture, HeaderFormat, 0, 0, 0, 0);
		var startHtml = Encoding.UTF8.GetByteCount(blankHeader);
		var endHtml = startHtml + Encoding.UTF8.GetByteCount(clipboardHtml);
		var startFragment = startHtml + Encoding.UTF8.GetByteCount(clipboardHtml[..(startMarkerIndex + StartFragmentMarker.Length)]);
		var endFragment = startHtml + Encoding.UTF8.GetByteCount(clipboardHtml[..endMarkerIndex]);
		var header = string.Format(CultureInfo.InvariantCulture, HeaderFormat, startHtml, endHtml, startFragment, endFragment);
		return header + clipboardHtml;
	}

	/// <summary>
	/// Builds UTF-8 bytes for Windows CF_HTML. Windows HTML Format is a byte
	/// clipboard format, not a UTF-16 text format.
	/// </summary>
	public static byte[] BuildWindowsClipboardHtmlBytes(string html)
	{
		return Encoding.UTF8.GetBytes(BuildWindowsClipboardHtml(html));
	}

	private static bool HasValidFragmentMarkers(string html)
	{
		var startMarkerIndex = html.IndexOf(StartFragmentMarker, StringComparison.Ordinal);
		var endMarkerIndex = html.IndexOf(EndFragmentMarker, StringComparison.Ordinal);
		return startMarkerIndex >= 0 && endMarkerIndex > startMarkerIndex;
	}

	private static readonly MarkdownHtmlCopyContent EmptyContent = new(string.Empty, string.Empty);

	private static bool TryInsertBodyFragmentMarkers(string html, out string markedHtml)
	{
		markedHtml = string.Empty;

		var bodyStartIndex = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
		var bodyEndIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
		if (bodyStartIndex < 0 || bodyEndIndex <= bodyStartIndex)
		{
			return false;
		}

		var bodyOpenEndIndex = html.IndexOf('>', bodyStartIndex);
		if (bodyOpenEndIndex < 0 || bodyOpenEndIndex >= bodyEndIndex)
		{
			return false;
		}

		var builder = new StringBuilder(html.Length + StartFragmentMarker.Length + EndFragmentMarker.Length + 12);
		builder.Append(html, 0, bodyOpenEndIndex + 1);
		builder.AppendLine();
		builder.AppendLine(StartFragmentMarker);
		builder.Append(html, bodyOpenEndIndex + 1, bodyEndIndex - bodyOpenEndIndex - 1);
		builder.AppendLine();
		builder.AppendLine(EndFragmentMarker);
		builder.Append(html, bodyEndIndex, html.Length - bodyEndIndex);
		markedHtml = builder.ToString();
		return true;
	}

	private static IClipboard ResolveClipboard()
	{
		var lifetime = Application.Current?.ApplicationLifetime;
		if (lifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } desktopClipboard })
		{
			return desktopClipboard;
		}

		if (lifetime is ISingleViewApplicationLifetime { MainView: { } mainView }
		    && TopLevel.GetTopLevel(mainView)?.Clipboard is { } singleViewClipboard)
		{
			return singleViewClipboard;
		}

		throw new InvalidOperationException("Could not resolve the current Avalonia clipboard. Pass IClipboard explicitly.");
	}
}
