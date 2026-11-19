using System.Collections.Generic;
using System.Linq;

// 与 Controls/CodeWF.Avalonia.Controls/Text/TextAutoPair.cs 互为副本：
// Markdown 包不依赖 Controls 主工程，修改任一侧须评估同步另一侧。
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
            ["*"] = "*",
            ["_"] = "_",
            ["`"] = "`",
            ["~"] = "~",
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
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        options ??= new TextAutoPairOptions();
        var source = text ?? string.Empty;
        var start = Clamp(selectionStart, 0, source.Length);
        var length = Clamp(selectionLength, 0, source.Length - start);

        // 已有选区 + 可包裹符号 → 用左右半边包裹选区。
        if (length > 0
            && options.Pairs.TryGetValue(input, out var wrapping)
            && options.WrapSelectionKeys.Contains(input))
        {
            var selected = source.Substring(start, length);
            return new TextAutoPairResult(start, length, input + selected + wrapping, start + input.Length + selected.Length + wrapping.Length);
        }

        // 输入右半边且右半边紧随其后 → 跳过（不重复插入）。
        if (length == 0
            && options.SkipExistingClosing
            && IsClosingKey(input, options)
            && start < source.Length
            && source[start].ToString() == input)
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
        options ??= new TextAutoPairOptions();
        if (!options.DeleteEmptyPairOnBackspace)
        {
            return null;
        }

        var source = text ?? string.Empty;
        var start = Clamp(selectionStart, 0, source.Length);
        var length = Clamp(selectionLength, 0, source.Length - start);

        // 有选区时按普通删除处理，由宿主的撤销栈负责。
        if (length > 0 || start <= 0 || start >= source.Length)
        {
            return null;
        }

        var left = source[start - 1].ToString();
        var right = source[start].ToString();
        return options.Pairs.TryGetValue(left, out var closing) && closing == right
            ? new TextAutoPairResult(start - 1, 2, string.Empty, start - 1)
            : null;
    }

    private static bool IsClosingKey(string input, TextAutoPairOptions options) =>
        options.Pairs.Values.Contains(input);

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
