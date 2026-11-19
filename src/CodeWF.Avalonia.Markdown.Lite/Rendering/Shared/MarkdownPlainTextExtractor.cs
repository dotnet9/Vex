using System.Text.RegularExpressions;

using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 从 Markdig 内联树提取纯文本，供复制、选择与无渲染降级路径复用。
/// </summary>
public static class MarkdownPlainTextExtractor
{
	public static string ExtractPlainText(ContainerInline? container)
	{
		if (container is null)
		{
			return string.Empty;
		}

			var parts = new List<string>();
			var stripTaskSeparator = false;
			var child = container.FirstChild;
			while (child is not null)
			{
				var text = child switch
				{
					LiteralInline literal => literal.Content.ToString(),
					CodeInline code => code.Content,
					LineBreakInline => Environment.NewLine,
					TaskList => string.Empty,
					// 行内样式标签（<span style> / </span>）不占正文；<br> 视为换行。
					HtmlInline html when IsLineBreakTag(html.Tag) => Environment.NewLine,
					HtmlInline html when IsSpanTag(html.Tag) => string.Empty,
					LinkInline { IsImage: true } image => ExtractImageText(image),
					ContainerInline nested => ExtractPlainText(nested),
					_ when MarkdownChemistry.TryGetChemInlinePlainText(child, out var chemText) => chemText,
					_ => IsTypeNameFallback(child.ToString() ?? string.Empty, child.GetType())
						? string.Empty
						: MarkdownChemistry.ReplaceChemCommandsWithPlainText(child.ToString() ?? string.Empty)
				};
				if (stripTaskSeparator && child is LiteralInline) text = text.TrimStart();
				stripTaskSeparator = child is TaskList;
				parts.Add(text);
				child = child.NextSibling;
			}

			return string.Concat(parts);
		}

		private static bool IsSpanTag(string? tag)
		{
			return tag is not null && (tag.Equals("</span>", StringComparison.OrdinalIgnoreCase)
				|| (tag.StartsWith("<span", StringComparison.OrdinalIgnoreCase) && tag.EndsWith('>')));
		}

		private static bool IsLineBreakTag(string? tag)
		{
			return tag is not null && Regex.IsMatch(tag, @"^<br\s*/?>$", RegexOptions.IgnoreCase);
		}

	public static string ExtractImageText(LinkInline image)
	{
		var altText = ExtractPlainText((ContainerInline)image);
		if (!string.IsNullOrWhiteSpace(altText))
		{
			return altText;
		}

		return string.IsNullOrWhiteSpace(image.Url) ? "[image]" : image.Url!;
	}

	/// <summary>
	/// Markdig 对未识别节点 ToString 会回退成类型名，这里识别该情形以避免把类型名当正文输出。
	/// </summary>
	public static bool IsTypeNameFallback(string text, Type type)
	{
		return string.Equals(text, type.FullName, StringComparison.Ordinal)
			   || string.Equals(text, type.Name, StringComparison.Ordinal);
	}
}
