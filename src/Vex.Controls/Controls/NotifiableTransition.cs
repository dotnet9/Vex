using Avalonia;
using Avalonia.Animation;

namespace Vex.Controls.Controls;

/// <summary>
/// 可等待的动效：把一段 Avalonia <see cref="Animation"/> 跑完并给出完成通知，
/// 带超时兜底（动画被取消、宿主切换时仍会放行等待方）。
/// <para>
/// 说明：Avalonia 12 的 <c>Transition</c> 已无公开的「开始/结束」可覆写点
/// （<c>DoTransition</c>/<c>Apply</c> 为 internal），因此这里基于 <see cref="Animation"/>
/// 提供同等的串行动效编排能力，用于「展开 → 再执行下一步」这类流程。
/// </para>
/// </summary>
public static class NotifiableTransition
{
    /// <summary>默认兜底超时（动画时长之外额外给一点余量）。</summary>
    private static readonly TimeSpan FallbackExtra = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// 运行动画并等待其结束；超出 <paramref name="animation"/> 的 Duration + 兜底余量后
    /// 即使动画未回调也会返回，保证调用方不被卡住。
    /// </summary>
    public static async Task RunAsync(
        Animatable target,
        Animation animation,
        CancellationToken cancellationToken = default)
    {
        var run = animation.RunAsync(target, cancellationToken);
        var timeout = animation.Duration + FallbackExtra;
        var completed = await Task.WhenAny(run, Task.Delay(timeout, cancellationToken)).ConfigureAwait(true);
        if (!ReferenceEquals(completed, run))
        {
            return;
        }

        await run.ConfigureAwait(true);
    }
}
