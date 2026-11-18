using System.Text;

namespace CodeWF.Avalonia.Markdown.Streaming;

/// <summary>一次快照编辑里单行（或连续若干行）的去向。</summary>
public enum SnapshotLineFate
{
	/// <summary>内容与位置都没变。</summary>
	Unchanged,
	/// <summary>内容没变，但行号因前后插入/删除而移动。</summary>
	Shifted,
	/// <summary>行内容被替换（同一位置上的新内容）。</summary>
	Replaced,
	/// <summary>行被删除。</summary>
	Deleted,
	/// <summary>新插入的行（只在 <see cref="SnapshotLineEdit.NewStartLine"/> 侧有意义）。</summary>
	Inserted,
}

/// <summary>一段连续行的编辑：<see cref="OldLineCount"/> 行为 0 即纯插入，<see cref="NewLineCount"/> 为 0 即纯删除。</summary>
public readonly record struct SnapshotLineEdit(
	SnapshotLineFate Fate,
	int OldStartLine,
	int OldLineCount,
	int NewStartLine,
	int NewLineCount);

/// <summary>
/// 即时编辑场景的「快照差分」（StreamMark SnapshotEdit 的保守移植）：
/// 宿主每隔几百毫秒把整篇文本整体赋给 viewer 时，块的事实身份（源码偏移）会全部漂移，
/// 折叠与高亮会话因此丢失。本类先把两次快照对齐成行级编辑，再据此搬运状态：
///
/// 1. 剥离公共前缀与公共后缀（最常见的「末尾追加」「光标处插入」都落到这一步）；
/// 2. 中间段按行内容做 LCS 对齐（内容相同的行视为同一行，位置变了就是 Shifted）；
/// 3. 输出行级去向表 + 合并后的编辑段，并提供旧偏移 → 新偏移的映射。
///
/// 纯逻辑、无 Avalonia 依赖；行号 0 起，偏移按整篇字符计。
/// </summary>
public sealed class MarkdownSnapshotEdit
{
	/// <summary>中间段 LCS 的规模上限：超过就退化为「整段替换」，避免即时编辑卡在 DP 上。</summary>
	private const int LcsCellBudget = 250_000;

	private readonly int[] _oldLineStarts;
	private readonly int[] _newLineStarts;
	private readonly int[] _oldToNewLine;

	private MarkdownSnapshotEdit(
		int[] oldLineStarts,
		int[] newLineStarts,
		int[] oldToNewLine,
		IReadOnlyList<SnapshotLineEdit> edits,
		int commonPrefixLines,
		int commonSuffixLines)
	{
		_oldLineStarts = oldLineStarts;
		_newLineStarts = newLineStarts;
		_oldToNewLine = oldToNewLine;
		Edits = edits;
		CommonPrefixLines = commonPrefixLines;
		CommonSuffixLines = commonSuffixLines;
	}

	/// <summary>合并后的编辑段（按旧行号升序）。</summary>
	public IReadOnlyList<SnapshotLineEdit> Edits { get; }

	/// <summary>起始处内容完全相同的行数。</summary>
	public int CommonPrefixLines { get; }

	/// <summary>结尾处内容完全相同的行数。</summary>
	public int CommonSuffixLines { get; }

	/// <summary>是否没有任何变化。</summary>
	public bool IsIdentity => Edits.Count == 0;

	/// <summary>旧行号 → 新行号（-1 = 该行已删除）。</summary>
	public IReadOnlyList<int> OldLineToNewLine => _oldToNewLine;

	/// <summary>旧行总数。</summary>
	public int OldLineCount => _oldToNewLine.Length;

