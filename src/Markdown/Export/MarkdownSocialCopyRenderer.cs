using System.Globalization;
using System.Net;
using System.Text;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using CodeWF.Toolkit.Lang;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Renders Markdown as inline-styled HTML that can be pasted into social editors
/// such as WeChat Official Account, Zhihu, and Juejin.
/// </summary>
public static class MarkdownSocialCopyRenderer
{
	private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.Build();

	public static MarkdownHtmlCopyContent RenderMarkdown(
		string markdown,
		CopyKind target,
		MarkdownExportStyle? style = null,
		string? documentPath = null,
		string? fileName = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return Render(
			new MarkdownExportDocument(markdown, documentPath, fileName),
			target,
			style,
			options);
	}

	public static MarkdownHtmlCopyContent RenderMarkdown(
		string markdown,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? style = null,
		string? documentPath = null,
		string? fileName = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return Render(
			new MarkdownExportDocument(markdown, documentPath, fileName),
			profile,
			style,
			options);
	}

	public static MarkdownHtmlCopyContent RenderFile(
		string markdownFilePath,
		CopyKind target,
		MarkdownExportStyle? style = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return Render(CreateDocumentFromFile(markdownFilePath), target, style, options);
	}

	public static MarkdownHtmlCopyContent RenderFile(
		string markdownFilePath,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? style = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return Render(CreateDocumentFromFile(markdownFilePath), profile, style, options);
	}

	public static MarkdownHtmlCopyContent Render(
		MarkdownExportDocument document,
		CopyKind target,
		MarkdownExportStyle? style = null,
		MarkdownSocialCopyOptions? options = null)
	{
		return Render(document, MarkdownSocialCopyProfiles.Resolve(target), style, options);
	}

	public static MarkdownHtmlCopyContent Render(
		MarkdownExportDocument document,
		MarkdownSocialCopyProfile profile,
		MarkdownExportStyle? style = null,
		MarkdownSocialCopyOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(profile);

		var resolvedStyle = style ?? MarkdownExportStyle.Resolve(null, null);
		var resolvedOptions = options ?? new MarkdownSocialCopyOptions();
		var title = WebUtility.HtmlEncode(resolvedOptions.Title ?? Path.GetFileNameWithoutExtension(document.FileName));
		var language = WebUtility.HtmlEncode(ResolveLanguage(resolvedOptions));
		var targetName = WebUtility.HtmlEncode(profile.TargetName);
		var startFragment = resolvedOptions.IncludeFragmentMarkers ? MarkdownHtmlClipboard.StartFragmentMarker : string.Empty;
		var endFragment = resolvedOptions.IncludeFragmentMarkers ? MarkdownHtmlClipboard.EndFragmentMarker : string.Empty;
		var section = RenderSocialSection(document, resolvedStyle, profile, resolvedOptions);
		var html = $$"""
			<!doctype html>
			<html lang="{{language}}">
			<head>
			  <meta charset="utf-8">
			  <meta name="codewf-markdown-copy-target" content="{{targetName}}">
			  <title>{{title}}</title>
			</head>
			<body>
			{{startFragment}}
			{{section}}
			{{endFragment}}
			</body>
			</html>
			""";

        var plainText = MarkdownParser.Parse(document.Markdown, resolvedOptions.SyntaxOptions?.CreatePipeline() ?? Pipeline).PlainText;
		return new MarkdownHtmlCopyContent(plainText, html);
	}

	private static MarkdownExportDocument CreateDocumentFromFile(string markdownFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(markdownFilePath);

		return new MarkdownExportDocument(
			File.ReadAllText(markdownFilePath),
			markdownFilePath);
	}

