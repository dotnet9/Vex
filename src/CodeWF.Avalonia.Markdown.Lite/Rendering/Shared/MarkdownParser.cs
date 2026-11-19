using System.Text;

using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

public static class MarkdownParser
{
	public static MarkdownDocumentModel Parse(string? markdown, MarkdownPipeline pipeline)
	{
		var source = markdown ?? string.Empty;
		if (string.IsNullOrWhiteSpace(source))
		{
			return MarkdownDocumentModel.Empty with { Source = source };
		}

		var document = Markdig.Markdown.Parse(source, pipeline);
		var blocks = new List<MarkdownDocumentBlock>();
		var plainText = new StringBuilder();
		var flags = MarkdownDependencyFlags.None;

		foreach (var block in document)
		{
			if (block is LinkReferenceDefinitionGroup)
			{
				continue;
			}

			var sourceSpan = GetSourceSpan(block, source.Length);
			var sourceText = source[sourceSpan.Start..sourceSpan.End];
			var kind = MarkdownBlockClassifier.GetKind(block, sourceText);
			if (kind == MarkdownBlockKind.LinkReference && string.IsNullOrWhiteSpace(sourceText))
			{
				continue;
			}

			var dependencyFlags = MarkdownBlockClassifier.GetDependencyFlags(block, kind);
			var blockText = ExtractPlainText(block).TrimEnd();
			var plainStart = plainText.Length;
			if (blockText.Length > 0)
			{
				plainText.Append(blockText);
			}

			var plainEnd = plainText.Length;
			if (plainText.Length > 0)
			{
				plainText.AppendLine();
				plainText.AppendLine();
			}

			flags |= dependencyFlags;
			blocks.Add(new MarkdownDocumentBlock(
				block,
				sourceSpan,
				new MarkdownTextSpan(plainStart, plainEnd),
				kind,
				dependencyFlags,
				ComputeHash(sourceText, kind, dependencyFlags),
				blockText,
				sourceText,
				MarkdownBlockClassifier.IsHeavy(block, kind)));
		}

		return new MarkdownDocumentModel(source, blocks, plainText.ToString().TrimEnd(), flags);
	}

	private static MarkdownTextSpan GetSourceSpan(Block block, int markdownLength)
	{
		var start = block.Span.Start >= 0 ? block.Span.Start : 0;
		var end = block.Span.End >= start ? block.Span.End + 1 : markdownLength;
		return new MarkdownTextSpan(
			Math.Clamp(start, 0, markdownLength),
			Math.Clamp(end, Math.Clamp(start, 0, markdownLength), markdownLength));
	}

	/// <summary>块内容哈希（开放围栏快速路径与解析器共用同一算法，保证快照内哈希可比）。</summary>
	internal static ulong ComputeBlockHash(string sourceText, MarkdownBlockKind kind, MarkdownDependencyFlags flags)
	{
		return ComputeHash(sourceText, kind, flags);
	}

	private static ulong ComputeHash(string sourceText, MarkdownBlockKind kind, MarkdownDependencyFlags flags)
	{
		const ulong offset = 14695981039346656037UL;
		const ulong prime = 1099511628211UL;

		var hash = offset;
		hash = Mix(hash, (byte)kind);
		hash = Mix(hash, (byte)flags);
		foreach (var c in sourceText)
		{
			hash = Mix(hash, (byte)c);
			hash = Mix(hash, (byte)(c >> 8));
		}

		return hash;

		static ulong Mix(ulong hash, byte value) => (hash ^ value) * prime;
	}

	/// <summary>块明文提取（开放围栏快速路径与解析器共用，保证 PlainText 逐字符一致）。</summary>
	internal static string ExtractPlainText(Block block)
	{
		var builder = new StringBuilder();
		AppendPlainTextBlock(builder, block, 0);
		return builder.ToString();
	}

