using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 任务列表勾选标记在源码中的位置，<see cref="Start"/> 指向 "["，<see cref="End"/> 指向 "]" 之后。
/// </summary>
public sealed record MarkdownTaskMarker(int Start, int End, bool IsChecked, int Line);

/// <summary>
/// 任务列表勾选回写结果：新 Markdown 文本与被改写的源码区间。
/// </summary>
public sealed record MarkdownTaskWriteResult(string Markdown, MarkdownTextSpan ChangedSpan, bool IsChecked);

/// <summary>
/// GFM 任务列表回写：在源码中定位 "- [ ]" / "- [x]" 的勾选标记并按需改写，
/// 返回新文本与变更区间，宿主以单次替换消费，避免整篇重设。
/// </summary>
public static class MarkdownTaskListWriter
{
	public static IReadOnlyList<MarkdownTaskMarker> FindTaskMarkers(string? markdown)
	{
		var source = markdown ?? string.Empty;
		if (source.Length == 0)
		{
			return [];
		}

		var markers = new List<MarkdownTaskMarker>();
		var line = 1;
		var lineStart = 0;
		var fenceMarker = '\0';
		var fenceLength = 0;

		while (lineStart <= source.Length)
		{
			var lineEnd = source.IndexOf('\n', lineStart);
			var isLastLine = lineEnd < 0;
			if (isLastLine)
			{
				lineEnd = source.Length;
			}

			var lineText = source.AsSpan(lineStart, lineEnd - lineStart).TrimEnd('\r');

			if (TryReadFence(lineText, out var marker, out var markerLength, out var isClosing))
			{
				if (fenceMarker == '\0')
				{
					fenceMarker = marker;
					fenceLength = markerLength;
				}
				else if (marker == fenceMarker && markerLength >= fenceLength && isClosing)
				{
					fenceMarker = '\0';
				}
			}
			else if (fenceMarker == '\0'
					 && TryLocateTaskMarkerInLine(lineText, lineStart, out var markerStart, out var markerEnd, out var isChecked))
			{
				markers.Add(new MarkdownTaskMarker(markerStart, markerEnd, isChecked, line));
			}

			if (isLastLine)
			{
				break;
			}

			lineStart = lineEnd + 1;
			line++;
		}

		return markers;
	}

	/// <summary>
	/// 在指定源码区间（通常为列表项块）内定位第一个勾选标记。
	/// </summary>
	public static bool TryLocateTaskMarker(
		string? markdown,
		int start,
		int end,
		out int markerStart,
		out bool isChecked)
	{
		markerStart = -1;
		isChecked = false;

		var source = markdown ?? string.Empty;
		var from = Math.Clamp(start, 0, source.Length);
		var to = Math.Clamp(end, from, source.Length);
		for (var index = from; index + 2 < to; index++)
		{
			if (source[index] != '['
				|| !IsTaskStateCharacter(source[index + 1])
				|| source[index + 2] != ']')
			{
				continue;
			}

			markerStart = index;
			isChecked = source[index + 1] is 'x' or 'X';
			return true;
		}

		return false;
	}

	public static MarkdownTaskWriteResult? SetTaskState(
		string? markdown,
		int markerStart,
		bool isChecked)
	{
		var source = markdown ?? string.Empty;
		if (markerStart < 0
			|| markerStart + 2 >= source.Length
			|| source[markerStart] != '['
			|| !IsTaskStateCharacter(source[markerStart + 1])
			|| source[markerStart + 2] != ']')
		{
			return null;
		}

		if (IsCheckedCharacter(source[markerStart + 1]) == isChecked)
		{
			return null;
		}

		var builder = new System.Text.StringBuilder(source);
		builder[markerStart + 1] = isChecked ? 'x' : ' ';
		return new MarkdownTaskWriteResult(
			builder.ToString(),
			new MarkdownTextSpan(markerStart, markerStart + 3),
			isChecked);
	}

