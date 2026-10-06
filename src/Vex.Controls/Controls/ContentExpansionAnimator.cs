using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Vex.Controls.Controls;

/// <summary>
/// 内容展开动画：单一 progress（0 → 1）同时驱动布局尺寸与透明度，
/// 用于侧栏开合、大纲树展开等「占位宽度/高度变化」场景，替代 GridLength 硬切。
/// <para>
/// 关闭动效（<see cref="Duration"/> 为零或 <see cref="Animate"/> 为 false）时立即到达终态，
/// 不影响布局与业务语义。
/// </para>
/// </summary>
public sealed class ContentExpansionAnimator : Border
{
    private static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, double>(nameof(Progress));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, bool>(nameof(IsExpanded), true);

    /// <summary>展开后的尺寸（水平方向为宽度，垂直方向为高度）。</summary>
    public static readonly StyledProperty<double> ExpandedSizeProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, double>(nameof(ExpandedSize), 320d);

    /// <summary>折叠后的尺寸（通常为 0，图标态侧栏可为 48）。</summary>
    public static readonly StyledProperty<double> CollapsedSizeProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, double>(nameof(CollapsedSize));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>动效时长；为零表示不做动画（全局关闭动效时由资源注入 0）。</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(200));

    public static readonly StyledProperty<bool> AnimateProperty =
        AvaloniaProperty.Register<ContentExpansionAnimator, bool>(nameof(Animate), true);

    public ContentExpansionAnimator()
    {
        ClipToBounds = true;
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public double ExpandedSize
    {
        get => GetValue(ExpandedSizeProperty);
        set => SetValue(ExpandedSizeProperty, value);
    }

    public double CollapsedSize
    {
        get => GetValue(CollapsedSizeProperty);
        set => SetValue(CollapsedSizeProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public bool Animate
    {
        get => GetValue(AnimateProperty);
        set => SetValue(AnimateProperty, value);
    }

    /// <summary>当前进度（0 = 折叠，1 = 展开），供测试与串联动效读取。</summary>
    public double Progress => GetValue(ProgressProperty);

    /// <summary>当前占位尺寸（由进度插值得到）。</summary>
    public double CurrentSize => Lerp(CollapsedSize, ExpandedSize, Math.Clamp(Progress, 0d, 1d));

    static ContentExpansionAnimator()
    {
        AffectsMeasure<ContentExpansionAnimator>(
            ProgressProperty,
            ExpandedSizeProperty,
            CollapsedSizeProperty,
            OrientationProperty);
        IsExpandedProperty.Changed.AddClassHandler<ContentExpansionAnimator>((animator, _) => animator.OnIsExpandedChanged());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = CurrentSize;
        if (Child is { } child)
        {
            var constraint = Orientation == Orientation.Horizontal
                ? new Size(size, availableSize.Height)
                : new Size(availableSize.Width, size);
            child.Measure(constraint);
        }

        return Orientation == Orientation.Horizontal
            ? new Size(size, double.IsFinite(availableSize.Height) ? availableSize.Height : 0)
            : new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = CurrentSize;
        if (Child is { } child)
        {
            var rect = Orientation == Orientation.Horizontal
                ? new Rect(0, 0, size, finalSize.Height)
                : new Rect(0, 0, finalSize.Width, size);
            child.Arrange(rect);
        }

        Opacity = Math.Clamp(Progress, 0d, 1d);
        return Orientation == Orientation.Horizontal
            ? new Size(size, finalSize.Height)
            : new Size(finalSize.Width, size);
    }

    private void OnIsExpandedChanged() => Run(IsExpanded ? 1d : 0d);

    private void Run(double target)
    {
        var from = Math.Clamp(Progress, 0d, 1d);
        if (!Animate || Duration <= TimeSpan.Zero || Math.Abs(from - target) < 0.001d)
        {
            SetCurrentValue(ProgressProperty, target);
            InvalidateMeasure();
            return;
        }

        var animation = new Animation
        {
            Duration = Duration,
            Easing = new CubicEaseInOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(ProgressProperty, target) } }
            }
        };

        _ = NotifiableTransition.RunAsync(this, animation)
            .ContinueWith(_ => InvalidateMeasure(), TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;
}
