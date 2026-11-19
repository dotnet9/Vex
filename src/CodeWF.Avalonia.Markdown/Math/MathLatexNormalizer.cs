using System.Text;
using System.Text.RegularExpressions;

namespace CodeWF.Avalonia.Markdown.MathRendering;

/// <summary>
/// LaTeX 文本规范化：把 \ce{} 化学式降级为上下标纯文本，
/// 供 CSharpMath 渲染前的输入清洗使用。
/// </summary>
internal static class MathLatexNormalizer
{
    public static string NormalizeLatex(string latex)
    {
        if (string.IsNullOrWhiteSpace(latex) || !latex.Contains(@"\ce{", StringComparison.Ordinal))
        {
            return latex;
        }

        var builder = new StringBuilder(latex.Length);
        var index = 0;
        while (index < latex.Length)
        {
            var ceStart = latex.IndexOf(@"\ce{", index, StringComparison.Ordinal);
            if (ceStart < 0)
            {
                builder.Append(latex[index..]);
                break;
            }

            builder.Append(latex[index..ceStart]);
            var contentStart = ceStart + 4;
            var contentEnd = FindMatchingBrace(latex, contentStart - 1);
            if (contentEnd < 0)
            {
                builder.Append(latex[ceStart..]);
                break;
            }

            builder.Append(ConvertChemExpression(latex[contentStart..contentEnd]));
            index = contentEnd + 1;
        }

        return builder.ToString();
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

    private static string ConvertChemExpression(string expression)
    {
        var tokens = Regex.Split(expression.Trim(), @"\s+").Where(token => token.Length > 0);
        return string.Join(" ", tokens.Select(ConvertChemToken));
    }

    private static string ConvertChemToken(string token)
    {
        var arrowMatch = Regex.Match(token, @"^(?<arrow><->|->|<-)(\[(?<label>[^\]]+)\])?$");
        if (arrowMatch.Success)
        {
            var arrow = arrowMatch.Groups["arrow"].Value switch
            {
                "<-" => @"\leftarrow",
                "<->" => @"\leftrightarrow",
                _ => @"\longrightarrow"
            };
            return arrowMatch.Groups["label"].Success
                ? $@"{arrow}^{{{ConvertChemFormula(arrowMatch.Groups["label"].Value)}}}"
                : arrow;
        }

        return ConvertChemFormula(token);
    }

    private static string ConvertChemFormula(string formula)
    {
        var builder = new StringBuilder(formula.Length * 2);
        for (var i = 0; i < formula.Length; i++)
        {
            var c = formula[i];
            if (char.IsUpper(c))
            {
                var start = i;
                i++;
                while (i < formula.Length && char.IsLower(formula[i]))
                {
                    i++;
                }
                builder.Append(@"\mathrm{").Append(formula[start..i]).Append('}');
                i--;
            }
            else if (char.IsDigit(c))
            {
                var start = i;
                while (i + 1 < formula.Length && char.IsDigit(formula[i + 1]))
                {
                    i++;
                }
                builder.Append("_{").Append(formula[start..(i + 1)]).Append('}');
            }
            else if (c == '^')
            {
                var value = ReadScriptValue(formula, ref i);
                builder.Append("^{").Append(ConvertScriptText(value)).Append('}');
            }
            else if ((c == '+' || c == '-') && i == formula.Length - 1)
            {
                builder.Append("^{").Append(c).Append('}');
            }
            else if (char.IsLetter(c))
            {
                builder.Append(@"\mathrm{").Append(c).Append('}');
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
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

    private static string ConvertScriptText(string text)
    {
        return text.All(c => char.IsLetter(c))
            ? $@"\mathrm{{{text}}}"
            : text;
    }
}
