using System.Text;

namespace CodeWF.Avalonia.Markdown.Streaming;

/// <summary>
/// 尾部容错补全结果：<see cref="RemendedText"/> 是补全后的临时文本（仅用于解析，
/// 绝不写回源码缓冲），<see cref="OffsetMap"/> 用于把临时文本中的块偏移映射回原始尾部。
/// </summary>
public sealed record MarkdownTailRemendResult(string RemendedText, MarkdownTailOffsetMap OffsetMap)
{
	public static MarkdownTailRemendResult Identity(string tail) =>
		new(tail, MarkdownTailOffsetMap.Identity(tail.Length));
}

/// <summary>
/// 补全插入造成的偏移映射。补全只在若干插入点追加字符（及对半截 HTML 标签的
/// 尾部剥离），因此映射是"分段恒等 + 插入点平移"的轻量结构。
/// </summary>
public sealed class MarkdownTailOffsetMap
{
	private readonly record struct InsertionSpan(int OriginalPosition, int TempStart, int Length);

	private readonly List<InsertionSpan> _insertions;
	private readonly int _remainedLength;
	private readonly int _originalLength;

	private MarkdownTailOffsetMap(List<InsertionSpan> insertions, int remainedLength, int originalLength)
	{
		_insertions = insertions;
		_remainedLength = remainedLength;
		_originalLength = originalLength;
	}

	public static MarkdownTailOffsetMap Identity(int length) =>
		new([], length, length);

	/// <summary>原始尾部长度。</summary>
	public int OriginalLength => _originalLength;

	/// <summary>临时偏移 → 原始偏移（落在插入字符上或剥离区间内时向收拢方向钳制）。</summary>
	public int MapTempToOriginal(int temp)
	{
		var original = temp;
		foreach (var insertion in _insertions)
		{
			if (original >= insertion.TempStart + insertion.Length)
			{
				original -= insertion.Length;
			}
			else
			{
				break;
			}
		}

		return Math.Min(original, _remainedLength);
	}

	/// <summary>
	/// 临时文本中的区间 [tempStart, tempEnd) 是否不含补全/剥离痕迹
	/// （不含时映射结果与原文逐字符一致，可安全用作块 key / 选区坐标）。
	/// </summary>
	public bool IsSpanClean(int tempStart, int tempEnd)
	{
		if (tempEnd > _remainedLength)
		{
			return false;
		}

		foreach (var insertion in _insertions)
		{
			if (insertion.TempStart < tempEnd && insertion.TempStart + insertion.Length > tempStart)
			{
				return false;
			}
		}

		return true;
	}

	internal static MarkdownTailOffsetMap Build(
		int remainedLength,
		int originalLength,
		IReadOnlyList<(int OriginalPosition, string Text)> insertions)
	{
		var spans = new List<InsertionSpan>(insertions.Count);
		var temp = 0;
		var original = 0;
		foreach (var (position, text) in insertions)
		{
			temp += position - original;
			original = position;
			spans.Add(new InsertionSpan(position, temp, text.Length));
			temp += text.Length;
		}

		return new MarkdownTailOffsetMap(spans, remainedLength, originalLength);
	}
}

/// <summary>
/// 尾部容错补全（规格：docs/流式规则.md §4）。
///
/// 尾部是用户正在观看的"半截文本"，补全让它尽量呈现目标形态而非残缺语法。
/// 补全只作用于临时文本，绝不写回源码缓冲；插入点全部记录进
/// <see cref="MarkdownTailOffsetMap"/>，块偏移可映射回原始源码坐标。
/// </summary>
public static class MarkdownTailRemender
{
	/// <summary>未完成链接的占位地址。</summary>
	public const string PendingLinkUri = "codewf-md:pending";

