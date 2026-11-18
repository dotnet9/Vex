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
        Children.Add(tools);
        Children.Add(table);
        SetRow(table, 1);
    }

    public MarkdownTableData Data { get; }
}
