using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

internal partial class MarkdownSelectionToolbar : UserControl
{
    public MarkdownSelectionToolbar() => InitializeComponent();

    /// <summary>
    /// 宿主扩展按钮追加到工具条尾部（每项前置分隔线）。默认集合为空时工具条与既有行为完全一致，
    /// 既有消费者（如 Vex）不受影响。extras 在 UserControl 视觉树内，可直接复用 .format 样式类。
    /// </summary>
    internal void AppendExtras(IEnumerable<Control> extras)
    {
        foreach (var extra in extras)
        {
            Items.Children.Add(new Border { Classes = { "rule" } });
            Items.Children.Add(extra);
        }
    }
}
