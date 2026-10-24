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
    public const string EnabledKey = "VexMotionEnabled";

    /// <summary>中档动效时长：资源 token 与无法使用 Transitions 的逐帧动画（如列宽）共用同一档位。</summary>
    public static readonly TimeSpan Mid = TimeSpan.FromMilliseconds(220);

    private static readonly (string Key, TimeSpan Duration)[] Tokens =
    [
        (FastKey, TimeSpan.FromMilliseconds(140)),
        (MidKey, Mid),
        (SlowKey, TimeSpan.FromMilliseconds(260))
    ];

    /// <summary>当前是否启用动效。</summary>
    public static bool IsEnabled { get; private set; } = true;
    public static event Action? Changed;

    /// <summary>按开关状态重写应用级动效 token。</summary>
    public static void Apply(bool enabled)
    {
        IsEnabled = enabled;
        if (Application.Current is { } app)
        {
            Apply(app.Resources, enabled);
        }
        Changed?.Invoke();
    }

    /// <summary>按开关状态重写指定资源字典中的动效 token（便于测试与局部覆盖）。</summary>
    public static void Apply(IResourceDictionary resources, bool enabled)
    {
        resources[EnabledKey] = enabled;
        foreach (var (key, duration) in Tokens)
        {
            resources[key] = enabled ? duration : TimeSpan.Zero;
        }
    }
}
