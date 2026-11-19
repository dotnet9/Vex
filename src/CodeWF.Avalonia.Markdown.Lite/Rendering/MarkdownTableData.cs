using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>表格的只读明文快照；不依赖单元格是否已布局或物化。</summary>
public sealed class MarkdownTableData
{
    public MarkdownTableData(IEnumerable<IEnumerable<string>> rows, bool hasHeader)
    {
        Rows = Array.AsReadOnly(rows.Select(row =>
            (IReadOnlyList<string>)Array.AsReadOnly(row.ToArray())).ToArray());
        HasHeader = hasHeader;
    }

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
    public bool HasHeader { get; }
}

/// <summary>创建随表格块一起物化、回收的工具；返回 null 保留默认表格。</summary>
public delegate Control? MarkdownTableToolsFactory(IMarkdownRenderContext context, MarkdownTableData data);
