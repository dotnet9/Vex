using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// GFM 任务列表辅助：识别列表项勾选状态与 "[x]"/"[ ]" 文本前缀。
/// </summary>
public static class MarkdownTaskListHelper
{
	public static bool TryReadTaskState(ListItemBlock item, out bool isChecked)
	{
		isChecked = false;
		if (item.FirstOrDefault() is not ParagraphBlock paragraph)
		{
			return false;
		}

		if (paragraph.Inline?.FirstChild is TaskList taskList)
		{
			isChecked = taskList.Checked;
			return true;
		}

		if (paragraph.Inline?.FirstChild is not LiteralInline literal)
		{
			return false;
		}

		var text = literal.Content.ToString();
		if (text.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
		{
			isChecked = true;
			return true;
		}

		return text.StartsWith("[ ]", StringComparison.Ordinal);
	}

	public static bool TryStripTaskPrefix(string text, out string stripped)
	{
		stripped = text;
		if (text.StartsWith("[x]", StringComparison.OrdinalIgnoreCase)
			|| text.StartsWith("[ ]", StringComparison.Ordinal))
		{
			stripped = text[3..];
			return true;
		}
		// Markdig emits TaskList separately, leaving its separator in the next literal.
		if (text.Length > 0 && char.IsWhiteSpace(text[0]))
		{
			stripped = text.TrimStart();
			return true;
		}

		return false;
	}
}
