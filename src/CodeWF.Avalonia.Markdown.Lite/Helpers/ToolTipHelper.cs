// 统一文本提示封装（与 Controls 的 ToolTipHelper.CreateTextTip 同语义的本地实现——
// Lite 不引用 Controls（避免连带 Semi 依赖），改 Controls/MindView 实现时需同步）：
// 字符串提示用 TextBlock 承载，长文案先按宽度启发式确定性预折行（插入 '\n'），
// 再由 TextBlock 的 Wrap + 最大宽度兜底，规避 ToolTip 弹窗首测 re-wrap 竞态导致的非确定性截断；
// 前景走 ToolTipForeground 主题令牌，随明暗主题反色跟随。
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Helpers;

/// <summary>
///     普通文本 ToolTip 的统一封装（Lite 本地实现）。
///     用 TextBlock 承载字符串提示：确定性预折行、本地钉死宽度/高度、主题前景色；
///     空文本不显示提示。代码块工具条等图标钮统一走这里，避免裸字符串提示
///     吃不到宿主主题的前景令牌（真机表现为深色底看不清字的提示框）。
/// </summary>
internal static class ToolTipHelper
{
    /// <summary>默认最大宽度：与 Semi 的 ToolTipMaxWidth(320) 对齐，长文案在此折行。</summary>
    private const double DefaultMaxWidth = 320;

    /// <summary>宽度启发式基准：1em 取提示实际字号（宿主 Chrome.axaml 给 ToolTip 设的 12）。</summary>
    private const double TipFontSize = 12;

    /// <summary>ASCII 字符估宽系数：比例字体下平均约 0.6em。</summary>
    private const double AsciiWidthInEm = 0.6;

    /// <summary>行宽安全余量：扣除主题 Padding（如 10,7）与字形测量误差，预折行行宽不贴 MaxWidth 边。</summary>
    private const double LineWidthMargin = 40;

    /// <summary>Width 余量：估宽按 0.6em 均值偏保守，加一小段让真实字形（如全大写路径）不断行错行。</summary>
    private const double TipWidthMargin = 16;

    /// <summary>保守行高：FontSize 12 下按 17px/行 预留（实际约 16px），宁可略高不裁字。</summary>
    private const double TipLineHeight = 17;

    /// <summary>TextBlock.Height 安全余量：行高估算误差再垫 4px。</summary>
    private const double TipHeightMargin = 4;

    /// <summary>ToolTip.Height 垂直余量：宿主 Chrome.axaml ToolTip Padding=10,7 → 垂直 14，再加 6px 余量。</summary>
    private const double TipVerticalMargin = 20;

    /// <summary>给控件挂统一文本提示；空文本不挂。</summary>
    internal static void SetTip(Control control, string? text)
    {
        var tip = CreateTextTip(text);
        if (tip is not null)
        {
            ToolTip.SetTip(control, tip);
        }
    }

    /// <summary>
    ///     创建统一样式的 ToolTip：确定性预折行、本地钉死宽度、前景跟随主题令牌；空文本返回 null（不显示提示）。
    ///     长文案在生成前按宽度启发式预算成多行插入 '\n'：ToolTip 弹窗首测存在 re-wrap 竞态，
    ///     同一长文案有时单行硬裁、有时只显示盘符、有时正常折行——预折行后 TextWrapping=Wrap 仅作兜底。
    ///     显式创建 ToolTip 实例并把 MaxWidth/Height 写成本地值：宿主主题（如 Semi 的 320）是样式值，
    ///     本地值优先——弹窗首测即终态，不依赖宿主模板的测量/挂树时机。
    /// </summary>
    internal static object? CreateTextTip(string? text, double maxWidth = DefaultMaxWidth)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var wrapped = WrapText(text, maxWidth);
        // Height 本地值钉死：行数取自预折行结果，行高用保守常量——弹窗首测高度曾被记成单行，
        // 第二行被窗口裁掉；宁可略高不裁字，首测即终态。
        var height = wrapped.Split('\n').Length * TipLineHeight + TipHeightMargin;
        return new ToolTip
        {
            MaxWidth = maxWidth,
            // ToolTip.Height 本地值 = 内容高 + 垂直 Padding 余量（Chrome.axaml ToolTip Padding=10,7 → 垂直 14 + 6 余量）。
            Height = height + TipVerticalMargin,
            Content = new TextBlock
            {
                // 确定性预折行：不依赖弹窗首测/重排时机；Wrap 只兜估宽偏小的行（如全大写 ASCII）。
                Text = wrapped,
                TextWrapping = TextWrapping.Wrap,
                // Width 本地值钉死内容宽度：取各行估宽最大值 + 余量并 clamp，首测即终态。
                Width = MeasureTipWidth(wrapped, maxWidth),
                // Height 本地值钉死内容高度：行数 × 保守行高 + 余量，与 Width 同一竞态的另一半，首测即终态。
                Height = height,
                // 本地 MaxWidth 写在 TextBlock 上：宿主主题模板不传导 ToolTip.MaxWidth 时依旧折行不裁切。
                MaxWidth = maxWidth,
                [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("ToolTipForeground")
            }
        };
    }