	private static string RenderSocialSection(
		MarkdownExportDocument document,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
        var parsed = Markdig.Markdown.Parse(document.Markdown, options.SyntaxOptions?.CreatePipeline() ?? Pipeline);
		EmbedLocalImages(parsed, document.FilePath);
		var body = RenderSocialBlocks(parsed, style, profile, options);
		if (profile.AppendSuffix)
		{
			body = string.IsNullOrWhiteSpace(body)
				? RenderSuffix(style, profile, options)
				: $"{body}{Environment.NewLine}{RenderSuffix(style, profile, options)}";
		}

		var toolName = WebUtility.HtmlEncode(ResolveToolName(options));
		var website = WebUtility.HtmlEncode(options.Website);
		return $$"""
			<section id="codewf-markdown" data-tool="{{toolName}}" data-website="{{website}}" style="{{BuildSocialRootStyle(style)}}">
			{{body}}
			</section>
			""";
	}

	private static string RenderSocialBlocks(
		IEnumerable<Block> blocks,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var builder = new StringBuilder();
		foreach (var block in blocks)
		{
			var html = RenderSocialBlock(block, style, profile, options);
			if (!string.IsNullOrWhiteSpace(html))
			{
				builder.AppendLine(html);
			}
		}

		return builder.ToString().TrimEnd();
	}

	private static string RenderSocialBlock(
		Block block,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		return block switch
		{
			HeadingBlock heading => RenderSocialHeading(heading, style, profile, options),
			ParagraphBlock paragraph => RenderSocialParagraph(paragraph, style, profile, options),
			ListBlock list => RenderSocialList(list, style, profile, options),
			QuoteBlock quote => RenderSocialQuote(quote, style, profile, options),
			CodeBlock codeBlock => RenderSocialCodeBlock(codeBlock, style),
			ThematicBreakBlock => $"""<hr style="height: 1px; border: none; border-top: 1px solid {style.BorderColor}; margin: 24px 0;" />""",
			Table table => RenderSocialTable(table, style, profile, options),
			HtmlBlock htmlBlock => options.AllowRawHtml
				? htmlBlock.Lines.ToString()
				: WebUtility.HtmlEncode(htmlBlock.Lines.ToString()),
			ContainerBlock container => RenderSocialBlocks(container, style, profile, options),
			_ => string.Empty
		};
	}

	private static string RenderSocialHeading(
		HeadingBlock heading,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var content = RenderSocialInlines(heading.Inline, style, profile, options);
		var toolName = WebUtility.HtmlEncode(ResolveToolName(options));
		var level = Math.Clamp(heading.Level, 1, 6);
		if (level == 2)
		{
			return profile.Format switch
			{
				MarkdownSocialCopyFormat.Mountain => $$"""
					<h2 data-tool="{{toolName}}" style="font-weight: bold; color: {{style.HeadingColor}}; font-size: {{FormatCssNumber(style.Heading2FontSize)}}px; display: block; text-align: center; background-image: url(https://my-wechat.mdvex.com/mdvex/mountain_2_20191028221337.png); background-position: center center; background-repeat: no-repeat; background-attachment: initial; background-origin: initial; background-clip: initial; background-size: 63px; margin-top: 38px; margin-bottom: 10px;"><span class="prefix" style="display: none;"></span><span class="content" style="text-align: center; display: inline-block; height: 38px; line-height: 42px; color: {{style.HeadingColor}}; background-position: left center; background-repeat: no-repeat; background-attachment: initial; background-origin: initial; background-clip: initial; background-size: 63px; margin-top: 38px; font-size: {{FormatCssNumber(style.Heading3FontSize)}}px; margin-bottom: 10px;">{{content}}</span><span class="suffix"></span></h2>
					""",
				_ => $$"""
					<h2 data-tool="{{toolName}}" style="margin-top: 30px; font-weight: bold; font-size: {{FormatCssNumber(style.Heading2FontSize)}}px; border-bottom: 2px solid {{style.BorderColor}}; margin-bottom: 30px; color: {{style.HeadingColor}};"><span class="prefix" style="display: none;"></span><span class="content" style="font-size: {{FormatCssNumber(style.Heading2FontSize)}}px; display: inline-block; border-bottom: 2px solid {{style.HeadingColor}};">{{content}}</span><span class="suffix"></span></h2>
					"""
			};
		}

		var fontSize = level switch
		{
			1 => style.Heading1FontSize,
			3 => style.Heading3FontSize,
			4 => style.Heading4FontSize,
			5 => style.Heading5FontSize,
			_ => style.Heading6FontSize
		};
		return $$"""
			<h{{level}} data-tool="{{toolName}}" style="margin-top: 26px; margin-bottom: 16px; font-weight: bold; font-size: {{FormatCssNumber(fontSize)}}px; line-height: 1.35; color: {{style.HeadingColor}};">{{content}}</h{{level}}>
			""";
	}

