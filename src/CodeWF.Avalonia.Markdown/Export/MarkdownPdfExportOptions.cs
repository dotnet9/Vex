namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Controls PDF export metadata.
/// </summary>
public sealed record MarkdownPdfExportOptions(
	string DefaultHeading = "Markdown Document",
	string DefaultFileName = "document.md",
	string PageSize = "A4",
	double? MarginMillimeters = null,
	string? WatermarkText = null,
	double WatermarkOpacity = 0.12,
	MarkdownSyntaxOptions? SyntaxOptions = null);
