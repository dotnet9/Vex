using System.Collections.Generic;
using System.Linq;

namespace CodeWF.Avalonia.Markdown.Editor;

/// <summary>自动配对选项。</summary>
public sealed class TextAutoPairOptions
{
    /// <summary>成对符号：键为左半边，值为右半边。</summary>
    public IReadOnlyDictionary<string, string> Pairs { get; init; } = DefaultPairs;

    /// <summary>允许"选区包裹"的符号集合；不在其中的符号只做插入不包裹。</summary>
    public IReadOnlySet<string> WrapSelectionKeys { get; init; } = DefaultWrapKeys;

    /// <summary>是否在输入右半边时跳过已有的右半边（默认开启）。</summary>
    public bool SkipExistingClosing { get; init; } = true;

    /// <summary>是否允许 Backspace 删除空配对的左右两半（默认开启）。</summary>
    public bool DeleteEmptyPairOnBackspace { get; init; } = true;

    public static readonly IReadOnlyDictionary<string, string> DefaultPairs =
        new Dictionary<string, string>
        {
            ["`"] = "`",
            ["("] = ")",
            ["["] = "]",
            ["{"] = "}",
            ["\""] = "\"",
            ["'"] = "'",
            ["$"] = "$",
            ["（"] = "）",
            ["【"] = "】",
            ["《"] = "》",
            ["“"] = "”",
        };

    public static readonly IReadOnlySet<string> DefaultWrapKeys =
        new HashSet<string> { "(", "[", "{", "\"", "'", "$", "（", "【", "《", "“" };
}

/// <summary>一次自动配对产生的文本改动。<see cref="CaretOffset"/> 为改动后的插入点。</summary>
public readonly record struct TextAutoPairResult(
    int Start,
    int Length,
    string Text,
    int CaretOffset);

/// <summary>
/// 自动配对（纯逻辑，无 UI 依赖）：成对符号插入、选区包裹、右半边跳过、
/// 空配对退格删除。宿主只需把结果应用到自己的文本模型并设置插入点。
/// <para>
/// 输入法组字过程中宿主不应调用本类（由宿主判断 preedit 状态）。
/// </para>
/// </summary>
public static class TextAutoPair
{
    /// <summary>处理一次字符输入；不需要配对时返回 null（宿主按原样插入）。</summary>
    public static TextAutoPairResult? HandleTextInput(
        string? text,
        int selectionStart,
        int selectionLength,
        string? input,
        TextAutoPairOptions? options = null)
    {
        var source = text ?? string.Empty;
        return HandleTextInput(source.Length, selectionStart, selectionLength, input,
            index => source[index], (start, length) => source.Substring(start, length), options);
    }

    public static TextAutoPairResult? HandleTextInput(
        int textLength,
        int selectionStart,
        int selectionLength,
        string? input,
        Func<int, char> charAt,
        Func<int, int, string> getText,
        TextAutoPairOptions? options = null)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        options ??= new TextAutoPairOptions();
        var start = Clamp(selectionStart, 0, textLength);
        var length = Clamp(selectionLength, 0, textLength - start);

        if (input.Length == 1 && IsEmphasisMarker(input[0]))
        {
            var marker = input[0];
            if (length > 0)
            {
                var selected = getText(start, length);
                var pair = new string(marker, 2);
                return new TextAutoPairResult(start, length, pair + selected + pair, start + pair.Length + selected.Length + pair.Length);
            }
            if (start > 0 && charAt(start - 1) == marker && (start < 2 || charAt(start - 2) != marker)
                && (start >= textLength || charAt(start) != marker))
            {
                return new TextAutoPairResult(start - 1, 1, new string(marker, 4), start + 1);
            }
            if (start < textLength && charAt(start) == marker)
            {
                return new TextAutoPairResult(start, 0, string.Empty, start + 1);
            }
            return null;
        }

        // 已有选区 + 可包裹符号 → 用左右半边包裹选区。
        if (length > 0
            && options.Pairs.TryGetValue(input, out var wrapping)
            && options.WrapSelectionKeys.Contains(input))
        {
            var selected = getText(start, length);
            return new TextAutoPairResult(start, length, input + selected + wrapping, start + input.Length + selected.Length + wrapping.Length);
        }

        // 输入右半边且右半边紧随其后 → 跳过（不重复插入）。
        if (length == 0
            && options.SkipExistingClosing
            && IsClosingKey(input, options)
            && start < textLength
            && charAt(start).ToString() == input)
        {
            return new TextAutoPairResult(start, 0, string.Empty, start + input.Length);
        }

        // 左半边插入配对，插入点落在中间。
        if (length == 0 && options.Pairs.TryGetValue(input, out var closing))
        {
            return new TextAutoPairResult(start, 0, input + closing, start + input.Length);
        }

        return null;
    }

    /// <summary>处理一次退格；命中空配对时返回删除两侧的结果，否则返回 null。</summary>
    public static TextAutoPairResult? HandleBackspace(
        string? text,
        int selectionStart,
        int selectionLength,
        TextAutoPairOptions? options = null)
    {
        var source = text ?? string.Empty;
        return HandleBackspace(source.Length, selectionStart, selectionLength,
            index => source[index], options);
    }

    public static TextAutoPairResult? HandleBackspace(
        int textLength,
        int selectionStart,
        int selectionLength,
        Func<int, char> charAt,
        TextAutoPairOptions? options = null)
    {
        options ??= new TextAutoPairOptions();
        if (!options.DeleteEmptyPairOnBackspace)
        {
            return null;
        }

        var start = Clamp(selectionStart, 0, textLength);
        var length = Clamp(selectionLength, 0, textLength - start);

        // 有选区时按普通删除处理，由宿主的撤销栈负责。
        if (length > 0 || start <= 0 || start >= textLength)
        {
            return null;
        }

        if (IsEmphasisMarker(charAt(start - 1)))
        {
            var marker = charAt(start - 1);
            return start >= 2 && start + 1 < textLength
                && charAt(start - 2) == marker && charAt(start) == marker && charAt(start + 1) == marker
                ? new TextAutoPairResult(start - 2, 4, string.Empty, start - 2)
                : null;
        }

        var left = charAt(start - 1).ToString();
        var right = charAt(start).ToString();
        return options.Pairs.TryGetValue(left, out var closing) && closing == right
            ? new TextAutoPairResult(start - 1, 2, string.Empty, start - 1)
            : null;
    }

    private static bool IsEmphasisMarker(char value) => value is '*' or '_' or '~';

    private static bool IsClosingKey(string input, TextAutoPairOptions options) =>
        options.Pairs.Values.Contains(input);

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
