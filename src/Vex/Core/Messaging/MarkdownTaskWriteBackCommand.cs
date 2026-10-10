namespace Vex.Core.Messaging;

/// <summary>
/// 预览里勾选任务列表项后回写 Markdown：携带新文本与被改写的源码区间，
/// 编辑器按区间精确替换（长度一致时保持光标位置），保持撤销栈单步。
/// </summary>
public sealed class MarkdownTaskWriteBackCommand : CodeWF.Toolkit.EventBus.Command
{
    public MarkdownTaskWriteBackCommand(string markdown, int start, int length)
    {
        Markdown = markdown;
        Start = start;
        Length = length;
    }

    public string Markdown { get; }

    /// <summary>被改写区间的起始偏移（通常为勾选标记 "[" 的位置）。</summary>
    public int Start { get; }

    /// <summary>被改写区间的长度（通常为 3，即 "[x]"）。</summary>
    public int Length { get; }
}
