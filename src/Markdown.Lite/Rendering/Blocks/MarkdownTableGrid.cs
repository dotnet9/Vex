using Avalonia;
using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 根据单元格的自然宽度布局表格。空间不足时优先保留短列，长列压缩后自动换行。
/// </summary>
internal sealed class MarkdownTableGrid : Grid
{
    private bool _collapsedBorders;
    protected override Type StyleKeyOverride => typeof(Grid);

    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = new double[ColumnDefinitions.Count];
        var collapseBorders = MarkdownTableLayout.GetCollapseBorders(this);
        foreach (var child in Children)
        {
            // 合并的是实际描边宽度，与字号、内容和行列数量无关。
            if ((collapseBorders || _collapsedBorders) && child is Border cell)
                cell.Margin = collapseBorders ? new Thickness(
                    GetColumn(cell) > 0 ? -cell.BorderThickness.Left : 0,
                    GetRow(cell) > 0 ? -cell.BorderThickness.Top : 0, 0, 0) : default;
            child.Measure(Size.Infinity);
            var column = GetColumn(child);
            var span = Math.Min(GetColumnSpan(child), widths.Length - column);
            if (span <= 0) continue;
            var width = child.DesiredSize.Width / span;
            for (var index = column; index < column + span; index++)
                widths[index] = Math.Max(widths[index], width);
        }
        _collapsedBorders = collapseBorders;

        var naturalWidth = widths.Sum();
        // 原型语义：表格恒铺满渲染区（table-layout:auto; width:100%），列宽按内容自然宽度比例分配。
        if (MarkdownTableLayout.GetFillAvailableWidth(this) && naturalWidth > 0 &&
            double.IsFinite(availableSize.Width) && naturalWidth < availableSize.Width)
        {
            for (var index = 0; index < widths.Length; index++) widths[index] *= availableSize.Width / naturalWidth;
        }
        if (double.IsFinite(availableSize.Width) && naturalWidth > availableSize.Width && widths.Length > 0)
        {
            // 极窄视口允许缩小保底宽度，避免列数多时溢出整个文档。
            var minimum = Math.Min(56, availableSize.Width / widths.Length);
            var minimumTotal = widths.Sum(width => Math.Min(width, minimum));
            var ratio = Math.Clamp((availableSize.Width - minimumTotal) / (naturalWidth - minimumTotal), 0, 1);
            for (var index = 0; index < widths.Length; index++)
            {
                var floor = Math.Min(widths[index], minimum);
                widths[index] = floor + (widths[index] - floor) * ratio;
            }
        }

        for (var index = 0; index < widths.Length; index++)
            ColumnDefinitions[index].Width = new GridLength(widths[index]);

        return base.MeasureOverride(availableSize);
    }
}
