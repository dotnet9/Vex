using Avalonia;
using Avalonia.Controls;

namespace Vex.Core.Motion;

/// <summary>
/// 全局动效开关：关闭时把三档动效时长资源归零，已有 Transitions/Animations
/// 因时长为零立即到达终态，等效于全局无动效（不改变布局与业务语义）。
/// </summary>
public static class MotionResources
{
    public const string FastKey = "MotionDurationFast";
    public const string MidKey = "MotionDurationMid";
    public const string SlowKey = "MotionDurationSlow";

    private static readonly (string Key, TimeSpan Duration)[] Tokens =
    [
        (FastKey, TimeSpan.FromMilliseconds(100)),
        (MidKey, TimeSpan.FromMilliseconds(200)),
        (SlowKey, TimeSpan.FromMilliseconds(300))
    ];

    /// <summary>当前是否启用动效。</summary>
    public static bool IsEnabled { get; private set; } = true;

    /// <summary>按开关状态重写应用级动效 token。</summary>
    public static void Apply(bool enabled)
    {
        IsEnabled = enabled;
        if (Application.Current is { } app)
        {
            Apply(app.Resources, enabled);
        }
    }

    /// <summary>按开关状态重写指定资源字典中的动效 token（便于测试与局部覆盖）。</summary>
    public static void Apply(IResourceDictionary resources, bool enabled)
    {
        foreach (var (key, duration) in Tokens)
        {
            resources[key] = enabled ? duration : TimeSpan.Zero;
        }
    }
}