	private static string RenderSocialParagraph(
		ParagraphBlock paragraph,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var content = RenderSocialInlines(paragraph.Inline, style, profile, options);
		if (string.IsNullOrWhiteSpace(content))
		{
			return string.Empty;
		}

		var toolName = WebUtility.HtmlEncode(ResolveToolName(options));
		return $"""<p data-tool="{toolName}" style="{BuildSocialParagraphStyle(style)}">{content}</p>""";
	}

	private static string RenderSocialList(
		ListBlock list,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var tag = list.IsOrdered ? "ol" : "ul";
		var builder = new StringBuilder();
		builder.Append($"""<{tag} style="margin: 8px 0 16px; padding-left: 24px; color: {style.BodyColor}; font-size: {FormatCssNumber(style.BodyFontSize)}px; line-height: {FormatCssNumber(style.LineHeightRatio + 0.2d)};">""");
		foreach (var item in list.OfType<ListItemBlock>())
		{
			builder.Append("<li style=\"margin: 4px 0; padding-left: 2px;\">");
			builder.Append(RenderSocialBlocks(item, style, profile, options));
			builder.Append("</li>");
		}

		builder.Append($"</{tag}>");
		return builder.ToString();
	}

	private static string RenderSocialQuote(
		QuoteBlock quote,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var body = RenderSocialBlocks(quote, style, profile, options);
		return $$"""
			<blockquote style="margin: 14px 0; padding: 8px 14px; border-left: 4px solid {{style.QuoteBorderColor}}; background: {{style.QuoteBackgroundColor}}; color: {{style.MutedColor}};">{{body}}</blockquote>
			""";
	}

	private static string RenderSocialCodeBlock(CodeBlock codeBlock, MarkdownExportStyle style)
	{
		var code = WebUtility.HtmlEncode(codeBlock.Lines.ToString().TrimEnd());
		return $$"""
			<pre style="margin: 14px 0; padding: 14px; border-radius: 6px; background: {{style.CodeBackgroundColor}}; color: {{style.CodeForegroundColor}}; overflow: auto; font-size: {{FormatCssNumber(style.CodeFontSize)}}px; line-height: 1.55;"><code style="font-family: {{FormatCssFontFamily(style.MonoFontFamily)}}; background: transparent; color: inherit; padding: 0;">{{code}}</code></pre>
			""";
	}

	private static string RenderSocialTable(
		Table table,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var builder = new StringBuilder();
		builder.Append($"""<table style="border-collapse: collapse; width: 100%; margin: 14px 0; font-size: {FormatCssNumber(style.TableFontSize)}px; color: {style.BodyColor};">""");
		foreach (var row in table.OfType<TableRow>())
		{
			builder.Append("<tr>");
			foreach (var cell in row.OfType<TableCell>())
			{
				var tag = row.IsHeader ? "th" : "td";
				var background = row.IsHeader ? $" background: {style.TableHeaderBackgroundColor};" : string.Empty;
				builder.Append($"""<{tag} style="border: 1px solid {style.BorderColor}; padding: 8px 10px; text-align: left;{background}">""");
				builder.Append(RenderSocialBlocks(cell, style, profile, options));
				builder.Append($"</{tag}>");
			}

			builder.Append("</tr>");
		}

		builder.Append("</table>");
		return builder.ToString();
	}

