using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 大纲条目：标题层级（1-6）、标题纯文本、1 起始的源码行号与源码起始偏移
/// （<see cref="SourceOffset"/> 配合宿主跳转/锚点定位）。
/// </summary>
public sealed record MarkdownOutlineItem(int Level, string Title, int Line, int SourceOffset)
{
	/// <summary>层级章节序号（1、1.1、1.1.1）。Extract 不填写（保持纯提取），
	/// 宿主需要时经 <see cref="MarkdownOutlineNumbering.WithNumbers"/> 生成。</summary>
	public string Number { get; init; } = string.Empty;
}

/// <summary>
/// 从已解析文档模型提取标题大纲。基于 Markdig AST，
/// 代码块/HTML 块内的 "#" 不会被误判为标题。
/// </summary>
public static class MarkdownOutlineExtractor
{
	public static IReadOnlyList<MarkdownOutlineItem> Extract(MarkdownDocumentModel? model)
	{
		if (model is null || model.Blocks.Count == 0)
		{
			return [];
		}

		var items = new List<MarkdownOutlineItem>();
		var source = model.Source;
		foreach (var block in model.Blocks)
		{
			if (block.Kind != MarkdownBlockKind.Heading || block.SyntaxBlock is not HeadingBlock heading)
			{
				continue;
			}

			var title = heading.Inline is null
				? string.Empty
				: MarkdownPlainTextExtractor.ExtractPlainText(heading.Inline).Trim();
			if (title.Length == 0)
			{
				continue;
			}

			items.Add(new MarkdownOutlineItem(
				Math.Clamp(heading.Level, 1, 6),
				title,
				GetLine(source, block.SourceSpan.Start),
				block.SourceSpan.Start));
		}

		return items;
	}

	/// <summary>
	/// 文本级提取：管线解析优先；空结果时退化为逐行扫描（跳过围栏代码块），
	/// 保证宿主打开文档后大纲必有内容。行号 1 起与编辑器行号一致。
	/// </summary>
	public static IReadOnlyList<MarkdownOutlineItem> ExtractFromText(string markdown, Markdig.MarkdownPipeline pipeline)
	{
		if (string.IsNullOrEmpty(markdown))
		{
			return [];
		}

		var items = new List<MarkdownOutlineItem>();
		var source = markdown.Replace("\r\n", "\n");
		try
		{
			items.AddRange(Extract(MarkdownParser.Parse(source, pipeline)));
		}
		catch
		{
			// 解析失败不影响编辑，退化为行扫描。
		}

		if (items.Count == 0)
		{
			var inFence = false;
			var offset = 0; // 行首在整篇中的字符偏移，供 SourceOffset（与 AST 提取同口径）
			var lines = source.Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var lineOffset = offset;
				offset += line.Length + 1;
				var trimmed = line.TrimStart();
				if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
				{
					inFence = !inFence;
					continue;
				}
				if (inFence)
				{
					continue;
				}

				var match = System.Text.RegularExpressions.Regex.Match(line, "^(#{1,3})\\s+(.+?)\\s*#*\\s*$");
				if (!match.Success)
				{
					continue;
				}

				var title = match.Groups[2].Value.Trim();
				if (title.Length == 0)
				{
					continue;
				}

				items.Add(new MarkdownOutlineItem(match.Groups[1].Value.Length, title, i + 1, lineOffset));
			}
		}

		return items;
	}

	/// <summary>
	/// 返回第一个标题的文本，用于导出/打印的默认标题；无标题时返回 null。
	/// </summary>
	public static string? FindFirstHeading(MarkdownDocumentModel? model)
	{
		var items = Extract(model);
		return items.Count == 0 ? null : items[0].Title;
	}

	private static int GetLine(string source, int offset)
	{
		var line = 1;
		var limit = Math.Clamp(offset, 0, source.Length);
		for (var i = 0; i < limit; i++)
		{
			if (source[i] == '\n')
			{
				line++;
			}
		}

		return line;
	}
}
