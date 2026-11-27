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
        RowDefinitions = new RowDefinitions("Auto,Auto");
        // 工具行默认收起：阅读态跳过工具条的量测/渲染（巨文档多表格累计可观），
        // 悬浮表格区域时出现（Typora 同款交互）；工具行与表格同 Grid，移入工具行不丢焦点。
        tools.IsVisible = false;
        PointerEntered += (_, _) => tools.IsVisible = true;
        PointerExited += (_, _) => tools.IsVisible = false;
        Children.Add(tools);
        Children.Add(table);
        SetRow(table, 1);
    }

    public MarkdownTableData Data { get; }
}