	/// <summary>
	/// 按源码偏移定位勾选标记并改写；偏移可落在标记区间内的任意位置。
	/// </summary>
	public static MarkdownTaskWriteResult? SetTaskStateAtOffset(
		string? markdown,
		int sourceOffset,
		bool isChecked)
	{
		var source = markdown ?? string.Empty;
		foreach (var marker in FindTaskMarkers(source))
		{
			if (sourceOffset >= marker.Start && sourceOffset <= marker.End)
			{
				return SetTaskState(source, marker.Start, isChecked);
			}
		}

		var before = source.LastIndexOf('\n', Math.Clamp(sourceOffset - 1, 0, Math.Max(0, source.Length - 1)));
		var lineStart = before < 0 ? 0 : before + 1;
		var lineEnd = source.IndexOf('\n', lineStart);
		if (lineEnd < 0)
		{
			lineEnd = source.Length;
		}

		if (TryLocateTaskMarker(source, lineStart, lineEnd, out var markerStart, out _))
		{
			return SetTaskState(source, markerStart, isChecked);
		}

		return null;
	}

	/// <summary>
	/// 按所在块区间定位勾选标记并改写。
	/// </summary>
	public static MarkdownTaskWriteResult? SetTaskStateInBlock(
		string? markdown,
		int blockStart,
		int blockEnd,
		bool isChecked)
	{
		var source = markdown ?? string.Empty;
		return TryLocateTaskMarker(source, blockStart, blockEnd, out var markerStart, out _)
			? SetTaskState(source, markerStart, isChecked)
			: null;
	}

	private static bool TryLocateTaskMarkerInLine(
		ReadOnlySpan<char> line,
		int lineOffset,
		out int markerStart,
		out int markerEnd,
		out bool isChecked)
	{
		markerStart = -1;
		markerEnd = -1;
		isChecked = false;

		var index = SkipBlockQuoteMarkers(line, 0, out _);
		index = SkipIndent(line, index);
		if (!TrySkipListMarker(line, index, out var contentStart))
		{
			return false;
		}

		contentStart = SkipIndent(line, contentStart);
		if (contentStart + 2 >= line.Length || line[contentStart] != '[')
		{
			return false;
		}

		if (!IsTaskStateCharacter(line[contentStart + 1]) || line[contentStart + 2] != ']')
		{
			return false;
		}

		markerStart = lineOffset + contentStart;
		markerEnd = markerStart + 3;
		isChecked = IsCheckedCharacter(line[contentStart + 1]);
		return true;
	}

	private static int SkipBlockQuoteMarkers(ReadOnlySpan<char> line, int start, out int depth)
	{
		depth = 0;
		var index = start;
		while (index < line.Length)
		{
			var probe = SkipIndent(line, index);
			if (probe >= line.Length || line[probe] != '>')
			{
				break;
			}

			depth++;
			index = probe + 1;
			if (index < line.Length && line[index] == ' ')
			{
				index++;
			}
		}

		return index;
	}

	private static int SkipIndent(ReadOnlySpan<char> line, int start)
	{
		var index = start;
		var spaces = 0;
		while (index < line.Length && line[index] == ' ' && spaces < 3)
		{
			index++;
			spaces++;
		}

		return index;
	}

	private static bool TrySkipListMarker(ReadOnlySpan<char> line, int start, out int contentStart)
	{
		contentStart = start;
		if (start >= line.Length)
		{
			return false;
		}

		var marker = line[start];
		if (marker is '-' or '*' or '+')
		{
			var after = start + 1;
			if (after < line.Length && char.IsWhiteSpace(line[after]))
			{
				contentStart = after + 1;
				return true;
			}

			return false;
		}

		if (!char.IsDigit(marker))
		{
			return false;
		}

		var index = start;
		while (index < line.Length && char.IsDigit(line[index]) && index - start < 9)
		{
			index++;
		}

		if (index >= line.Length || line[index] is not ('.' or ')'))
		{
			return false;
		}

		index++;
		if (index < line.Length && char.IsWhiteSpace(line[index]))
		{
			contentStart = index + 1;
			return true;
		}

		return false;
	}

	private static bool TryReadFence(ReadOnlySpan<char> line, out char marker, out int markerLength, out bool isClosing)
	{
		marker = '\0';
		markerLength = 0;
		isClosing = false;

		var start = SkipIndent(line, 0);
		if (start >= line.Length || line[start] is not ('`' or '~'))
		{
			return false;
		}

		marker = line[start];
		var index = start;
		while (index < line.Length && line[index] == marker)
		{
			index++;
		}

		markerLength = index - start;
		if (markerLength < 3)
		{
			return false;
		}

		isClosing = line[index..].Trim().Length == 0;
		return true;
	}

	private static bool IsTaskStateCharacter(char character)
	{
		return character is ' ' or 'x' or 'X';
	}

	private static bool IsCheckedCharacter(char character)
	{
		return character is 'x' or 'X';
	}
}
