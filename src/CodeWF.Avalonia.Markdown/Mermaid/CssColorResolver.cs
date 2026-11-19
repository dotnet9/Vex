// CSS 自定义属性与 color-mix() 的字面量化。
// Skia 不实现 CSS 级联：fill="var(--accent, #FAFAFA)"、color-mix(in srgb, …) 这类写法会被当成
// 无效颜色直接画成黑色——象限图四格、分组标题这类「带回退的嵌套表达式」整块糊掉。
// 这里只按 CSS 的求值顺序把 var()/color-mix() 展平成字面量，不实现 var() 之外的级联语义。

using System.Globalization;
using System.Text;

namespace CodeWF.Avalonia.Markdown.Mermaid;

internal static class CssColorResolver
{
    private const int MaxDepth = 24;

    private readonly record struct Rgba(byte R, byte G, byte B, byte A)
    {
        public readonly bool Opaque => A == byte.MaxValue;
    }

    /// <summary>
    /// 把 <paramref name="text"/> 里所有 <c>var()</c> 与 <c>color-mix()</c> 展开成字面量。
    /// <paramref name="tokens"/> 是「--变量名 → 字面量」的权威表，命中即用它，未命中才走回退值。
    /// </summary>
    public static string Flatten(string text, IReadOnlyDictionary<string, string> tokens)
        => Substitute(text, tokens, 0);

    private static string Substitute(string text, IReadOnlyDictionary<string, string> tokens, int depth)
    {
        if (depth > MaxDepth || text.Length == 0) return text;

        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        while (cursor < text.Length)
        {
            var varAt = text.IndexOf("var(", cursor, StringComparison.Ordinal);
            var mixAt = text.IndexOf("color-mix(", cursor, StringComparison.Ordinal);
            if (varAt < 0 && mixAt < 0)
            {
                builder.Append(text, cursor, text.Length - cursor);
                break;
            }

            // 两个函数都可能嵌套出现，先处理更靠前的那个，剩下的交给递归。
            int start, head;
            if (mixAt >= 0 && (varAt < 0 || mixAt < varAt))
            {
                start = mixAt;
                head = "color-mix(".Length;
            }
            else
            {
                start = varAt;
                head = "var(".Length;
            }

            var close = MatchParen(text, start + head - 1);
            builder.Append(text, cursor, start - cursor);
            if (close < 0)
            {
                builder.Append(text, start, text.Length - start);
                break;
            }

            var body = text[(start + head)..close];
            var resolved = head == 4
                ? ResolveVar(body, tokens, depth)
                : ResolveMix(body, tokens, depth);
            // 解不出来就原样交回，让调用方的兜底逻辑决定怎么处理，别把文档改坏。
            builder.Append(resolved ?? text[start..(close + 1)]);
            cursor = close + 1;
        }

        return builder.ToString();
    }

    private static string? ResolveVar(string body, IReadOnlyDictionary<string, string> tokens, int depth)
    {
        // 回退值本身可以含逗号（color-mix、rgb），所以只在第一个顶层逗号处切一刀。
        var split = IndexOfTopLevel(body, ',');
        var name = (split < 0 ? body : body[..split]).Trim();
        if (name.StartsWith("--", StringComparison.Ordinal) && tokens.TryGetValue(name, out var token))
        {
            return Substitute(token, tokens, depth + 1);
        }

        // CSS 里的空白 insignificant，但展开后要落到 fill 属性上，留着一个前导空格就是脏值。
        return split < 0 ? null : Substitute(body[(split + 1)..].Trim(), tokens, depth + 1);
    }

    private static string? ResolveMix(string body, IReadOnlyDictionary<string, string> tokens, int depth)
    {
        var parts = SplitTopLevel(body, ',');
        if (parts.Count < 3) return null;

        // 语法是 color-mix(in srgb, A p%, B q%)；色彩空间段不参与计算，只按位置取两色。
        var first = Substitute(parts[1], tokens, depth + 1);
        var second = Substitute(parts[2], tokens, depth + 1);
        if (!TryColor(first, out var a) || !TryColor(second, out var b)) return null;

        // 只写一侧时另一侧取补数（CSS 里 A 16% 就是 B 84%）；两侧都写才按和归一。
        // 把缺省当成 50% 会把 16% 算成 16/66，节点底色整体偏深。
        var hasA = TryPercent(first, out var rawA);
        var hasB = TryPercent(second, out var rawB);
        double aPercent, bPercent;
        if (hasA && !hasB)
        {
            aPercent = rawA;
            bPercent = 100 - rawA;
        }
        else if (hasB && !hasA)
        {
            bPercent = rawB;
            aPercent = 100 - rawB;
        }
        else
        {
            aPercent = hasA ? rawA : 50;
            bPercent = hasB ? rawB : 50;
            var total = aPercent + bPercent;
            if (total <= 0) return null;
            aPercent = aPercent * 100 / total;
            bPercent = 100 - aPercent;
        }

        if (aPercent < 0 || bPercent < 0) return null;

        // 通道按预乘 alpha 插值：透明的一侧不该贡献颜色，否则 transparent 会把邻色拽向纯黑。
        // 取整口径沿用 MermaidBlockRenderer 镜像公式的截断，两条路径算出的同一个色不会差 1。
        var weightA = aPercent / 100;
        var weightB = bPercent / 100;
        var alphaA = a.A / 255d;
        var alphaB = b.A / 255d;
        var resultAlpha = alphaA * weightA + alphaB * weightB;

        byte Channel(byte left, byte right) => resultAlpha <= 0
            ? (byte)0
            : (byte)Math.Clamp((left * alphaA * weightA + right * alphaB * weightB) / resultAlpha, 0, 255);

        return Format(new Rgba(
            Channel(a.R, b.R),
            Channel(a.G, b.G),
            Channel(a.B, b.B),
            resultAlpha <= 0 ? (byte)0 : (byte)Math.Clamp(resultAlpha * 255, 0, 255)));
    }