	/// <summary>
	/// 比较两份快照；两边都为空时返回空编辑。行以 '\n' 切分，'\r' 视为行尾装饰
	/// （对齐全按原文比较，只影响行数统计）。
	/// </summary>
	public static MarkdownSnapshotEdit Compare(string? oldText, string? newText)
	{
		oldText ??= string.Empty;
		newText ??= string.Empty;

		var oldLines = SplitLines(oldText, out var oldLineStarts);
		var newLines = SplitLines(newText, out var newLineStarts);

		// 公共前缀（行级）。
		var prefix = 0;
		while (prefix < oldLines.Length && prefix < newLines.Length
			&& string.Equals(oldLines[prefix], newLines[prefix], StringComparison.Ordinal))
		{
			prefix++;
		}

		// 公共后缀（行级，不越过前缀）。
		var suffix = 0;
		while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
			&& string.Equals(oldLines[oldLines.Length - 1 - suffix], newLines[newLines.Length - 1 - suffix], StringComparison.Ordinal))
		{
			suffix++;
		}

		var oldMiddle = oldLines.Length - prefix - suffix;
		var newMiddle = newLines.Length - prefix - suffix;
		var map = new int[oldLines.Length];
		Array.Fill(map, -1);

		for (var i = 0; i < prefix; i++)
		{
			map[i] = i;
		}

		for (var i = 0; i < suffix; i++)
		{
			map[oldLines.Length - 1 - i] = newLines.Length - 1 - i;
		}

		// 中间段 LCS：内容相同的行视为同一行，按出现顺序配对。
		var pairedOld = new bool[Math.Max(0, oldMiddle)];
		var pairedNew = new bool[Math.Max(0, newMiddle)];
		if (oldMiddle > 0 && newMiddle > 0)
		{
			if ((long)oldMiddle * newMiddle <= LcsCellBudget)
			{
				LcsPair(oldLines, prefix, oldMiddle, newLines, prefix, newMiddle, pairedOld, pairedNew, map);
			}
			else
			{
				// 规模过大：退化为逐位比较，只保留「同一相对位置内容相同」的行。
				for (var i = 0; i < oldMiddle && i < newMiddle; i++)
				{
					if (string.Equals(oldLines[prefix + i], newLines[prefix + i], StringComparison.Ordinal))
					{
						pairedOld[i] = pairedNew[i] = true;
						map[prefix + i] = prefix + i;
					}
				}
			}
		}

