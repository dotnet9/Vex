namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Describes a social editor copy profile. Applications can pass custom profiles
/// to reuse the renderer with their own target name or suffix behavior.
/// </summary>
public sealed record MarkdownSocialCopyProfile(
	string TargetName,
	MarkdownSocialCopyFormat Format)
{
	public bool AppendSuffix { get; init; }

	public string SuffixContainerId { get; init; } = "vex-suffix-juejin-container";

	public string SuffixContainerClass { get; init; } = "vex-suffix-juejin-container";

	public string SuffixLinkText { get; init; } = "codewf.com";

	public string SuffixLinkUrl { get; init; } = "https://codewf.com";
}