	/// <summary>对尾部执行全部容错处理器，返回补全文本与偏移映射。</summary>
	public static MarkdownTailRemendResult Apply(string tail)
	{
		ArgumentNullException.ThrowIfNull(tail);
		if (tail.Length == 0)
		{
			return MarkdownTailRemendResult.Identity(tail);
		}

		// 围栏状态先行：围栏内容是字面文本，半截标签剥离与字符级配对都必须跳过。
		var insertions = new List<(int Position, string Text)>();
		string remained;
		if (TryMatchFenceState(tail, out var fenceChar, out var fenceLength))
		{
			remained = tail;
			insertions.Add((tail.Length, "\n" + new string(fenceChar, Math.Max(3, fenceLength))));
		}
		else
		{
			// 半截 HTML 标签剥离（缩短文本，之后的偏移以剥离后的文本为基准）。
			remained = TryStripHalfHtmlTag(tail, out var stripPosition) ? tail[..stripPosition] : tail;

			// P10 未完成链接：](...) 缺右括号、[text 缺地址。
			if (HasUnclosedLinkParenthesis(remained))
			{
				insertions.Add((remained.Length, ")"));
			}
			else if (HasUnclosedBracket(remained))
			{
				insertions.Add((remained.Length, "](" + PendingLinkUri + ")"));
			}

			// P20 强调（按奇偶补齐；单个 `_` 因 snake_case 误报不处理，见规格 §4.1）。
			if (CountOccurrences(remained, "**") % 2 == 1)
			{
				insertions.Add((remained.Length, "**"));
			}

			if (CountOccurrences(remained, "__") % 2 == 1)
			{
				insertions.Add((remained.Length, "__"));
			}

			if ((CountCharacter(remained, '*') - 2 * CountOccurrences(remained, "**")) % 2 == 1)
			{
				insertions.Add((remained.Length, "*"));
			}

			// P30 行内代码反引号。
			if (CountCharacter(remained, '`') % 2 == 1)
			{
				insertions.Add((remained.Length, "`"));
			}

			// P35 删除线。
			if (CountOccurrences(remained, "~~") % 2 == 1)
			{
				insertions.Add((remained.Length, "~~"));
			}

			// P40 数学块。
			if (CountOccurrences(remained, "$$") % 2 == 1)
			{
				insertions.Add((remained.Length, "\n$$"));
			}

			// P45 setext 歧义打破：尾行只有 1-2 个 =/-（可带空白）且上一非空行有内容时，
			// 半个列表项会被解析器判成 setext 标题下划线；插入零宽空格打破
			// （借鉴 vercel/streamdown remend 的 SetextHeadingHandler，Apache-2.0）。
			if (EndsWithIncompleteSetextUnderline(remained))
			{
				insertions.Add((remained.Length, "\u200B"));
			}
		}

		if (insertions.Count == 0)
		{
			return new MarkdownTailRemendResult(remained, MarkdownTailOffsetMap.Build(remained.Length, tail.Length, []));
		}

		var builder = new StringBuilder(remained.Length + 16);
		var cursor = 0;
		foreach (var (position, text) in insertions)
		{
			builder.Append(remained, cursor, position - cursor);
			builder.Append(text);
			cursor = position;
		}

		builder.Append(remained, cursor, remained.Length - cursor);

		return new MarkdownTailRemendResult(
			builder.ToString(),
			MarkdownTailOffsetMap.Build(remained.Length, tail.Length, insertions));
	}

	/// <summary>
	/// 尾行是否为"半截 setext 下划线"：只有 1-2 个 =/-（可带空白）且上一非空行有内容。
	/// 单个 =/- 本就构成合法下划线，此处的危害是流式中的半个列表项（如只敲出 "-"）被误判成标题。
	/// </summary>
	private static bool EndsWithIncompleteSetextUnderline(string text)
	{
		var lastNewline = text.LastIndexOf('\n');
		var lastLine = lastNewline < 0 ? text : text[(lastNewline + 1)..];
		var trimmed = lastLine.Trim(' ', '\t');
		if (trimmed is not ("=" or "==" or "-" or "--"))
		{
			return false;
		}

		// 上一非空行需要有内容，否则不存在 setext 标题的成立条件。
		var scanEnd = lastNewline;
		while (scanEnd > 0)
		{
			var lineStart = text.LastIndexOf('\n', scanEnd - 1);
			lineStart = lineStart < 0 ? 0 : lineStart + 1;
			if (text[lineStart..scanEnd].Trim(' ', '\t').Length > 0)
			{
				return true;
			}

			scanEnd = lineStart - 1;
		}

		return false;
	}

