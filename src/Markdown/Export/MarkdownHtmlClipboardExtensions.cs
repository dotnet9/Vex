using global::Avalonia.Input.Platform;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Convenience methods for copying Markdown as rich HTML through an Avalonia clipboard.
/// </summary>
public static class MarkdownHtmlClipboardExtensions
{
	public static Task SetMarkdownHtmlAsync(
		this IClipboard clipboard,
		string markdown,
		MarkdownExportStyle? theme,
		CopyKind kind)
	{
		return MarkdownHtmlClipboard.SetHtmlAsync(
			clipboard,
			MarkdownSocialCopyRenderer.RenderMarkdown(
				markdown,
				kind,
				theme));
	}

	public static Task SetMarkdownHtmlAsync(
		this IClipboard clipboard,
		string markdown,
		string? themeName,
		CopyKind kind,
		string? typographySize = null)
	{
		return clipboard.SetMarkdownHtmlAsync(
			markdown,
			MarkdownExportStyle.Resolve(themeName, typographySize),
			kind);
	}

	public static Task SetMarkdownHtmlAsync(
		this IClipboard clipboard,
		string markdown,
		MarkdownExportStyle? theme,
		MarkdownSocialCopyProfile profile)
	{
		return MarkdownHtmlClipboard.SetHtmlAsync(
			clipboard,
			MarkdownSocialCopyRenderer.RenderMarkdown(
				markdown,
				profile,
				theme));
	}

	public static Task SetMarkdownHtmlAsync(
		this IClipboard clipboard,
		string markdown,
		string? themeName,
		MarkdownSocialCopyProfile profile,
		string? typographySize = null)
	{
		return clipboard.SetMarkdownHtmlAsync(
			markdown,
			MarkdownExportStyle.Resolve(themeName, typographySize),
			profile);
	}

	public static async Task<bool> TrySetMarkdownHtmlAsync(
		this IClipboard clipboard,
		string markdown,
		MarkdownExportStyle? theme,
		string? targetName)
	{
		if (!MarkdownSocialCopyProfiles.TryResolve(targetName, out var profile))
		{
			return false;
		}

		await clipboard.SetMarkdownHtmlAsync(
			markdown,
			theme,
			profile).ConfigureAwait(false);
		return true;
	}

	public static Task<bool> TrySetMarkdownHtmlAsync(
		this IClipboard clipboard,
		string markdown,
		string? themeName,
		string? targetName,
		string? typographySize = null)
	{
		return clipboard.TrySetMarkdownHtmlAsync(
			markdown,
			MarkdownExportStyle.Resolve(themeName, typographySize),
			targetName);
	}
}