	private static string RenderSocialInlines(
		ContainerInline? container,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		if (container is null)
		{
			return string.Empty;
		}

		var builder = new StringBuilder();
		var inline = container.FirstChild;
		while (inline is not null)
		{
			builder.Append(RenderSocialInline(inline, style, profile, options));
			inline = inline.NextSibling;
		}

		return builder.ToString();
	}

	private static string RenderSocialInline(
		MarkdigInline inline,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		return inline switch
		{
			LiteralInline literal => WebUtility.HtmlEncode(literal.Content.ToString()),
			LineBreakInline => "<br />",
			CodeInline code => $"""<code style="font-family: {FormatCssFontFamily(style.MonoFontFamily)}; font-size: {FormatCssNumber(style.CodeFontSize)}px; color: {style.InlineCodeForegroundColor}; background: {style.InlineCodeBackgroundColor}; padding: 2px 4px; border-radius: 4px;">{WebUtility.HtmlEncode(code.Content)}</code>""",
			EmphasisInline emphasis => RenderSocialEmphasis(emphasis, style, profile, options),
			LinkInline { IsImage: true } image => RenderSocialImage(image, style, profile, options),
			LinkInline link => RenderSocialLink(link, style, profile, options),
			TaskList taskList => taskList.Checked ? "[x] " : "[ ] ",
			HtmlInline htmlInline => options.AllowRawHtml
				? htmlInline.Tag
				: WebUtility.HtmlEncode(htmlInline.Tag),
			ContainerInline nested => RenderSocialInlines(nested, style, profile, options),
			_ => WebUtility.HtmlEncode(inline.ToString() ?? string.Empty)
		};
	}

	private static string RenderSocialEmphasis(
		EmphasisInline emphasis,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var content = RenderSocialInlines(emphasis, style, profile, options);
		return emphasis.DelimiterCount >= 2
			? $"<strong style=\"font-weight: bold;\">{content}</strong>"
			: $"<em style=\"font-style: italic;\">{content}</em>";
	}

	private static string RenderSocialLink(
		LinkInline link,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var content = RenderSocialInlines(link, style, profile, options);
		if (!TryGetSafeUri(link.Url, allowDataImage: false, out var safeUrl))
		{
			return content;
		}

		var url = WebUtility.HtmlEncode(safeUrl);
		return $"""<a href="{url}" style="{BuildSocialLinkStyle(style)}">{content}</a>""";
	}

	private static string RenderSocialImage(
		LinkInline image,
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		if (!TryGetSafeUri(image.Url, allowDataImage: true, out var safeUrl))
		{
			return WebUtility.HtmlEncode(RenderSocialInlines(image, style, profile, options));
		}

		var url = WebUtility.HtmlEncode(safeUrl);
		var alt = WebUtility.HtmlEncode(RenderSocialInlines(image, style, profile, options));
		return $"""<img src="{url}" alt="{alt}" style="max-width: 100%; display: block; margin: 14px auto;" />""";
	}

