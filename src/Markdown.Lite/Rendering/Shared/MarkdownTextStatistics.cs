using System.Text;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 文档文本统计：CJK 按字计数、拉丁按词计数，并统计段落数、标题数与阅读时长。
/// </summary>
public sealed record MarkdownTextStatistics(
	int Words,
	int Characters,
	int Lines,
	int Paragraphs,
	int Headings,
	int ReadingMinutes)
{
	public static MarkdownTextStatistics Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

/// <summary>
/// 基于已解析文档模型的文本统计，正文口径与渲染路径一致
/// （代码块/表格/公式走同一纯文本提取）。
/// </summary>
public static class MarkdownTextStatisticsCalculator
{
	/// <summary>阅读速度：每分钟 220 个计数单位（CJK 字或拉丁词）。</summary>
	public const double WordsPerMinute = 220d;

	public static MarkdownTextStatistics Calculate(MarkdownDocumentModel? model)
	{
		if (model is null || model.Source.Length == 0)
		{
			return MarkdownTextStatistics.Empty;
		}

		var body = CollectBodyText(model);
		var (words, characters) = CountText(body);
		var paragraphs = CountParagraphs(model);
		var headings = model.Blocks.Count(block => block.Kind == MarkdownBlockKind.Heading);
		var readingMinutes = words == 0
			? 0
			: Math.Max(1, (int)Math.Ceiling(words / WordsPerMinute));

		return new MarkdownTextStatistics(
			words,
			characters,
			CountLines(model.Source),
			paragraphs,
			headings,
			readingMinutes);
	}

	private static string CollectBodyText(MarkdownDocumentModel model)
	{
		var builder = new StringBuilder();
		foreach (var block in model.Blocks)
		{
			if (block.PlainText.Length == 0)
			{
				continue;
			}

			if (builder.Length > 0)
			{
				builder.AppendLine();
			}

			builder.Append(block.PlainText);
		}

		return builder.ToString();
	}

	private static int CountParagraphs(MarkdownDocumentModel model)
	{
		var count = 0;
		foreach (var block in model.Blocks)
		{
			count += block.Kind switch
			{
				MarkdownBlockKind.Paragraph or MarkdownBlockKind.Image => 1,
				MarkdownBlockKind.Quote => CountContainerParagraphs(block.SyntaxBlock),
				MarkdownBlockKind.List => CountListItemParagraphs(block.SyntaxBlock),
				_ => 0
			};
		}

		return count;
	}

	private static int CountContainerParagraphs(Markdig.Syntax.Block block)
	{
		if (block is not Markdig.Syntax.ContainerBlock container)
		{
			return 0;
		}

		var count = 0;
		foreach (var child in container)
		{
			count += child switch
			{
				Markdig.Syntax.ParagraphBlock => 1,
				Markdig.Syntax.ContainerBlock nested => CountContainerParagraphs(nested),
				_ => 0
			};
		}

		return count;
	}

	private static int CountListItemParagraphs(Markdig.Syntax.Block block)
	{
		if (block is not Markdig.Syntax.ContainerBlock container)
		{
			return 0;
		}

		var count = 0;
		foreach (var child in container)
		{
			count += CountContainerParagraphs(child);
		}

		return Math.Max(count, 1);
	}

	private static (int Words, int Characters) CountText(string text)
	{
		var words = 0;
		var characters = 0;
		var inLatinWord = false;

		foreach (var character in text)
		{
			if (IsMarkdownSyntaxCharacter(character))
			{
				inLatinWord = false;
				continue;
			}

			characters++;

			if (IsCjkCharacter(character))
			{
				words++;
				inLatinWord = false;
				continue;
			}

			if (char.IsLetterOrDigit(character))
			{
				if (!inLatinWord)
				{
					words++;
					inLatinWord = true;
				}

				continue;
			}

			inLatinWord = false;
		}

		return (words, characters);
	}

	private static int CountLines(string source)
	{
		if (source.Length == 0)
		{
			return 0;
		}

		var lines = 1;
		for (var index = 0; index < source.Length; index++)
		{
			if (source[index] != '\n')
			{
				continue;
			}

			lines++;
		}

		return lines;
	}

	private static bool IsMarkdownSyntaxCharacter(char character)
	{
		return character is '`' or '*' or '_' or '>' or '#' or '-' or '[' or ']' or '(' or ')' or '!' or '|';
	}

	private static bool IsCjkCharacter(char character)
	{
		return character is >= '\u3400' and <= '\u9FFF';
	}
}
