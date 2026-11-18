using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 代码高亮能力委托：输入代码与排版参数，返回高亮内容控件；
/// 返回 null 表示无法高亮（调用方应使用 Core 的单色等宽降级渲染）。
/// <paramref name="session"/> 为宿主提供的行级缓存会话（可 null）：流式增长场景
/// 下未变行复用缓存 token，只重算新增/变更行。
/// </summary>
public delegate Control? MarkdownCodeHighlighter(
    string code,
    string language,
    bool isDark,
    FontFamily fontFamily,
    double fontSize,
    double lineHeight,
    MarkdownCodeHighlightSession? session,
    Func<bool>? hasGlobalSelection,
    Func<Task>? copySelectionAsync);

/// <summary>
/// 长代码分块参数：超过 <see cref="ThresholdLines"/> 的代码按 <see cref="LinesPerChunk"/>
/// 行切块渲染，避免单个文本排版随行数线性变慢。
/// </summary>
public static class MarkdownCodeChunking
{
    /// <summary>行数超过该值才分块（短代码分块的固定开销大于收益）。</summary>
    public const int ThresholdLines = 200;

    /// <summary>每块的行数。</summary>
    public const int LinesPerChunk = 128;
}

/// <summary>
/// 可选能力：代码正文支持原地增长。流式追加时外框先尝试 <see cref="TryUpdate"/>，
/// 成功则复用已有分块控件（只重建尾部变化的块）；返回 false 表示调用方应整体重建正文。
/// </summary>
public interface IMarkdownCodeBodyUpdate
{
    bool TryUpdate(string code);
}
