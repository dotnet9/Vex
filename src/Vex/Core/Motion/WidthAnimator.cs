using System.Diagnostics;
using Avalonia.Threading;

namespace Vex.Core.Motion;

/// <summary>
/// 列宽逐帧插值：Avalonia 的 ColumnDefinition 不参与 Transitions，收起/展开只能按帧回调驱动绑定值，
/// 效果对应原型的 width 过渡。关闭动效或首尾同宽时直接停在终值，不产生中间态。
/// </summary>
public sealed class WidthAnimator
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);
    private readonly Action _onFrame;
    private readonly DispatcherTimer _timer = new() { Interval = Frame };
    private readonly Stopwatch _elapsed = new();
    private double _from, _to;
    private TimeSpan _duration;
    private bool _running;

    public WidthAnimator(Action onFrame)
    {
        _onFrame = onFrame;
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>动画进行中的插值宽度；空闲时为 null，绑定值回到用户设定宽度。</summary>
    public double? Value { get; private set; }

    /// <summary>正在播放的插值器数量：无头 UI 测试用它等待布局稳定后再量几何。</summary>
    public static int RunningCount { get; private set; }

    public void Animate(double from, double to, TimeSpan duration)
    {
        if (!MotionResources.IsEnabled || Math.Abs(from - to) < .5)
        {
            Stop();
            return;
        }
        _from = from;
        _to = to;
        _duration = duration;
        Value = from;
        _elapsed.Restart();
        if (!_running)
        {
            _running = true;
            RunningCount++;
            _timer.Start();
        }
        _onFrame();
    }

    private void Tick()
    {
        var progress = Math.Clamp(_elapsed.ElapsedMilliseconds / _duration.TotalMilliseconds, 0, 1);
        // ease-out：起步快、收尾稳，对应原型 .24s ease 的观感。
        Value = _from + (_to - _from) * (1 - Math.Pow(1 - progress, 3));
        if (progress >= 1) Stop();
        _onFrame();
    }

    private void Stop()
    {
        if (_running)
        {
            _running = false;
            RunningCount--;
        }
        _timer.Stop();
        _elapsed.Reset();
        Value = null;
    }
}
