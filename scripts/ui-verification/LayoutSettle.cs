using System.Diagnostics;
using Avalonia.Threading;
using Vex.Core.Motion;

internal static class LayoutSettle
{
    /// <summary>
    /// 侧栏列宽是逐帧插值（DispatcherTimer），无头测试必须让真实时间走完动画再量几何，
    /// 否则读到的是动画起点而不是终态。
    /// </summary>
    public static void Run()
    {
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
        while (WidthAnimator.RunningCount > 0 && Stopwatch.GetTimestamp() < deadline)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
        Dispatcher.UIThread.RunJobs();
    }
}
