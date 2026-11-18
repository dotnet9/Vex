using System.Text;

using Markdig.Extensions.Mathematics;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

public enum MarkdownChemInlineKind
{
	Text,
	Subscript,
	Superscript
}

public sealed record MarkdownChemInline(MarkdownChemInlineKind Kind, string Text);

public sealed record MarkdownChemExpression(IReadOnlyList<MarkdownChemInline> Inlines, string PlainText);

public static class MarkdownChemistry
{
	private const string ChemCommand = @"\ce{";

	public static bool TryParseLatex(string latex, out MarkdownChemExpression expression)
	{
		expression = new MarkdownChemExpression([], string.Empty);
		var trimmed = TrimInlineMathDelimiters(latex);
		if (!TryReadChemCommand(trimmed, 0, out var content, out var end))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(trimmed[end..]))
		{
			return false;
		}

		expression = ParseExpression(content);
		return expression.Inlines.Count > 0;
	}

	public static bool TryGetChemInlinePlainText(object inline, out string plainText)
	{
		plainText = string.Empty;
		string text;
		if (inline is MathInline mathInline)
		{
			text = mathInline.Content.ToString().Trim();
		}
		else
		{
			var type = inline.GetType();
			if (!type.Name.Contains("Math", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			text = inline.ToString()?.Trim() ?? string.Empty;
			if (IsTypeNameFallback(text, type))
			{
				return false;
			}
		}

		if (!TryParseLatex(text, out var expression))
		{
			return false;
		}

		plainText = expression.PlainText;
		return true;
	}

	public static string ReplaceChemCommandsWithPlainText(string text)
	{
		if (string.IsNullOrEmpty(text) || !text.Contains(ChemCommand, StringComparison.Ordinal))
		{
			return text;
		}

		var builder = new StringBuilder(text.Length);
		var index = 0;
		while (index < text.Length)
		{
			var start = text.IndexOf(ChemCommand, index, StringComparison.Ordinal);
			if (start < 0)
			{
				builder.Append(text[index..]);
				break;
			}

			builder.Append(text[index..start]);
			if (TryReadChemCommand(text, start, out var content, out var end))
			{
				builder.Append(ParseExpression(content).PlainText);
				index = end;
			}
			else
			{
				builder.Append(ChemCommand);
				index = start + ChemCommand.Length;
			}
		}

		return builder.ToString();
	}

	private static MarkdownChemExpression ParseExpression(string expression)
	{
		var inlines = new List<MarkdownChemInline>();
		var plainText = new StringBuilder(expression.Length);
		var index = 0;
		while (index < expression.Length)
		{
			if (char.IsWhiteSpace(expression[index]))
			{
				Append(inlines, plainText, MarkdownChemInlineKind.Text, " ");
				while (index < expression.Length && char.IsWhiteSpace(expression[index]))
				{
					index++;
				}

				continue;
			}

			if (TryReadArrow(expression, index, out var arrow, out var label, out var arrowEnd))
			{
				Append(inlines, plainText, MarkdownChemInlineKind.Text, arrow);
				if (!string.IsNullOrWhiteSpace(label))
				{
					Append(inlines, plainText, MarkdownChemInlineKind.Superscript, label);
				}

				index = arrowEnd;
				continue;
			}

			var tokenEnd = FindTokenEnd(expression, index);
			ParseFormulaToken(expression[index..tokenEnd], inlines, plainText);
			index = tokenEnd;
		}

		return new MarkdownChemExpression(inlines, plainText.ToString());
	}

	private static int FindTokenEnd(string expression, int start)
	{
		var index = start;
		while (index < expression.Length
			   && !char.IsWhiteSpace(expression[index])
			   && !StartsWithArrow(expression, index))
		{
			index++;
		}

		return index;
	}

	private static void ParseFormulaToken(
		string token,
		List<MarkdownChemInline> inlines,
		StringBuilder plainText)
	{
		if (token is "+" or "-" or "=")
		{
			Append(inlines, plainText, MarkdownChemInlineKind.Text, token);
			return;
		}

		for (var i = 0; i < token.Length; i++)
		{
			var c = token[i];
			if (c == '\\' && token.AsSpan(i).StartsWith(@"\cdot", StringComparison.Ordinal))
			{
				Append(inlines, plainText, MarkdownChemInlineKind.Text, "\u00b7");
				i += 4;
			}
			else if (char.IsUpper(c))
			{
				var start = i++;
				while (i < token.Length && char.IsLower(token[i]))
				{
					i++;
				}

				Append(inlines, plainText, MarkdownChemInlineKind.Text, token[start..i]);
				i--;
			}
			else if (char.IsDigit(c))
			{
				var start = i;
				while (i + 1 < token.Length && char.IsDigit(token[i + 1]))
				{
					i++;
				}

				var kind = start == 0 ? MarkdownChemInlineKind.Text : MarkdownChemInlineKind.Subscript;
				Append(inlines, plainText, kind, token[start..(i + 1)]);
			}
			else if (c == '^' || c == '_')
			{
				var value = ReadScriptValue(token, ref i);
				if (!string.IsNullOrEmpty(value))
				{
					Append(
						inlines,
						plainText,
						c == '^' ? MarkdownChemInlineKind.Superscript : MarkdownChemInlineKind.Subscript,
						value);
				}
			}
			else if ((c == '+' || c == '-') && i == token.Length - 1 && token.Length > 1)
			{
				Append(inlines, plainText, MarkdownChemInlineKind.Superscript, c.ToString());
			}
			else
			{
				Append(inlines, plainText, MarkdownChemInlineKind.Text, c.ToString());
			}
		}
	}

	private static bool TryReadArrow(
		string text,
		int start,
		out string arrow,
		out string label,
		out int end)
	{
		arrow = string.Empty;
		label = string.Empty;
		end = start;

		var arrowLength = 0;
		if (text.AsSpan(start).StartsWith("<->", StringComparison.Ordinal))
		{
			arrow = "\u21cc";
			arrowLength = 3;
		}
		else if (text.AsSpan(start).StartsWith("->", StringComparison.Ordinal))
		{
			arrow = "\u2192";
			arrowLength = 2;
		}
		else if (text.AsSpan(start).StartsWith("<-", StringComparison.Ordinal))
		{
			arrow = "\u2190";
			arrowLength = 2;
		}

		if (arrowLength == 0)
		{
			return false;
		}

		end = start + arrowLength;
		if (end < text.Length && text[end] == '[')
		{
			var close = text.IndexOf(']', end + 1);
			if (close > end)
			{
				label = text[(end + 1)..close];
				end = close + 1;
			}
		}

		return true;
	}

	private static bool StartsWithArrow(string text, int index)
	{
		return text.AsSpan(index).StartsWith("<->", StringComparison.Ordinal)
			   || text.AsSpan(index).StartsWith("->", StringComparison.Ordinal)
			   || text.AsSpan(index).StartsWith("<-", StringComparison.Ordinal);
	}

	private static string ReadScriptValue(string text, ref int index)
	{
		if (index + 1 >= text.Length)
		{
			return string.Empty;
		}

		if (text[index + 1] == '{')
		{
			var end = FindMatchingBrace(text, index + 1);
			if (end > index + 1)
			{
				var value = text[(index + 2)..end];
				index = end;
				return value;
			}
		}

		var start = index + 1;
		var endIndex = start;
		while (endIndex < text.Length && (char.IsLetterOrDigit(text[endIndex]) || text[endIndex] is '+' or '-'))
		{
			endIndex++;
		}

		index = Math.Max(start, endIndex) - 1;
		return text[start..endIndex];
	}

	private static bool TryReadChemCommand(string text, int start, out string content, out int end)
	{
		content = string.Empty;
		end = start;
		if (!text.AsSpan(start).StartsWith(ChemCommand, StringComparison.Ordinal))
		{
			return false;
		}

		var contentStart = start + ChemCommand.Length;
		var contentEnd = FindMatchingBrace(text, contentStart - 1);
		if (contentEnd < 0)
		{
			return false;
		}

		content = text[contentStart..contentEnd];
		end = contentEnd + 1;
		return true;
	}

	private static int FindMatchingBrace(string text, int openBraceIndex)
	{
		var depth = 0;
		for (var i = openBraceIndex; i < text.Length; i++)
		{
			if (text[i] == '{')
			{
				depth++;
			}
			else if (text[i] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return i;
				}
			}
		}

		return -1;
	}

	private static string TrimInlineMathDelimiters(string text)
	{
		var trimmed = text.Trim();
		if (trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal) && trimmed.Length > 4)
		{
			return trimmed[2..^2].Trim();
		}

		if (trimmed.StartsWith('$') && trimmed.EndsWith('$') && trimmed.Length > 2)
		{
			return trimmed[1..^1].Trim();
		}

		if (trimmed.StartsWith(@"\(", StringComparison.Ordinal) && trimmed.EndsWith(@"\)", StringComparison.Ordinal) && trimmed.Length > 4)
		{
			return trimmed[2..^2].Trim();
		}

		if (trimmed.StartsWith(@"\[", StringComparison.Ordinal) && trimmed.EndsWith(@"\]", StringComparison.Ordinal) && trimmed.Length > 4)
		{
			return trimmed[2..^2].Trim();
		}

		return trimmed;
	}

	private static bool IsTypeNameFallback(string text, Type type)
	{
		return string.Equals(text, type.FullName, StringComparison.Ordinal)
			   || string.Equals(text, type.Name, StringComparison.Ordinal);
	}

	private static void Append(
		List<MarkdownChemInline> inlines,
		StringBuilder plainText,
		MarkdownChemInlineKind kind,
		string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		if (inlines.Count > 0 && inlines[^1].Kind == kind)
		{
			var previous = inlines[^1];
			inlines[^1] = previous with { Text = previous.Text + text };
		}
		else
		{
			inlines.Add(new MarkdownChemInline(kind, text));
		}

		plainText.Append(text);
	}
}
