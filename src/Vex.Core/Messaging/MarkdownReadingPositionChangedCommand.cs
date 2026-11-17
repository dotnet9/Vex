using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

/// <summary>只同步当前阅读章节，不移动编辑光标或触发反向导航。</summary>
public sealed class MarkdownReadingPositionChangedCommand(int line) : Command
{
    public int Line { get; } = Math.Max(1, line);
}