    /// <summary>
    ///     确定性预折行：按字符类别估宽（CJK/全角 1.0em、ASCII 0.6em）把长文案贪心预算成多行并插入 '\n'。
    ///     行宽预算 = maxWidth - 安全余量；不超预算的文本原样返回（常规短提示零改动），
    ///     已含换行的文本保留原有硬断行；只插 '\n' 不丢字符。
    /// </summary>
    private static string WrapText(string text, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var budget = Math.Max(TipFontSize, maxWidth - LineWidthMargin);
        if (MeasureTextWidth(text) <= budget) return text;

        var builder = new StringBuilder(text.Length + 16);
        var first = true;
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (!first) builder.Append('\n');
            first = false;
            AppendWrappedLine(builder, line, budget);
        }

        return builder.ToString();
    }

    /// <summary>钉死提示宽度：预折行结果各行估宽最大值 + 余量，clamp 到 maxWidth。</summary>
    private static double MeasureTipWidth(string wrappedText, double maxWidth)
    {
        var widest = 0d;
        foreach (var line in wrappedText.Split('\n')) widest = Math.Max(widest, MeasureTextWidth(line));
        return Math.Min(widest + TipWidthMargin, maxWidth);
    }

    /// <summary>估算文本渲染宽度（像素）：CJK/全角 1.0em、ASCII 0.6em、控制字符 0，其余保守按 1.0em。</summary>
    private static double MeasureTextWidth(string text)
    {
        var width = 0d;
        foreach (var character in text) width += GetCharWidth(character);
        return width;
    }

    /// <summary>贪心断行单个逻辑行：CJK 任意断、ASCII 在非字母数字边界断；不可断超长 token 按字符强断。</summary>
    private static void AppendWrappedLine(StringBuilder builder, string line, double budget)
    {
        var lineWidth = 0d;
        var hasContent = false;
        var index = 0;
        while (index < line.Length)
        {
            var end = GetTokenEnd(line, index);
            var tokenWidth = MeasureRangeWidth(line, index, end);

            if (hasContent && lineWidth + tokenWidth > budget)
            {
                builder.Append('\n');
                lineWidth = 0;
                hasContent = false;
            }

            if (!hasContent && tokenWidth > budget)
            {
                // 单个不可断超长 token（如无分隔符的长 ASCII 路径段）按字符强断，至少放一个字符保证推进。
                while (index < end)
                {
                    var width = GetCharWidth(line[index]);
                    if (hasContent && lineWidth + width > budget)
                    {
                        builder.Append('\n');
                        lineWidth = 0;
                        hasContent = false;
                    }

                    builder.Append(line[index]);
                    lineWidth += width;
                    hasContent = true;
                    index++;
                }

                continue;
            }

            builder.Append(line, index, end - index);
            lineWidth += tokenWidth;
            hasContent = true;
            index = end;
        }
    }

    /// <summary>取断行 token 终点：宽字符与 ASCII 标点各成一段（其后可断），连续字母数字为一段（不可断）。</summary>
    private static int GetTokenEnd(string line, int start)
    {
        var character = line[start];
        if (IsWideChar(character) || !char.IsLetterOrDigit(character)) return start + 1;

        var end = start + 1;
        while (end < line.Length && char.IsLetterOrDigit(line[end]) && !IsWideChar(line[end])) end++;
        return end;
    }

    private static double MeasureRangeWidth(string line, int start, int end)
    {
        var width = 0d;
        for (var index = start; index < end; index++) width += GetCharWidth(line[index]);
        return width;
    }

    private static double GetCharWidth(char character) =>
        char.IsControl(character) ? 0
            : IsWideChar(character) ? TipFontSize
            : character <= '\u007F' ? TipFontSize * AsciiWidthInEm
            : TipFontSize;

    /// <summary>宽字符（East Asian Wide/Fullwidth）：按 1.0em 估宽。</summary>
    private static bool IsWideChar(char character) =>
        character is >= '\u1100' and <= '\u115F'      // Hangul Jamo
            or >= '\u2E80' and <= '\u303E'            // CJK 部首、康熙部首、CJK 符号
            or >= '\u3041' and <= '\u33FF'            // 假名、注音、CJK 用符号
            or >= '\u3400' and <= '\u4DBF'            // CJK 扩展 A
            or >= '\u4E00' and <= '\u9FFF'            // CJK 统一表意
            or >= '\uA000' and <= '\uA4CF'            // 彝文
            or >= '\uAC00' and <= '\uD7A3'            // Hangul 音节
            or >= '\uF900' and <= '\uFAFF'            // CJK 兼容表意
            or >= '\uFE30' and <= '\uFE4F'            // CJK 兼容形式
            or >= '\uFF00' and <= '\uFF60'            // 全角 ASCII、半角片假名
            or >= '\uFFE0' and <= '\uFFE6';           // 全角符号
}
