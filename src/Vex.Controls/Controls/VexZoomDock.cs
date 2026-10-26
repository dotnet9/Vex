using System;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Vex.Controls;

/// <summary>
/// 原型 .vex-zoomdock 的原生实现：坞里只有拖动条与还原钮，百分比不再常驻，
/// 手按在拖动条上（拖动或方向键调值）时才浮在拖动块正上方，松手即隐（对应 design/assets/prototype/zoomdock.js）。
/// 几何全部取自「界面密度」派生令牌，宿主只决定量程、读数换算与还原动作。
/// </summary>
public class VexZoomDock : TemplatedControl
{
    /// <summary>原型 zoomdock.js 里 Chrome range 的拖动块宽，量不到模板 Thumb 时按它推算块心。</summary>
    private const double FallbackThumbWidth = 15;

    /// <summary>松手后气泡收起的延时，对应原型 dismiss(120)。</summary>
    private static readonly TimeSpan DismissDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>方向键调值后的气泡滞留时长，对应原型 LINGER 600。</summary>
    private static readonly TimeSpan Linger = TimeSpan.FromMilliseconds(600);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<VexZoomDock, double>(nameof(Minimum), 0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<VexZoomDock, double>(nameof(Maximum), 100);

    /// <summary>刻度间隔：原型 range 的 step，键盘步长与鼠标吸附共用它。</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<VexZoomDock, double>(nameof(Step), 1);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VexZoomDock, double>(nameof(Value), 100, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>读数换算：1 表示值本身就是百分比（正文 50–200、导图 1–200），100 表示值是倍率（演示 1–2.5）。</summary>
    public static readonly StyledProperty<double> PercentMultiplierProperty =
        AvaloniaProperty.Register<VexZoomDock, double>(nameof(PercentMultiplier), 1d);

    public static readonly StyledProperty<ICommand?> ResetCommandProperty =
        AvaloniaProperty.Register<VexZoomDock, ICommand?>(nameof(ResetCommand));

    /// <summary>拖动条的可读名称（原型 range 的 aria-label），同时作为悬停提示。</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VexZoomDock, string?>(nameof(Label));

    /// <summary>还原钮的可读名称（原型 data-l10n-tip）。</summary>
    public static readonly StyledProperty<string?> ResetLabelProperty =
        AvaloniaProperty.Register<VexZoomDock, string?>(nameof(ResetLabel));

    /// <summary>还原钮图形（宿主传入应用里的 fit 图标几何，原型三处坞共用同一枚）。</summary>
    public static readonly StyledProperty<object?> ResetGeometryProperty =
        AvaloniaProperty.Register<VexZoomDock, object?>(nameof(ResetGeometry));

    private Slider? _slider;
    private Canvas? _bubbleTrack;
    private Border? _bubble;
    private TextBlock? _bubbleText;
    private Thumb? _thumb;
    private TranslateTransform? _bubbleShift;
    private ScaleTransform? _bubbleScale;
    private Timer? _dismissTimer;
    private bool _isPointerDown;
    private bool _isLingering;

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double PercentMultiplier { get => GetValue(PercentMultiplierProperty); set => SetValue(PercentMultiplierProperty, value); }
    public ICommand? ResetCommand { get => GetValue(ResetCommandProperty); set => SetValue(ResetCommandProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? ResetLabel { get => GetValue(ResetLabelProperty); set => SetValue(ResetLabelProperty, value); }
    public object? ResetGeometry { get => GetValue(ResetGeometryProperty); set => SetValue(ResetGeometryProperty, value); }

    /// <summary>还原钮被按下（宿主没有命令可绑时使用，例如代码后置的导图视图）。</summary>
    public event EventHandler? ResetRequested;

    /// <summary>当前读数文案，供宿主与验证器断言。</summary>
    public string PercentText => $"{Math.Round(Value * PercentMultiplier)}%";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _slider = e.NameScope.Find<Slider>("PART_Slider");
        _bubbleTrack = e.NameScope.Find<Canvas>("PART_BubbleTrack");
        _bubble = e.NameScope.Find<Border>("PART_Bubble");
        _bubbleText = e.NameScope.Find<TextBlock>("PART_BubbleText");
        if (_slider is null || _bubble is null || _bubbleTrack is null) return;
        // 原型 .zoom-value 常态 translateX(-50%) scale(.9)，点亮时 scale(1)；缩放绕气泡自身中心，位移不受原点影响。
        _bubble.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _bubbleShift = new TranslateTransform();
        _bubbleScale = new ScaleTransform(.9, .9);
        _bubble.RenderTransform = new TransformGroup { Children = { _bubbleShift, _bubbleScale } };
        // 拖动条在隐藏时也可能还没量到块，显露后在 OnAttachedToVisualTree 里补找。
        HookThumb();
        _slider.SizeChanged += (_, _) => Place();
        _bubbleTrack.SizeChanged += (_, _) => Place();
        _bubble.SizeChanged += (_, _) => Place();
        _slider.AddHandler(PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, _) => OnPressed()),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _slider.AddHandler(PointerReleasedEvent, new EventHandler<PointerReleasedEventArgs>((_, _) => OnReleased()),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _slider.AddHandler(PointerCaptureLostEvent, new EventHandler<PointerCaptureLostEventArgs>((_, _) => OnReleased()),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _slider.AddHandler(KeyDownEvent, new EventHandler<KeyEventArgs>(OnSliderKeyDown), RoutingStrategies.Tunnel);
        _slider.AddHandler(LostFocusEvent, new EventHandler<RoutedEventArgs>((_, _) =>
        {
            _isPointerDown = false;
            Show(TimeSpan.Zero);
        }), RoutingStrategies.Tunnel);
        if (e.NameScope.Find<Button>("PART_Reset") is { } reset) reset.Click += (_, _) => RaiseReset();
        Place();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 坞可能在隐藏时才建的，那时模板里的拖动块还没进可视树、量不到位置，显露后补找一次再定位。
        HookThumb();
        Place();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dismissTimer?.Dispose();
        _dismissTimer = null;
    }

    private void HookThumb()
    {
        if (_slider is null || _thumb is not null) return;
        _thumb = FindThumb(_slider);
        if (_thumb is not null) _thumb.SizeChanged += (_, _) => Place();
    }

    private static Thumb? FindThumb(Visual root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is Thumb thumb) return thumb;
            var nested = FindThumb(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private void OnPressed()
    {
        _isPointerDown = true;
        Show(null);
    }

    private void OnReleased()
    {
        _isPointerDown = false;
        Show(DismissDelay);
    }

    private void OnSliderKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End) Show(Linger);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            Place();
            // 拖动中的连续变化由按下状态维持显现；聚焦时的变化视同方向键调值，重新计一次滞留。
            if (_isPointerDown) Show(null);
            else if (IsKeyboardFocusWithin) Show(Linger);
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty
                 || change.Property == StepProperty || change.Property == PercentMultiplierProperty)
        {
            Place();
        }
    }

    /// <param name="after">null 表示常驻（拖动中），给了 TimeSpan 就是到点再收（键盘调值、松手、失焦）。</param>
    private void Show(TimeSpan? after)
    {
        _dismissTimer?.Dispose();
        _dismissTimer = null;
        _isLingering = after is not null;
        var visible = after != TimeSpan.Zero;
        if (_bubble is not null) _bubble.Opacity = visible ? 1 : 0;
        if (_bubbleScale is { } scale) (scale.ScaleX, scale.ScaleY) = (visible ? 1 : .9, visible ? 1 : .9);
        if (after is { } delay && delay > TimeSpan.Zero)
        {
            _dismissTimer = new Timer(_ => Dispatcher.UIThread.Post(() =>
            {
                if (!_isLingering || _bubble is null) return;
                _bubble.Opacity = 0;
                if (_bubbleScale is { } scale) (scale.ScaleX, scale.ScaleY) = (.9, .9);
            }), null, delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>气泡贴着拖动块中心、浮在拖动条上方 6px：原型按 THUMB_W 与 100%+6 换算，原生优先取模板里那颗 Thumb 的实测位置。</summary>
    private void Place()
    {
        if (_slider is null || _bubble is null || _bubbleText is null || _bubbleTrack is null) return;
        _bubbleText.Text = PercentText;
        // 原型 .zoom-value 的 bottom:calc(100% + 6px) 以定位父级整高计，拖动条在父级里竖向居中，故取 (父高 + 条高) / 2。
        Canvas.SetBottom(_bubble, (_bubbleTrack.Bounds.Height + _slider.Bounds.Height) / 2 + 6);
        // 块心要在气泡的坐标平面里量：Thumb 隔着 Slider 与 Track 两层，直接拿 Bounds.CenterX 会漏掉父级内缩。
        var center = _thumb?.TranslatePoint(new Point(_thumb.Bounds.Width / 2, _thumb.Bounds.Height / 2), _bubbleTrack)?.X
                     ?? EstimatedThumbCenter();
        Canvas.SetLeft(_bubble, center);
        if (_bubbleShift is not null) _bubbleShift.X = -_bubble.Bounds.Width / 2;
    }

    private double EstimatedThumbCenter()
    {
        var width = _slider?.Bounds.Width ?? 0;
        if (width <= 0) return 0;
        var span = Maximum - Minimum;
        var ratio = span <= 0 ? 0 : Math.Clamp((Value - Minimum) / span, 0, 1);
        return FallbackThumbWidth / 2 + ratio * Math.Max(0, width - FallbackThumbWidth);
    }

    internal void RaiseReset()
    {
        ResetRequested?.Invoke(this, EventArgs.Empty);
        if (ResetCommand?.CanExecute(null) == true) ResetCommand.Execute(null);
    }
}
