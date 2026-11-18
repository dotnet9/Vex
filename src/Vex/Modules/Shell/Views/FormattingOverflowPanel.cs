using Avalonia;
using Avalonia.Controls;

namespace Vex.Modules.Shell.Views;

public sealed class FormattingToolbarPanel : Ursa.Controls.ToolBarPanel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (TemplatedParent is not Ursa.Controls.ToolBar toolbar || !toolbar.Classes.Contains(":overflow")
            || toolbar.Classes.Contains("measure-compact"))
            return base.MeasureOverride(availableSize);

        // Ursa also measures the parked items here. Use their row styles for the fit check,
        // not the wider, localized menu labels. The popup retains its own measurement pass.
        toolbar.Classes.Add("measure-compact");
        try { return base.MeasureOverride(availableSize); }
        finally { toolbar.Classes.Remove("measure-compact"); }
    }
}

/// <summary>双列命令菜单；按钮和段落下拉框占格，分隔符不占格。</summary>
public sealed class FormattingOverflowPanel : Panel
{
    private const double Gap = 4;
    private static bool IsCommand(Control child) => child is Button or ComboBox;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(0, (availableSize.Width - Gap) / 2);
        foreach (var child in Children)
            child.Measure(IsCommand(child) ? new Size(width, double.PositiveInfinity) : default);
        var buttons = Children.Where(c => IsCommand(c) && c.IsVisible).ToArray();
        var height = 0d;
        for (var i = 0; i < buttons.Length; i += 2)
            height += Math.Max(buttons[i].DesiredSize.Height, i + 1 < buttons.Length ? buttons[i + 1].DesiredSize.Height : 0) + Gap;
        return new Size(availableSize.Width, Math.Max(0, height - Gap));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children.Where(c => !IsCommand(c))) child.Arrange(default);
        var buttons = Children.Where(c => IsCommand(c) && c.IsVisible).ToArray();
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
