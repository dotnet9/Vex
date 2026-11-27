using global::Avalonia.Controls;
using CodeWF.Avalonia.Markdown.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>表格和宿主工具共用一个文档块，保持虚拟化、索引与块高度一致。</summary>
public sealed class MarkdownTableBlock : Grid
{
    protected override Type StyleKeyOverride => typeof(Grid);

    internal MarkdownTableBlock(Border table, Control tools, MarkdownTableData data)
    {
        Data = data;
        Classes.Add(MarkdownStyleKeys.TableBlock);
        RowDefinitions = new RowDefinitions("Auto");
        // 工具条悬浮叠加在表格右上角（同单元格 + 高 ZIndex），显隐不改变表格位置——
        // 曾放独立 Auto 工具行：悬浮出现→表格下移→指针落出网格→收起→表格回弹→再进入，
        // 布局振荡表现为表格不停晃动（真机复现）；叠加定位从结构上消除该环。
        tools.IsVisible = false;
        PointerEntered += (_, _) => tools.IsVisible = true;
        PointerExited += (_, _) => tools.IsVisible = false;
        Children.Add(tools);
        Children.Add(table);
        SetRow(table, 0);
        SetRow(tools, 0);
        tools.ZIndex = 1;
        tools.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
    }

    public MarkdownTableData Data { get; }
}