    private static string Format(Rgba color) => color.Opaque
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"rgba({color.R}, {color.G}, {color.B}, {Math.Round(color.A / 255d, 3):0.###})";

    /// <summary>解析 <c>#rgb</c>/<c>#rrggbb</c>/<c>#rrggbbaa</c>/<c>rgb()</c>/<c>rgba()</c>/<c>transparent</c> 与常用颜色名。</summary>
    private static bool TryColor(string? text, out Rgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = StripPercent(text.Trim());
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = new Rgba(0, 0, 0, 0);
            return true;
        }

        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length is 3 or 4)
            {
                hex = string.Concat(hex.Select(channel => new string(channel, 2)));
            }

            if (hex.Length is not (6 or 8) || !IsHex(hex)) return false;
            byte Channel(int offset) => Convert.ToByte(hex.Substring(offset, 2), 16);
            color = new Rgba(Channel(0), Channel(2), Channel(4), hex.Length == 8 ? Channel(6) : byte.MaxValue);
            return true;
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = value.IndexOf('(');
            var close = value.LastIndexOf(')');
            if (open < 0 || close < open) return false;
            var channels = value[(open + 1)..close].Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (channels.Length is < 3 or > 4) return false;

            static byte Channel(string raw)
            {
                if (raw.EndsWith('%')
                    && double.TryParse(raw[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    return (byte)Math.Clamp(Math.Round(percent * 2.55), 0, 255);
                }

                return byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteValue)
                    ? byteValue
                    : (byte)Math.Clamp(Math.Round(double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture)), 0, 255);
            }

            var alpha = channels.Length == 4 ? ToAlpha(channels[3]) : byte.MaxValue;
            color = new Rgba(Channel(channels[0]), Channel(channels[1]), Channel(channels[2]), alpha);
            return true;
        }

        if (NamedColors.TryGetValue(value, out var named))
        {
            color = named;
            return true;
        }

        return false;
    }

    private static byte ToAlpha(string raw)
    {
        if (raw.EndsWith('%')
            && double.TryParse(raw[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            return (byte)Math.Clamp(Math.Round(percent * 2.55), 0, 255);
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return byte.MaxValue;
        return (byte)Math.Clamp(Math.Round(value <= 1 ? value * 255 : value), 0, 255);
    }

    /// <summary>color-mix 的百分比是跟在颜色后面的独立记号，因此只认结尾的那一个。</summary>
    private static bool TryPercent(string text, out double percent)
    {
        percent = 0;
        var trimmed = text.TrimEnd();
        if (trimmed.Length < 2 || trimmed[^1] != '%') return false;
        var start = trimmed.Length - 1;
        while (start > 0 && (char.IsDigit(trimmed[start - 1]) || trimmed[start - 1] is '.' or '-')) start--;
        return start < trimmed.Length - 1
            && double.TryParse(trimmed[start..(trimmed.Length - 1)], NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
    }

    private static string StripPercent(string text)
    {
        if (!TryPercent(text, out _)) return text;
        var trimmed = text.TrimEnd();
        var start = trimmed.Length - 1;
        while (start > 0 && (char.IsDigit(trimmed[start - 1]) || trimmed[start - 1] is '.' or '-')) start--;
        return trimmed[..start].Trim();
    }

    private static bool IsHex(string value)
        => value.All(Uri.IsHexDigit);

    /// <summary>从 <paramref name="open"/> 指向的 '(' 找到配对的 ')'，引号内的括号不参与计数。</summary>
    private static int MatchParen(string text, int open)
    {
        var depth = 0;
        char quote = '\0';
        for (var index = open; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfTopLevel(string text, char separator)
    {
        var depth = 0;
        char quote = '\0';
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                continue;
            }

            if (character is '"' or '\'') quote = character;
            else if (character == '(') depth++;
            else if (character == ')') depth--;
            else if (character == separator && depth == 0) return index;
        }

        return -1;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var cursor = 0;
        while (cursor <= text.Length)
        {
            var next = cursor >= text.Length ? -1 : IndexOfTopLevel(text[cursor..], separator);
            if (next < 0)
            {
                parts.Add(text[cursor..]);
                break;
            }

            parts.Add(text[cursor..(cursor + next)]);
            cursor += next + 1;
        }

        return parts;
    }

    private static readonly Dictionary<string, Rgba> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new Rgba(0, 0, 0, 255),
        ["silver"] = new Rgba(192, 192, 192, 255),
        ["gray"] = new Rgba(128, 128, 128, 255),
        ["grey"] = new Rgba(128, 128, 128, 255),
        ["white"] = new Rgba(255, 255, 255, 255),
        ["maroon"] = new Rgba(128, 0, 0, 255),
        ["red"] = new Rgba(255, 0, 0, 255),
        ["purple"] = new Rgba(128, 0, 128, 255),
        ["fuchsia"] = new Rgba(255, 0, 255, 255),
        ["green"] = new Rgba(0, 128, 0, 255),
        ["lime"] = new Rgba(0, 255, 0, 255),
        ["olive"] = new Rgba(128, 128, 0, 255),
        ["yellow"] = new Rgba(255, 255, 0, 255),
        ["navy"] = new Rgba(0, 0, 128, 255),
        ["blue"] = new Rgba(0, 0, 255, 255),
        ["teal"] = new Rgba(0, 128, 128, 255),
        ["aqua"] = new Rgba(0, 255, 255, 255),
        ["orange"] = new Rgba(255, 165, 0, 255)
    };
}
