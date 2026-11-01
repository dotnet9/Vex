using Avalonia;
using Avalonia.Controls;

namespace Vex.Modules.Shell.Views;

/// <summary>双列命令菜单；工具条的分隔符仍归 Ursa 管理，不占菜单单元格。</summary>
public sealed class FormattingOverflowPanel : Panel
{
    private const double Gap = 4;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(0, (availableSize.Width - Gap) / 2);
        foreach (var child in Children)
            child.Measure(child is Button ? new Size(width, double.PositiveInfinity) : default);
        var buttons = Children.OfType<Button>().Where(b => b.IsVisible).ToArray();
        var height = 0d;
        for (var i = 0; i < buttons.Length; i += 2)
            height += Math.Max(buttons[i].DesiredSize.Height, i + 1 < buttons.Length ? buttons[i + 1].DesiredSize.Height : 0) + Gap;
        return new Size(availableSize.Width, Math.Max(0, height - Gap));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children.Where(c => c is not Button)) child.Arrange(default);
        var buttons = Children.OfType<Button>().Where(b => b.IsVisible).ToArray();
        var width = Math.Max(0, (finalSize.Width - Gap) / 2);
        var top = 0d;
        for (var i = 0; i < buttons.Length; i += 2)
        {
            var height = Math.Max(buttons[i].DesiredSize.Height, i + 1 < buttons.Length ? buttons[i + 1].DesiredSize.Height : 0);
            buttons[i].Arrange(new Rect(0, top, width, height));
            if (i + 1 < buttons.Length) buttons[i + 1].Arrange(new Rect(width + Gap, top, width, height));
            top += height + Gap;
        }
        return finalSize;
    }
}