	private static bool TryGetSafeUri(string? value, bool allowDataImage, out string safeUri)
	{
		safeUri = string.Empty;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var trimmed = value.Trim();
		if (allowDataImage && trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
		{
			safeUri = trimmed;
			return true;
		}

		if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
		{
			if (uri.Scheme is not ("http" or "https" or "mailto"))
			{
				return false;
			}

			safeUri = trimmed;
			return true;
		}

		if (trimmed.StartsWith('#') || !trimmed.Contains(':'))
		{
			safeUri = trimmed;
			return true;
		}

		return false;
	}

	private static string BuildSocialRootStyle(MarkdownExportStyle style)
	{
		return $"font-size: {FormatCssNumber(style.BodyFontSize)}px; color: {style.BodyColor}; background: {style.PageBackgroundColor}; padding: 25px 30px; line-height: {FormatCssNumber(style.LineHeightRatio + 0.2d)}; word-spacing: 0px; letter-spacing: 0px; word-break: break-word; word-wrap: break-word; text-align: justify; font-family: {FormatCssFontFamily(style.BodyFontFamily)}; margin-top: -10px;";
	}

	private static string BuildSocialParagraphStyle(MarkdownExportStyle style)
	{
		var lineHeight = style.BodyFontSize * (style.LineHeightRatio + 0.2d);
		return $"font-size: {FormatCssNumber(style.BodyFontSize)}px; padding-top: 8px; padding-bottom: 8px; margin: 0; line-height: {FormatCssNumber(lineHeight)}px; color: {style.BodyColor};";
	}

	private static string BuildSocialLinkStyle(MarkdownExportStyle style)
	{
		return $"word-wrap: break-word; color: {style.LinkColor}; text-decoration: none; border-bottom: 1px solid {style.LinkColor};";
	}

	private static string RenderSuffix(
		MarkdownExportStyle style,
		MarkdownSocialCopyProfile profile,
		MarkdownSocialCopyOptions options)
	{
		var id = WebUtility.HtmlEncode(profile.SuffixContainerId);
		var className = WebUtility.HtmlEncode(profile.SuffixContainerClass);
		var toolName = WebUtility.HtmlEncode(ResolveToolName(options));
		var linkText = WebUtility.HtmlEncode(profile.SuffixLinkText);
		var linkUrl = WebUtility.HtmlEncode(profile.SuffixLinkUrl);
		var linkHtml = $"""<a href="{linkUrl}" style="{BuildSocialLinkStyle(style)} font-weight: bold;">{linkText}</a>""";
		var suffixContent = FormatSuffix(ResolveSuffixFormat(options), linkHtml);
		return $"""
			<p id="{id}" class="{className}" data-tool="{toolName}" style="{BuildSocialParagraphStyle(style)} margin-top: 20px !important;">{suffixContent}</p>
			""";
	}

	private static string ResolveToolName(MarkdownSocialCopyOptions options)
	{
		return ResolveLocalizedOption(
			options.ToolName,
			global::CodeWF.Avalonia.Markdown.MarkdownL.SocialCopyToolName,
			"Markdown editor");
	}

	private static string ResolveLanguage(MarkdownSocialCopyOptions options)
	{
		if (!string.IsNullOrWhiteSpace(options.Language))
		{
			return options.Language;
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

	private static string ResolveSuffixFormat(MarkdownSocialCopyOptions options)
	{
		return ResolveLocalizedOption(
			options.SuffixFormat,
			global::CodeWF.Avalonia.Markdown.MarkdownL.SocialCopySuffixFormat,
			"Formatted with {0}");
	}

	private static string ResolveLocalizedOption(string? value, string resourceKey, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		try
		{
			var localized = MarkdownLocalization.GetResource(resourceKey);
			if (!string.IsNullOrWhiteSpace(localized)
			    && !string.Equals(localized, resourceKey, StringComparison.Ordinal))
			{
				return localized;
			}
		}
		catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentException)
		{
			return fallback;
		}

		return fallback;
	}

	private static string FormatSuffix(string format, string linkHtml)
	{
		try
		{
			return string.Format(CultureInfo.CurrentCulture, format, linkHtml);
		}
		catch (FormatException)
		{
			return $"{WebUtility.HtmlEncode(format)} {linkHtml}";
		}
	}

	private static string FormatCssNumber(double value)
	{
		return value.ToString("0.##", CultureInfo.InvariantCulture);
	}

	private static string FormatCssFontFamily(string fontFamily)
	{
		var families = fontFamily.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(FormatCssFontName);
		return string.Join(", ", families);
	}

	private static string FormatCssFontName(string fontName)
	{
		if (fontName.Contains('\'') || fontName.Contains('"'))
		{
			return fontName;
		}

		return fontName.Any(char.IsWhiteSpace)
			? $"'{fontName}'"
			: fontName;
	}

	private static void EmbedLocalImages(ContainerBlock container, string? documentPath)
	{
		foreach (var block in container)
		{
			if (block is LeafBlock { Inline: { } inline })
			{
				EmbedLocalImages(inline, documentPath);
			}

			if (block is ContainerBlock childContainer)
			{
				EmbedLocalImages(childContainer, documentPath);
			}
		}
	}

	private static void EmbedLocalImages(ContainerInline container, string? documentPath)
	{
		foreach (var inline in container)
		{
			if (inline is LinkInline { IsImage: true } image
			    && TryCreateImageDataUri(image.Url, documentPath, out var dataUri))
			{
				image.Url = dataUri;
			}

			if (inline is ContainerInline childContainer)
			{
				EmbedLocalImages(childContainer, documentPath);
			}
		}
	}

	private static bool TryCreateImageDataUri(string? url, string? documentPath, out string dataUri)
	{
		dataUri = string.Empty;
		if (!TryResolveLocalImagePath(url, documentPath, out var path))
		{
			return false;
		}

	try
		{
			var imageSource = MarkdownImageSourceLoader.Load(
				path,
				maxLocalImageBytes: MarkdownImageSourceLoader.DefaultMaxRemoteImageBytes);
			var bytes = imageSource.Bytes;
			dataUri = $"data:{ResolveImageMediaType(path)};base64,{Convert.ToBase64String(bytes)}";
			return true;
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
		{
			return false;
		}
	}

	private static bool TryResolveLocalImagePath(string? url, string? documentPath, out string path)
	{
		path = string.Empty;
		if (string.IsNullOrWhiteSpace(url)
		    || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			if (!uri.IsFile)
			{
				return false;
			}

			path = uri.LocalPath;
			return File.Exists(path);
		}

		foreach (var candidate in EnumerateLocalImagePathCandidates(url))
		{
			if (Path.IsPathRooted(candidate))
			{
				path = candidate;
				if (File.Exists(path))
				{
					return true;
				}
			}
		}

		var baseDirectory = ResolveImageBaseDirectory(documentPath);
		if (string.IsNullOrWhiteSpace(baseDirectory))
		{
			return false;
		}

		foreach (var candidate in EnumerateLocalImagePathCandidates(url))
		{
			path = Path.GetFullPath(Path.Combine(baseDirectory, candidate));
			if (File.Exists(path))
			{
				return true;
			}
		}

		path = string.Empty;
		return false;
	}

	private static string? ResolveImageBaseDirectory(string? documentPath)
	{
		if (string.IsNullOrWhiteSpace(documentPath))
		{
			return Directory.GetCurrentDirectory();
		}

		if (Directory.Exists(documentPath))
		{
			return documentPath;
		}

		var directory = Path.GetDirectoryName(documentPath);
		return string.IsNullOrWhiteSpace(directory)
			? Directory.GetCurrentDirectory()
			: directory;
	}

	private static IEnumerable<string> EnumerateLocalImagePathCandidates(string url)
	{
		var normalized = url.Replace('/', Path.DirectorySeparatorChar);
		yield return normalized;

		var decoded = DecodeLocalImageUrl(normalized);
		if (!string.Equals(decoded, normalized, StringComparison.Ordinal))
		{
			yield return decoded;
		}
	}

	private static string DecodeLocalImageUrl(string url)
	{
		try
		{
			return Uri.UnescapeDataString(url);
		}
		catch (UriFormatException)
		{
			return url;
		}
	}

	private static string ResolveImageMediaType(string path)
	{
		return Path.GetExtension(path).ToLowerInvariant() switch
		{
			".svg" => "image/svg+xml",
			".gif" => "image/gif",
			".png" => "image/png",
			".jpg" or ".jpeg" => "image/jpeg",
			".webp" => "image/webp",
			".bmp" => "image/bmp",
			".avif" => "image/avif",
			_ => "application/octet-stream"
		};
	}
}
