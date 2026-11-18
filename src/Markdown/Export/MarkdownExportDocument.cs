namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Describes a Markdown document that can be exported by CodeWF.Avalonia.Markdown.
/// </summary>
public sealed record MarkdownExportDocument
{
	public MarkdownExportDocument(string? markdown, string? filePath = null, string? fileName = null)
	{
		Markdown = markdown ?? string.Empty;
		FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
		FileName = ResolveFileName(filePath, fileName);
	}

	public string Markdown { get; }

	public string? FilePath { get; }

	public string FileName { get; }

	private static string ResolveFileName(string? filePath, string? fileName)
	{
		if (!string.IsNullOrWhiteSpace(fileName))
		{
			return fileName;
		}

		if (!string.IsNullOrWhiteSpace(filePath))
		{
			var resolved = Path.GetFileName(filePath);
			if (!string.IsNullOrWhiteSpace(resolved))
			{
				return resolved;
			}
		}

		return "document.md";
	}
}