	private static void AppendPlainTextBlock(StringBuilder builder, Block block, int indent)
	{
		switch (block)
		{
			case HeadingBlock heading:
				AppendIndentedLine(builder, indent, ExtractPlainText(heading.Inline));
				break;
			case ParagraphBlock paragraph:
				AppendIndentedLine(builder, indent, ExtractPlainText(paragraph.Inline));
				break;
			case LinkReferenceDefinitionGroup:
			case LinkReferenceDefinition:
				break;
			case MathBlock mathBlock:
				AppendIndentedLines(builder, indent, ExtractMathPlainText(mathBlock.Lines.ToString().TrimEnd()));
				break;
			case CodeBlock codeBlock:
				AppendIndentedLines(builder, indent, codeBlock.Lines.ToString().TrimEnd());
				break;
			case ListBlock list:
				AppendPlainTextList(builder, list, indent);
				break;
			case QuoteBlock quote:
				foreach (var child in quote)
				{
					AppendPlainTextBlock(builder, child, indent);
				}
				break;
			case ThematicBreakBlock:
				AppendIndentedLine(builder, indent, "---");
				break;
			case Table table:
				AppendPlainTextTable(builder, table, indent);
				break;
			case HtmlBlock htmlBlock:
				AppendIndentedLines(builder, indent, htmlBlock.Lines.ToString().TrimEnd());
				break;
			default:
				var text = block.ToString() ?? string.Empty;
				if (!IsTypeNameFallback(text, block.GetType()))
				{
					AppendIndentedLine(builder, indent, MarkdownChemistry.ReplaceChemCommandsWithPlainText(text));
				}
				break;
		}
	}

	private static string ExtractMathPlainText(string text)
	{
		return MarkdownChemistry.TryParseLatex(text, out var expression)
			? expression.PlainText
			: MarkdownChemistry.ReplaceChemCommandsWithPlainText(text);
	}

	private static void AppendPlainTextList(StringBuilder builder, ListBlock list, int indent)
	{
		var index = 1;
		foreach (var item in list.OfType<ListItemBlock>())
		{
			var marker = list.IsOrdered ? $"{index++}. " : "- ";
			AppendPlainTextListItem(builder, item, marker, indent);
		}
	}

	private static void AppendPlainTextListItem(StringBuilder builder, ListItemBlock item, string marker, int indent)
	{
		var isFirstBlock = true;
		foreach (var block in item)
		{
			if (isFirstBlock && block is ParagraphBlock paragraph)
			{
				var text = ExtractPlainText(paragraph.Inline);
				if (TryStripTaskPrefix(text, out var stripped))
				{
					text = stripped.TrimStart();
				}

				AppendIndent(builder, indent);
				builder.Append(marker);
				builder.AppendLine(text);
			}
			else
			{
				AppendPlainTextBlock(builder, block, indent + 2);
			}

			isFirstBlock = false;
		}
	}

	private static void AppendPlainTextTable(StringBuilder builder, Table table, int indent)
	{
		foreach (var row in table.OfType<TableRow>())
		{
			var cells = row.OfType<TableCell>()
				.Select(cell => ExtractBlocksPlainText(cell).ReplaceLineEndings(" "))
				.ToArray();
			AppendIndentedLine(builder, indent, string.Join('\t', cells));
		}
	}

	private static string ExtractBlocksPlainText(IEnumerable<Block> blocks)
	{
		var builder = new StringBuilder();
		foreach (var block in blocks)
		{
			AppendPlainTextBlock(builder, block, 0);
		}

		return builder.ToString().Trim();
	}

	private static string ExtractPlainText(ContainerInline? container)
		=> MarkdownPlainTextExtractor.ExtractPlainText(container);

	private static bool TryStripTaskPrefix(string text, out string stripped)
	{
		stripped = text;
		if (text.StartsWith("[x]", StringComparison.OrdinalIgnoreCase)
			|| text.StartsWith("[ ]", StringComparison.Ordinal))
		{
			stripped = text[3..];
			return true;
		}

		return false;
	}

	private static void AppendIndentedLines(StringBuilder builder, int indent, string text)
	{
		foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
		{
			AppendIndentedLine(builder, indent, line);
		}
	}

	private static void AppendIndentedLine(StringBuilder builder, int indent, string text)
	{
		AppendIndent(builder, indent);
		builder.AppendLine(text);
	}

	private static void AppendIndent(StringBuilder builder, int indent)
	{
		if (indent > 0)
		{
			builder.Append(' ', indent);
		}
	}

	private static bool IsTypeNameFallback(string text, Type type)
	{
		return string.Equals(text, type.FullName, StringComparison.Ordinal)
			   || string.Equals(text, type.Name, StringComparison.Ordinal);
	}
}