		var edits = BuildEdits(prefix, oldMiddle, newMiddle, pairedOld, pairedNew);
		return new MarkdownSnapshotEdit(oldLineStarts, newLineStarts, map, edits, prefix, suffix);
	}

	/// <summary>某旧行的去向（Unchanged / Shifted / Replaced / Deleted）。</summary>
	public SnapshotLineFate FateOf(int oldLine)
	{
		if (oldLine < 0 || oldLine >= _oldToNewLine.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(oldLine));
		}

		var newLine = _oldToNewLine[oldLine];
		if (newLine >= 0)
		{
			return newLine == oldLine ? SnapshotLineFate.Unchanged : SnapshotLineFate.Shifted;
		}

		foreach (var edit in Edits)
		{
			if (edit.Fate == SnapshotLineFate.Replaced
				&& oldLine >= edit.OldStartLine && oldLine < edit.OldStartLine + edit.OldLineCount)
			{
				return SnapshotLineFate.Replaced;
			}
		}

		return SnapshotLineFate.Deleted;
	}

	/// <summary>
	/// 旧偏移 → 新偏移。落点所在行保留时按其行内相对位置平移；行被删除或所在行被替换时返回 null。
	/// </summary>
	public int? MapOffset(int oldOffset)
	{
		if (oldOffset < 0 || _oldLineStarts.Length == 0)
		{
			return null;
		}

		var line = Array.BinarySearch(_oldLineStarts, oldOffset);
		if (line < 0)
		{
			line = ~line - 1;
		}

		if (line < 0 || line >= _oldToNewLine.Length)
		{
			return null;
		}

		var newLine = _oldToNewLine[line];
		if (newLine < 0)
		{
			return null;
		}

		var relative = oldOffset - _oldLineStarts[line];
		return _newLineStarts[newLine] + relative;
	}

	/// <summary>按行切分并返回每行起始偏移（行数 = 分段数；空文本也算一行）。</summary>
	private static string[] SplitLines(string text, out int[] lineStarts)
	{
		var lines = text.Length == 0 ? [string.Empty] : text.Split('\n');
		var starts = new int[lines.Length];
		var offset = 0;
		for (var i = 0; i < lines.Length; i++)
		{
			starts[i] = offset;
			// +1 = 换行符（最后一行没有换行符，偏移不会用到它之后的位置）。
			offset += lines[i].Length + 1;
		}

		lineStarts = starts;
		return lines;
	}

	/// <summary>经典 LCS 回溯，把内容相同的行按顺序配对写进 <paramref name="map"/>。</summary>
	private static void LcsPair(
		string[] oldLines, int oldStart, int oldCount,
		string[] newLines, int newStart, int newCount,
		bool[] pairedOld, bool[] pairedNew, int[] map)
	{
		var width = newCount + 1;
		var table = new int[(oldCount + 1) * width];
		for (var i = oldCount - 1; i >= 0; i--)
		{
			for (var j = newCount - 1; j >= 0; j--)
			{
				table[i * width + j] = string.Equals(oldLines[oldStart + i], newLines[newStart + j], StringComparison.Ordinal)
					? table[(i + 1) * width + j + 1] + 1
					: Math.Max(table[(i + 1) * width + j], table[i * width + j + 1]);
			}
		}

		var x = 0;
		var y = 0;
		while (x < oldCount && y < newCount)
		{
			if (string.Equals(oldLines[oldStart + x], newLines[newStart + y], StringComparison.Ordinal))
			{
				pairedOld[x] = true;
				pairedNew[y] = true;
				map[oldStart + x] = newStart + y;
				x++;
				y++;
			}
			else if (table[(x + 1) * width + y] >= table[x * width + y + 1])
			{
				x++;
			}
			else
			{
				y++;
			}
		}
	}

	/// <summary>把中间段的配对结果压成连续编辑段（插入/删除/替换各一段）。</summary>
	private static IReadOnlyList<SnapshotLineEdit> BuildEdits(
		int prefix, int oldMiddle, int newMiddle,
		bool[] pairedOld, bool[] pairedNew)
	{
		var edits = new List<SnapshotLineEdit>();
		var x = 0;
		var y = 0;
		while (x < oldMiddle || y < newMiddle)
		{
			if (x < oldMiddle && y < newMiddle && pairedOld[x] && pairedNew[y])
			{
				x++;
				y++;
				continue;
			}

			var startOld = x;
			var startNew = y;
			while (x < oldMiddle && !pairedOld[x])
			{
				x++;
			}

			while (y < newMiddle && !pairedNew[y])
			{
				y++;
			}

			var oldCount = x - startOld;
			var newCount = y - startNew;
			var fate = oldCount == 0 ? SnapshotLineFate.Inserted
				: newCount == 0 ? SnapshotLineFate.Deleted
				: SnapshotLineFate.Replaced;
			edits.Add(new SnapshotLineEdit(fate, prefix + startOld, oldCount, prefix + startNew, newCount));
		}

		return edits;
	}

	/// <summary>调试用：把编辑段渲染成一行摘要。</summary>
	public override string ToString()
	{
		if (Edits.Count == 0)
		{
			return $"identity(prefix={CommonPrefixLines}, suffix={CommonSuffixLines})";
		}

		var builder = new StringBuilder();
		foreach (var edit in Edits)
		{
			if (builder.Length > 0)
			{
				builder.Append("; ");
			}

			builder.Append($"{edit.Fate} old={edit.OldStartLine}+{edit.OldLineCount} new={edit.NewStartLine}+{edit.NewLineCount}");
		}

		return builder.ToString();
	}
}
