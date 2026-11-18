namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Loaded Markdown image bytes plus the metadata needed by viewers and exporters.
/// </summary>
public sealed record MarkdownImageSource(
	byte[] Bytes,
	string FileName,
	bool IsSvg,
	bool IsGif,
	string? LocalPath);
