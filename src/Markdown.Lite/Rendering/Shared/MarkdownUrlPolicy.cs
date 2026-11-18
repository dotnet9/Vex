using System.Diagnostics;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// Applies the common external-link policy used by the Full and Lite viewers.
/// </summary>
internal static class MarkdownUrlPolicy
{
	public static bool IsAllowedExternalUri(string? url)
	{
		return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
			&& uri.Scheme is "http" or "https" or "mailto";
	}

	/// <summary>
	/// 链接文字是否疑似伪装：文字本身像 URL（scheme:// 或 www. 开头），
	/// 但其主机与实际目标主机不一致——典型钓鱼形态
	/// <c>[https://github.com](https://evil.example)</c>。
	/// 普通文字链接、同主机不同路径均不触发。
	/// </summary>
	public static bool IsMisleadingLinkText(string? linkText, string? url)
	{
		if (string.IsNullOrWhiteSpace(linkText) || string.IsNullOrWhiteSpace(url))
		{
			return false;
		}

		var text = linkText.Trim();
		var looksLikeUrl = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			|| text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
			|| text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
		if (!looksLikeUrl)
		{
			return false;
		}

		if (!Uri.TryCreate(text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + text : text, UriKind.Absolute, out var textUri)
			|| !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var targetUri))
		{
			return false;
		}

		return !string.Equals(textUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase);
	}

	public static void Open(string? url)
	{
		if (!IsAllowedExternalUri(url))
		{
			return;
		}

		try
		{
			Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
		}
		catch
		{
			// Link activation must not interrupt Markdown rendering.
		}
	}
}
