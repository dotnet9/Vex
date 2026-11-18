using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 行内 <c>&lt;span style="..."&gt;</c> 的白名单样式（color / background-color / font-size）。
/// 值保留作者写法（如 "#c92a2a"、"18px"），渲染端再经 <see cref="TryGetColor"/> / <see cref="TryGetFontSize"/>
/// 解析为画刷与字号；白名单外或解析失败的属性被丢弃，避免任意样式注入。
/// </summary>
public sealed record MarkdownSpanStyle(string? Foreground = null, string? Background = null, double? FontSize = null)
{
    public bool IsEmpty => Foreground is null && Background is null && FontSize is null;

    /// <summary>内层样式覆盖外层（未声明的属性继承外层），模拟 CSS 层叠。</summary>
    public MarkdownSpanStyle Over(MarkdownSpanStyle inner) => new(
        inner.Foreground ?? Foreground,
        inner.Background ?? Background,
        inner.FontSize ?? FontSize);

    private static readonly Regex DeclarationPattern = new(
        @"(color|background-color|font-size)\s*:\s*([^;""']+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>解析 style 属性文本；只保留白名单属性，全部无效时返回 null。</summary>
    public static MarkdownSpanStyle? FromDeclarations(string? styleText)
    {
        if (string.IsNullOrWhiteSpace(styleText))
        {
            return null;
        }

        string? foreground = null, background = null;
        double? fontSize = null;
        foreach (Match match in DeclarationPattern.Matches(styleText))
        {
            var value = match.Groups[2].Value.Trim();
            switch (match.Groups[1].Value.ToLowerInvariant())
            {
                case "color" when ParseColor(value).HasValue:
                    foreground = value;
                    break;
                case "background-color" when ParseColor(value).HasValue:
                    background = value;
                    break;
                case "font-size":
                    fontSize = TryGetFontSize(value);
                    break;
            }
        }

        var style = new MarkdownSpanStyle(foreground, background, fontSize);
        return style.IsEmpty ? null : style;
    }

    /// <summary>字号：仅接受 px（与原型约定一致），clamp 8–64；非法值返回 null。</summary>
    public static double? TryGetFontSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value.Trim(), @"^(\d{1,3}(?:\.\d+)?)px$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return Math.Clamp(double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 8, 64);
    }

    /// <summary>
    /// 作者色可读性回退（移植 StreamMark HtmlPaint 的 Readable 策略）：作者色与背景对比度不足时
    /// 放弃该色改用主题色，避免暗色主题下深色文字变黑洞。背景优先取 span 自身背景色，
    /// 否则按前景亮度推断主题底（亮文字视为暗色主题 #1E1E1E，否则白色）。
    /// </summary>
    public static Color? EnsureReadable(Color? authored, Color? background, Color fallback)
    {
        if (authored is not { } color)
        {
            return null;
        }

        var surface = background ?? InferSurface(color);
        return ContrastRatio(color, surface) < 2.2 ? fallback : color;
    }

    private static Color InferSurface(Color foreground)
    {
        return RelativeLuminance(foreground) > 0.5
            ? Color.FromRgb(0x1E, 0x1E, 0x1E)
            : Colors.White;
    }

    private static double ContrastRatio(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var lighter = Math.Max(la, lb);
        var darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.03928 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    /// <summary>颜色解析：#RGB/#RGBA/#RRGGBB/#RRGGBBAA、rgb()/rgba()、常用 CSS 命名色。</summary>
    public static Color? TryGetColor(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ColorCache.GetOrAdd(value.Trim(), ParseColor);
    }

    /// <summary>按 CSS 值取共享画刷（解析失败返回 null），避免每次渲染新建 SolidColorBrush。</summary>
    public static IBrush? GetBrush(string? value)
    {
        return TryGetColor(value) is { } color ? BrushCache.GetOrAdd((color.A, color.R, color.G, color.B), ToBrush) : null;

        static IBrush ToBrush((byte A, byte R, byte G, byte B) key)
        {
            return new SolidColorBrush(Color.FromArgb(key.A, key.R, key.G, key.B));
        }
    }

    private static readonly ConcurrentDictionary<string, Color?> ColorCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<(byte A, byte R, byte G, byte B), IBrush> BrushCache = new();

    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = Colors.Black, ["white"] = Colors.White, ["red"] = Colors.Red, ["green"] = Colors.Green,
        ["blue"] = Colors.Blue, ["yellow"] = Colors.Yellow, ["orange"] = Colors.Orange, ["purple"] = Colors.Purple,
        ["gray"] = Colors.Gray, ["grey"] = Colors.Gray, ["silver"] = Colors.Silver, ["maroon"] = Colors.Maroon,
        ["olive"] = Colors.Olive, ["lime"] = Colors.Lime, ["aqua"] = Colors.Aqua, ["cyan"] = Colors.Cyan,
        ["teal"] = Colors.Teal, ["navy"] = Colors.Navy, ["fuchsia"] = Colors.Fuchsia, ["magenta"] = Colors.Magenta,
        ["pink"] = Colors.Pink, ["brown"] = Colors.Brown, ["gold"] = Colors.Gold, ["indigo"] = Colors.Indigo,
        ["violet"] = Colors.Violet, ["coral"] = Colors.Coral, ["tomato"] = Colors.Tomato, ["salmon"] = Colors.Salmon,
        ["khaki"] = Colors.Khaki, ["beige"] = Colors.Beige, ["ivory"] = Colors.Ivory, ["transparent"] = Colors.Transparent,
    };

    private static Color? ParseColor(string value)
    {
        var text = value.Trim();
        if (NamedColors.TryGetValue(text, out var named))
        {
            return named;
        }

        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            try
            {
                byte Hex(int index, int length) => Convert.ToByte(hex.Substring(index, length), 16);
                byte Scaled(int index) => (byte)(Hex(index, 1) * 17);
                return hex.Length switch
                {
                    3 => Color.FromRgb(Scaled(0), Scaled(1), Scaled(2)),
                    4 => WithAlpha(Color.FromRgb(Scaled(0), Scaled(1), Scaled(2)), Scaled(3)),
                    6 => Color.FromRgb(Hex(0, 2), Hex(2, 2), Hex(4, 2)),
                    8 => WithAlpha(Color.FromRgb(Hex(0, 2), Hex(2, 2), Hex(4, 2)), Hex(6, 2)),
                    _ => null,
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }

        var rgb = Regex.Match(text, @"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:[,/]\s*([\d.]+)\s*)?\)$", RegexOptions.IgnoreCase);
        if (rgb.Success)
        {
            return Color.FromRgb(
                byte.Parse(rgb.Groups[1].Value),
                byte.Parse(rgb.Groups[2].Value),
                byte.Parse(rgb.Groups[3].Value));
        }

        return null;

        static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