	/// <summary>
	/// 最后一行末尾的半截 HTML 标签（如 `&lt;div clas`）无法构成元素，
	/// 剥离后解析避免残缺标签被当成纯文本渲染。返回是否剥离及剥离起点。
	/// </summary>
	private static bool TryStripHalfHtmlTag(string tail, out int stripPosition)
	{
		stripPosition = -1;
		var lastNewline = tail.LastIndexOf('\n');
		var scanStart = lastNewline < 0 ? 0 : lastNewline + 1;
		var open = tail.LastIndexOf('<', tail.Length - 1, tail.Length - scanStart);
		if (open < scanStart || open + 1 >= tail.Length)
		{
			return false;
		}

		var next = tail[open + 1];
		if (next is not ('/' or '!' or '?') && !char.IsAsciiLetter(next))
		{
			return false;
		}

		// 同一行内 `<` 之后没有 `>` 才是半截标签。
		if (tail.IndexOf('>', open) >= 0)
		{
			return false;
		}

		stripPosition = open;
		return true;
	}

	/// <summary>尾部是否停留在未闭合围栏内（开围栏行识别规则与切分器一致）。</summary>
	private static bool TryMatchFenceState(string text, out char fenceChar, out int fenceLength)
	{
		fenceChar = default;
		fenceLength = 0;
		var inFence = false;
		var offset = 0;
		while (offset <= text.Length)
		{
			var newline = text.IndexOf('\n', offset);
			var end = newline < 0 ? text.Length : newline + 1;
			var line = text[offset..end];
			if (inFence)
			{
				if (IsFenceClose(line, fenceChar, fenceLength))
				{
					inFence = false;
				}
			}
			else if (CountIndent(line, out var rest) <= 3 && TryMatchFenceOpen(rest, out fenceChar, out fenceLength))
			{
				inFence = true;
			}

			if (newline < 0)
			{
				break;
			}

			offset = end;
		}

		return inFence;
	}

	private static bool HasUnclosedLinkParenthesis(string text)
	{
		var paren = text.LastIndexOf("](", StringComparison.Ordinal);
		return paren >= 0 && text.IndexOf(')', paren + 2) < 0;
	}

	private static bool HasUnclosedBracket(string text)
	{
		var bracket = text.LastIndexOf('[');
		return bracket >= 0 && text.IndexOf(']', bracket + 1) < 0;
	}

	private static int CountOccurrences(string text, string token)
	{
		var count = 0;
		var offset = 0;
		while ((offset = text.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
		{
			count++;
			offset += token.Length;
		}

		return count;
	}

	private static int CountCharacter(string text, char value)
	{
		var count = 0;
		foreach (var c in text)
		{
			if (c == value)
			{
				count++;
			}
		}

		return count;
	}

	private static int CountIndent(string line, out string rest)
	{
		var indent = 0;
		var index = 0;
		while (index < line.Length)
		{
			var c = line[index];
			if (c == ' ')
			{
				indent++;
				index++;
			}
			else if (c == '\t')
			{
				indent += 4 - (indent % 4);
				index++;
			}
			else
			{
				break;
			}
		}

		rest = line[index..];
		return indent;
	}

	private static bool TryMatchFenceOpen(string trimmed, out char fenceChar, out int fenceLength)
	{
		fenceChar = default;
		fenceLength = 0;
		if (trimmed.Length < 3)
		{
			return false;
		}

		var candidate = trimmed[0];
		if (candidate is not ('`' or '~'))
		{
			return false;
		}

		var run = 1;
		while (run < trimmed.Length && trimmed[run] == candidate)
		{
			run++;
		}

		if (run < 3 || (candidate == '`' && trimmed[run..].Contains('`')))
		{
			return false;
		}

		fenceChar = candidate;
		fenceLength = run;
		return true;
	}

	private static bool IsFenceClose(string line, char fenceChar, int fenceLength)
	{
		var indent = CountIndent(line, out var rest);
		if (indent > 3)
		{
			return false;
		}

		var run = 0;
		while (run < rest.Length && rest[run] == fenceChar)
		{
			run++;
		}

		return run >= fenceLength && rest[run..].Trim().Length == 0;
	}
}
