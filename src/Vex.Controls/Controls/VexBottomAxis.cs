using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Vex.Controls;

/// <summary>
/// 原型「底部悬浮件同轴」规则（design/assets/workbench/workspace-chrome.css 的 --vex-axis）：
/// 左右折叠钮、居中视图胶囊、右下角缩放坞的中心线必须落在同一条水平线上，基准是左折叠钮的圆心。
/// CSS 用 bottom + translateY(50%) 表达， Avalonia 没有 calc，这里改成把「中心距容器底」换算成外边距：
/// margin-bottom = 轴线 - 自身高 / 2，自身高度一变（切密度、换语言、字号变化）就重算，所以三档密度都不会失配。
/// </summary>
public static class VexBottomAxis
{
    /// <summary>原型 --vex-axis：轴线到 .workspace 底边的距离（折叠钮底距 18px + 半径 12px）。</summary>
    public const double Workspace = 30;

    /// <summary>原型 --block-edge：正文列相对 .workspace 底边抬起的量。</summary>
    public const double BlockEdge = 10;

    /// <summary>原型块面 1px 描边：窗格内部再抬起的量。</summary>
    public const double BlockBorder = 1;

    /// <summary>原型 --vex-axis-col：悬浮在正文列（.workspace-main）里的件用的轴线。</summary>
    public const double Column = Workspace - BlockEdge;

    /// <summary>原型 --vex-axis-view：悬浮在窗格（.reading-pane / .map-viewport）里的件用的轴线。</summary>
    public const double View = Column - BlockBorder;

    public static readonly AttachedProperty<double> CenterFromBottomProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("CenterFromBottom", typeof(VexBottomAxis), double.NaN);

    private static readonly AttachedProperty<object?> SubscriptionProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Subscription", typeof(VexBottomAxis));

    static VexBottomAxis()
    {
        CenterFromBottomProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            // 重复赋值先退订再订，避免换轴线（例如从窗格级切到列级）后残留旧监听。
            if (control.GetValue(SubscriptionProperty) is EventHandler<AvaloniaPropertyChangedEventArgs> previous)
                control.PropertyChanged -= previous;

            if (args.NewValue is not double axis || double.IsNaN(axis))
            {
                control.SetValue(SubscriptionProperty, null);
                return;
            }

            void OnChanged(object? _, AvaloniaPropertyChangedEventArgs e)
            {
                // 只有自身高度或外边距变了才需要重算底距；其余属性变化直接跳过。
                if (e.Property != Visual.BoundsProperty && e.Property != Layoutable.MarginProperty) return;
                Apply(control, axis);
            }
            control.PropertyChanged += OnChanged;
            control.SetValue(SubscriptionProperty, (EventHandler<AvaloniaPropertyChangedEventArgs>)OnChanged);
            Apply(control, axis);
        });
    }

    public static double GetCenterFromBottom(Control control) => control.GetValue(CenterFromBottomProperty);

    public static void SetCenterFromBottom(Control control, double value) => control.SetValue(CenterFromBottomProperty, value);

    private static void Apply(Control control, double axis)
    {
        var current = control.Bounds;
        if (current.Height <= 0) return;
        var margin = control.Margin;
        var bottom = axis - current.Height / 2;
        if (Math.Abs(margin.Bottom - bottom) < 0.01) return;
        control.Margin = new Thickness(margin.Left, margin.Top, margin.Right, bottom);
    }
}
