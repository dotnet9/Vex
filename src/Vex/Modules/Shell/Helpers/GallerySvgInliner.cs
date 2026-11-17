using System.Globalization;
using System.Text.RegularExpressions;

namespace Vex.Modules.Shell.Helpers;

/// <summary>
/// Mermaider 输出的 SVG 依赖宿主注入的 CSS 自定义属性（var(--fg)/var(--bg) 等）与 color-mix，
/// 文档内渲染有主题 CSS 撑着，而缩略图走 SvgImage 光栅化（不解析 CSS 变量）会落黑块。
/// 这里把变量与 color-mix 全部内联为字面量色值，让缩略图自包含。
/// </summary>
internal static partial class GallerySvgInliner
{
    private static readonly Dictionary<string, string> FixedVars = new(StringComparer.Ordinal)
    {
        ["--fs-xs"] = "10px",
        ["--fs-s"] = "12px",
        ["--fs-m"] = "14px",
        ["--fs-l"] = "18px",
        ["--muted"] = "#647586",
        ["--line"] = "#456789",
        ["--surface"] = "#F3E4D5",
        ["--border"] = "#B4C5D6",
    };

    // var(变量) 与 var(变量, 回退值) 两种形式；回退值里允许一层 var（如 var(--accent, var(--fg))）。
    [GeneratedRegex(@"var\(\s*(--[a-zA-Z0-9_-]+)\s*(?:,\s*((?:[^()]|\(var\([a-zA-Z0-9_-]+\)\))*?)\s*)?\)", RegexOptions.Compiled)]
    private static partial Regex VarPattern();

    [GeneratedRegex(@"(--_[a-zA-Z0-9_-]+)\s*:\s*([^;{}]+);", RegexOptions.Compiled)]
    private static partial Regex DerivedDefinitionPattern();

    [GeneratedRegex(@"color-mix\(\s*in srgb\s*,\s*(?<a>[^,()]+?)\s+(?<ratio>\d+)%\s*,\s*(?<b>[^()]+?)\s*\)", RegexOptions.Compiled)]
    private static partial Regex ColorMixPattern();

    [GeneratedRegex(@"<style>.*?</style>", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex StyleBlockPattern();

    /// <summary>把 SVG 里的 CSS 变量与 color-mix 内联为字面量；返回原串若无匹配。</summary>
    public static string Inline(string svg, string foreground, string background, string accent)
    {
        var themed = new Dictionary<string, string>(FixedVars, StringComparer.Ordinal)
        {
            ["--fg"] = foreground,
            ["--bg"] = background,
            ["--accent"] = accent,
        };
        // 有表值用表值；无表值时用回退值（嵌套 var 已由第一轮递归解析的假设不成立，
        // 这里先把回退值原样放回，下一轮 VarPattern 会再处理其中的 var）。
        string ResolveVar(Match match)
        {
            if (themed.TryGetValue(match.Groups[1].Value, out var known)) return known;
            return match.Groups[2].Success ? match.Groups[2].Value : match.Value;
        }
        string previous;
        var result = svg;
        do
        {
            previous = result;
            result = VarPattern().Replace(result, ResolveVar);
        }
        while (result != previous);
        // 内部派生变量（--_xxx）在 style 块里由外部变量/color-mix 定义，先取值再替换使用点。
        var derived = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in DerivedDefinitionPattern().Matches(result)
                     .Cast<Match>()
                     .Where(match => match.Groups[1].Value.StartsWith("--_")))
        {
            derived[definition.Groups[1].Value] = definition.Groups[2].Value.Trim();
        }
        if (derived.Count > 0)
        {
            do
            {
                previous = result;
                result = VarPattern().Replace(result, match => derived.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
            }
            while (result != previous);
        }
        // color-mix 先内层后外层循环替换，直到不再变化（嵌套混合通常不超过两层）。
        for (var round = 0; round < 3; round++)
        {
            result = ColorMixPattern().Replace(result, match =>
            {
                var first = ToColor(match.Groups["a"].Value.Trim());
                var second = ToColor(match.Groups["b"].Value.Trim());
                if (first is null || second is null) return match.Value;
                var ratio = int.Parse(match.Groups["ratio"].Value, CultureInfo.InvariantCulture) / 100.0;
                return Mix(first.Value, second.Value, ratio);
            });
        }
        // style 块只剩变量定义与 class 规则（drop-shadow/字体），SvgImage 均不解析；使用点已全部内联，整块删除。
        return StyleBlockPattern().Replace(result, string.Empty);
    }

    private static (byte R, byte G, byte B)? ToColor(string text)
    {
        text = text.Trim();
        if (text.StartsWith('#') && text.Length is 7)
        {
            return (
                byte.Parse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        return null;
    }

    private static string Mix((byte R, byte G, byte B) first, (byte R, byte G, byte B) second, double ratio)
    {
        static byte Channel(byte a, byte b, double weight) => (byte)Math.Round(a * weight + b * (1 - weight));
        return $"#{Channel(first.R, second.R, ratio):X2}{Channel(first.G, second.G, ratio):X2}{Channel(first.B, second.B, ratio):X2}";
    }
}
