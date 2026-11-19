namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// HTML content prepared for rich clipboard copy. Text is the plain-text fallback.
/// </summary>
public sealed record MarkdownHtmlCopyContent(string Text, string Html);
