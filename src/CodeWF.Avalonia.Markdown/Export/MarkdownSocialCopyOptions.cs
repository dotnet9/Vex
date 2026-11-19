namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Options used when rendering Markdown for rich social-editor clipboard HTML.
/// </summary>
public sealed record MarkdownSocialCopyOptions
{
    public MarkdownSyntaxOptions? SyntaxOptions { get; init; }
	public string? Language { get; init; }

	public string? Title { get; init; }

	public bool IncludeFragmentMarkers { get; init; } = true;

	public string? ToolName { get; init; }

	public string? SuffixFormat { get; init; }

	public string Website { get; init; } = "https://codewf.com";

	/// <summary>
	/// Allows raw HTML blocks and inline tags to pass through to clipboard HTML.
	/// Keep disabled for untrusted Markdown.
	/// </summary>
	public bool AllowRawHtml { get; init; }
}
