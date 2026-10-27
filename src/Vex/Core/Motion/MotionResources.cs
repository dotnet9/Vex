using Avalonia;
using Avalonia.Controls;

namespace Vex.Core.Motion;

/// <summary>
/// 界面动效三档（原型 data-motion，design/assets/prototype/prototype.css:7-8）：
/// 完整 ×1、精简 ×0.35、关闭 ×0。档位键同时是设置里的持久化值。
/// </summary>
public static class MotionModes
{
    public const string Full = "full", Reduced = "reduced", Off = "off";

    /// <summary>原型 --vex-motion 乘算系数；未知值按完整档处理。</summary>
    public static double Scale(string? mode) => mode switch
    {
        Reduced => .35,
        Off => 0,
        _ => 1,
    };
}

/// <summary>
/// 全局动效开关：把三档动效时长按档位系数乘算后写进应用级资源，已有 Transitions/Animations
/// 因时长缩短或归零而提前到达终态，等效于降级/无动效（不改变布局与业务语义）。
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

    /// <summary>当前是否启用动效（关闭档为假）。</summary>
    public static bool IsEnabled { get; private set; } = true;

    /// <summary>当前档位系数：逐帧动画按它决定走时长还是直接落位。</summary>
    public static double Scale { get; private set; } = 1;

    public static event Action? Changed;

    /// <summary>按档位重写应用级动效 token。</summary>
    public static void Apply(string mode) => Apply(MotionModes.Scale(mode));

    /// <summary>按系数重写应用级动效 token。</summary>
    public static void Apply(double scale)
    {
        Scale = scale;
        IsEnabled = scale > 0;
        if (Application.Current is { } app)
        {
            Apply(app.Resources, scale);
        }
        Changed?.Invoke();
    }

    /// <summary>按系数重写指定资源字典中的动效 token（便于测试与局部覆盖）。</summary>
    public static void Apply(IResourceDictionary resources, double scale)
    {
        resources[EnabledKey] = scale > 0;
        foreach (var (key, duration) in Tokens)
        {
            resources[key] = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * scale);
        }
    }
}
