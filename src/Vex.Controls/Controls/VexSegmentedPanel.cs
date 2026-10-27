using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Vex.Controls;

/// <summary>
/// 原型 .vex-segmented（design/assets/prototype/segmented.css）的原生实现：轨道由宿主 Border.vex-segmented 画，
/// 本面板只负责横排、选项间距，以及那颗唯一会滑动伸缩的指示块 .vex-segmented-thumb。
/// 选中项不再自画底色——底色只由指示块表达，按钮本身永远透明，选中只换字色与字重（与原型一致）。
/// </summary>
public class VexSegmentedPanel : Panel
{
    /// <summary>宿主给每个分段按钮标的当前选中项；同一面板最多一个为真，全为假时指示块淡出。</summary>
    public static readonly AttachedProperty<bool> IsActiveProperty =
        AvaloniaProperty.RegisterAttached<VexSegmentedPanel, Control, bool>("IsActive");

    /// <summary>原型 .vex-segmented 的 gap（MCP 主标签一类的宿主给 4，其余 2）。</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<VexSegmentedPanel, double>(nameof(Spacing), 2);

    /// <summary>原型 button{flex:1}：各段等宽并长满轨道（MCP 页签、密度档位用）；默认按内容自然宽排。</summary>
    public static readonly StyledProperty<bool> FillItemsProperty =
        AvaloniaProperty.Register<VexSegmentedPanel, bool>(nameof(FillItems));

    private readonly Border _thumb;

    /// <summary>本面板按内容算出的自然宽度；可用宽度不足时按它等比让位，指示块才能一直贴着所选段。</summary>
    private double _contentWidth;

    /// <summary>等宽档（FillItems）下每段的统一宽度；安排时按它铺开，否则 DesiredSize 只有内容的自然宽。</summary>
    private double _uniformItemWidth;

    static VexSegmentedPanel()
    {
        SpacingProperty.Changed.AddClassHandler<VexSegmentedPanel>((panel, _) => panel.InvalidateMeasure());
        FillItemsProperty.Changed.AddClassHandler<VexSegmentedPanel>((panel, _) => panel.InvalidateMeasure());
        IsActiveProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is not bool active) return;
            // 选中态只有 IsActive 一个入口：原型 button[aria-pressed=true] 的字色字重由 .active 类承载，
            // 这里代宿主同步，免得宿主两头维护同一个状态；切换段不改按钮尺寸，直接按现成 Bounds 移动指示块。
            control.Classes.Set("active", active);
            if (control.Parent is VexSegmentedPanel panel) panel.MoveThumb();
        });
    }

    public VexSegmentedPanel()
    {
        _thumb = new Border
        {
            Classes = { "vex-segmented-thumb" },
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Opacity = 0,
        };
        // 指示块排第一，画在按钮之下，正好对应原型里 thumb 是轨道的第一个子元素。
        Children.Add(_thumb);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public bool FillItems
    {
        get => GetValue(FillItemsProperty);
        set => SetValue(FillItemsProperty, value);
    }

    public static bool GetIsActive(Control control) => control.GetValue(IsActiveProperty);

    public static void SetIsActive(Control control, bool value) => control.SetValue(IsActiveProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var spacing = Math.Max(0, Spacing);
        // 原型 flex 行是 align-items:stretch：轨道高度取最高的一段，每段都撑满这一行高。
        // 无约束高度在 Avalonia 里是 +∞（NaN 会被 Measure 直接拒掉）。
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : double.PositiveInfinity;
        var items = new List<Layoutable>();
        var width = 0d;
        var tallest = 0d;
        var widest = 0d;

        foreach (var child in Children)
        {
            if (child == _thumb || child is not Layoutable layoutable || !child.IsVisible) continue;
            layoutable.Measure(new Size(double.PositiveInfinity, height));
            items.Add(layoutable);
            width += layoutable.DesiredSize.Width + spacing;
            tallest = Math.Max(tallest, layoutable.DesiredSize.Height);
            widest = Math.Max(widest, layoutable.DesiredSize.Width);
        }

        if (items.Count > 0) width -= spacing;
        width = Math.Max(0, width);
        _contentWidth = width;

        // 原型 button{flex:1}（MCP 页签、密度档位）：等宽并长满轨道，所以整行按同一段宽重测一次。
        if (FillItems && items.Count > 0)
        {
            var room = double.IsPositiveInfinity(availableSize.Width) ? width : Math.Max(width, availableSize.Width);
            var uniform = Math.Max(widest, (room - spacing * (items.Count - 1)) / items.Count);
            foreach (var item in items) item.Measure(new Size(uniform, height));
            _uniformItemWidth = uniform;
            width = uniform * items.Count + spacing * (items.Count - 1);
            _contentWidth = width;
            tallest = items.Max(item => item.DesiredSize.Height);
        }
        else
        {
            _uniformItemWidth = 0;
        }

        // 指示块按整行尺寸测量：它自己的 Margin/Width 不参与面板大小，免得选中末段时轨道被撑宽。
        _thumb.Measure(new Size(width, tallest));
        return new Size(width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var spacing = Math.Max(0, Spacing);
        var visible = Children.Count(child => child != _thumb && child is Layoutable && child.IsVisible);
        // 原型轨道宽度由内容决定；被压缩时按等比让位，保证指示块始终贴着所选段。
        var scale = visible > 0 && finalSize.Width < _contentWidth && _contentWidth > 0
            ? Math.Max(0, (finalSize.Width - spacing * (visible - 1)) / _contentWidth)
            : 1d;
        // 等宽档还要吃掉父级多给的空间（flex 的 grow 是按剩余空间平分的）。
        var grow = FillItems && visible > 0 ? Math.Max(0, finalSize.Width - _contentWidth) / visible : 0d;
        var x = 0d;
        var rowHeight = finalSize.Height;

        foreach (var child in Children)
        {
            if (child == _thumb || child is not Layoutable layoutable) continue;
            if (!child.IsVisible)
            {
                layoutable.Arrange(default);
                continue;
            }
            var desired = FillItems && _uniformItemWidth > 0 ? _uniformItemWidth : layoutable.DesiredSize.Width;
            var width = desired * scale + grow;
            child.Arrange(new Rect(x, 0, width, rowHeight));
            x += width + spacing;
        }

        _thumb.Arrange(new Rect(0, 0, finalSize.Width, rowHeight));
        MoveThumb();
        return finalSize;
    }

    private void MoveThumb()
    {
        if (!IsArrangeValid) return;
        foreach (var child in Children)
        {
            if (child == _thumb || child is not Control control || !child.IsVisible) continue;
            if (!control.GetValue(IsActiveProperty)) continue;
            var bounds = control.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            _thumb.Width = bounds.Width;
            _thumb.Margin = new Thickness(bounds.X, 0, 0, 0);
            _thumb.Opacity = 1;
            return;
        }
        _thumb.Opacity = 0;
    }
}
