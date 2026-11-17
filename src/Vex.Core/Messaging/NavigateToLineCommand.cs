using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class NavigateToLineCommand : Command
{
    private static int _lastSequence;

    public NavigateToLineCommand(int line)
    {
        Line = Math.Max(1, line);
        Sequence = Interlocked.Increment(ref _lastSequence);
    }

    public int Line { get; }

    /// <summary>
    /// 单调递增的跳转序号。重复点击同一章节时 <see cref="Line"/> 不变，
    /// 接收端按序号判断「这是一次新的跳转」，从而每次都重新定位。
    /// </summary>
    public int Sequence { get; }
}
