using CodeWF.Avalonia.Markdown.Core.Blocks;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>表格结构编辑；保留表头、至少一列及已有列对齐。</summary>
public static class MarkdownTableEditor
{
    /// <param name="action">0 追加行，1 删除末行，2 追加列，3 删除末列。</param>
    public static bool ChangeStructure(MarkdownBlockModel model, int action)
    {
        if (model.Kind != MarkdownBlockKind.Table || model.Cells.Count == 0) return false;
        var columns = model.Cells.Max(row => row.Count);
        switch (action)
        {
            case 0:
                model.Cells.Add(Enumerable.Repeat(string.Empty, Math.Max(1, columns)).ToList());
                return true;
            case 1 when model.Cells.Count > 1:
                model.Cells.RemoveAt(model.Cells.Count - 1);
                return true;
            case 2:
                foreach (var row in model.Cells)
                {
                    while (row.Count < columns) row.Add(string.Empty);
                    row.Add(string.Empty);
                }
                model.Alignments = model.Alignments.Concat([TableCellAlignment.None]).ToList();
                return true;
            case 3 when columns > 1:
                foreach (var row in model.Cells)
                    if (row.Count == columns) row.RemoveAt(columns - 1);
                model.Alignments = model.Alignments.Take(columns - 1).ToList();
                return true;
            default:
                return false;
        }
    }
}
