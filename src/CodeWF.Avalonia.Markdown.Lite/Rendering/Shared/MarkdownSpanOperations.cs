using System.Text.RegularExpressions;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 行内 <c>&lt;span style&gt;</c> 文本操作（选区包装 / 清除），与原型 document-operations 的行为一致：
/// 选区紧贴同款 span 时改写或解包（同值再点一次 = toggle），否则新包一层；属性合并进既有开标签。
/// </summary>
public static class MarkdownSpanOperations
{
    /// <summary>支持的样式属性（与 <see cref="MarkdownSpanStyle"/> 白名单一致）。</summary>
    public static readonly IReadOnlyCollection<string> Properties = ["color", "background-color", "font-size"];

    /// <summary>把 [start, end) 包上 <c>&lt;span style="property:value"&gt;</c>；返回新文本与新选区。
    /// toggle=true（默认）时同款同值再套一次即解包；选色器等明确设值路径传 false。</summary>
    public static (string Text, int Start, int End) Wrap(string text, int start, int end, string property, string value, bool toggle = true)
    {
        if (start >= end || !Properties.Contains(property))
        {
            return (text, start, end);
        }

        // 只看紧贴选区左侧的开标签：取窗口里最近一个 '<span'，更早的 span 不得干扰合并判定。
        var windowStart = Math.Max(0, start - 200);
        var window = text[windowStart..start];
        var at = window.LastIndexOf("<span", StringComparison.OrdinalIgnoreCase);
        var open = at >= 0 ? OpenPattern.Match(window[at..]) : Match.Empty;
        if (open.Success && open.Index == 0 && end + 7 <= text.Length && text.Substring(end, 7) == "</span>")
        {
            var absolute = windowStart + at;
            var inner = text[start..end];
            var style = MarkdownSpanStyle.FromDeclarations(open.Groups[1].Value) ?? new MarkdownSpanStyle();
            var current = property switch
            {
                "color" => style.Foreground,
                "background-color" => style.Background,
                "font-size" => style.FontSize is { } size ? size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "px" : null,
                _ => null,
            };
            var updated = string.Equals(current, value, StringComparison.OrdinalIgnoreCase) ? null : Merge(style, property, value);
            if (updated is null)
            {
                if (!toggle)
                {
                    return (text, start, end);
                }

                // 同值再点一次：解包。
                return (text[..absolute] + inner + text[(end + 7)..], absolute, absolute + inner.Length);
            }

            var openTag = SerializeOpenTag(updated);
            return (text[..absolute] + openTag + inner + "</span>" + text[(end + 7)..],
                absolute + openTag.Length, absolute + openTag.Length + inner.Length);
        }

        var freshTag = SerializeOpenTag(new MarkdownSpanStyle(
            property == "color" ? value : null,
            property == "background-color" ? value : null,
            property == "font-size" ? MarkdownSpanStyle.TryGetFontSize(value) : null));
        return (text[..start] + freshTag + text[start..end] + "</span>" + text[end..],
            start + freshTag.Length, end + freshTag.Length);
    }

    /// <summary>剥掉 [start, end) 区间内所有 span 标签；返回新文本与新选区。</summary>
    public static (string Text, int Start, int End) Clear(string text, int start, int end)
    {
        var cuts = new List<(int From, int To)>();
        foreach (Match match in AnySpanTag.Matches(text))
        {
            if (match.Index < end && match.Index + match.Length > start)
            {
                cuts.Add((match.Index, match.Index + match.Length));
            }
        }

        if (cuts.Count == 0)
        {
            return (text, start, end);
        }

        int Map(int position)
        {
            var delta = 0;
            foreach (var (from, to) in cuts)
            {
                if (position >= to)
                {
                    delta += to - from;
                }
                else if (position > from)
                {
                    return from - delta;
                }
                else
                {
                    break;
                }
            }

            return Math.Max(0, position - delta);
        }

        var builder = new System.Text.StringBuilder();
        var cursor = 0;
        foreach (var (from, to) in cuts)
        {
            builder.Append(text, cursor, from - cursor);
            cursor = to;
        }

        builder.Append(text[cursor..]);
        return (builder.ToString(), Map(start), Map(end));
    }

    /// <summary>读选区所在 span 的当前属性值：选区紧贴同款 span（开标签结束于 start、闭标签始于 end）时返回样式值，否则 null。</summary>
    public static string? CurrentValue(string text, int start, int end, string property)
    {
        if (start > end || !Properties.Contains(property))
        {
            return null;
        }

        var windowStart = Math.Max(0, start - 200);
        var window = text[windowStart..start];
        var at = window.LastIndexOf("<span", StringComparison.OrdinalIgnoreCase);
        var open = at >= 0 ? OpenPattern.Match(window[at..]) : Match.Empty;
        if (!open.Success || open.Index != 0 || end + 7 > text.Length || text.Substring(end, 7) != "</span>")
        {
            return null;
        }

        var style = MarkdownSpanStyle.FromDeclarations(open.Groups[1].Value);
        return property switch
        {
            "color" => style?.Foreground,
            "background-color" => style?.Background,
            "font-size" => style?.FontSize is { } size ? size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "px" : null,
            _ => null,
        };
    }

    /// <summary>合并一个属性；返回 null 表示同值需要解包。</summary>
    private static MarkdownSpanStyle? Merge(MarkdownSpanStyle style, string property, string value)
    {
        var current = property switch
        {
            "color" => style.Foreground,
            "background-color" => style.Background,
            "font-size" => style.FontSize is { } size ? size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "px" : null,
            _ => null,
        };
        if (current == value)
        {
            return null;
        }

        return new MarkdownSpanStyle(
            property == "color" ? value : style.Foreground,
            property == "background-color" ? value : style.Background,
            property == "font-size" ? MarkdownSpanStyle.TryGetFontSize(value) : style.FontSize);
    }

    private static string SerializeOpenTag(MarkdownSpanStyle style)
    {
        var declarations = new List<string>();
        if (style.Foreground is { } foreground)
        {
            declarations.Add("color:" + foreground);
        }

        if (style.Background is { } background)
        {
            declarations.Add("background-color:" + background);
        }

        if (style.FontSize is { } fontSize)
        {
            declarations.Add("font-size:" + fontSize.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "px");
        }

        return "<span style=\"" + string.Join(";", declarations) + "\">";
    }

    private static readonly Regex OpenPattern = new(
        @"^<span\b[^>]*\bstyle\s*=\s*""([^""]*)""[^>]*>$|^<span\b[^>]*\bstyle\s*=\s*'([^']*)'[^>]*>$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AnySpanTag = new(
        @"<span\b[^>]*>|</span\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
}
