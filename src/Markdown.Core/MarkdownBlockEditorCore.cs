using CodeWF.Avalonia.Markdown.Core.Blocks;

namespace CodeWF.Avalonia.Markdown.Core;

/// <summary>
/// 块编辑核心：对块列表的键盘级操作（回车拆块/退格合并/空文档首块），
/// 全部为纯模型操作并返回焦点指令，零 UI 依赖——UI 层只负责渲染状态与执行焦点。
/// Enter 语义对齐 Mark Text muya（MIT）：当前块留光标前文本、光标后文本拆入新段落，
/// 空段落可连续存在（末尾连敲即持续加空行），有序列表编号自动递增。
/// </summary>
public sealed class MarkdownBlockEditorCore
{
    private readonly List<MarkdownBlockModel> _models;

    public MarkdownBlockEditorCore(List<MarkdownBlockModel> models) => _models = models;

    public IReadOnlyList<MarkdownBlockModel> Blocks => _models;

    public int Count => _models.Count;

    /// <summary>编辑结果：Changed 表示块列表有结构变化（UI 需重建块视图）；Focus 为焦点块下标。</summary>
    public sealed record BlockEditResult(bool Changed, int FocusIndex, int Caret);

    /// <summary>
    /// 空文档保证一个空段落块（Typora 行为：空白文档可点击落光标）。
    /// 已有块时不做任何事。返回是否插入了首块。
    /// </summary>
    public bool EnsureFirstBlock()
    {
        if (_models.Count > 0) return false;
        _models.Add(new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph });
        return true;
    }

    /// <summary>
    /// 回车：在 <paramref name="caret"/> 处把块拆为两段（光标后文本进新块），返回焦点指令。
    /// 标题块拆出的新块为段落；空列表项回车降级为普通段落；有序列表编号自动递增。
    /// 代码块/原始块不参与拆分（返回未变更）。
    /// </summary>
    public BlockEditResult Enter(int index, int caret)
    {
        if (index < 0 || index >= _models.Count)
            return new BlockEditResult(false, Math.Clamp(index, 0, Math.Max(0, _models.Count - 1)), caret);

        var model = _models[index];
        // 代码块/原始块回车=块内换行（由 UI 层的编辑器原生处理），不拆块。
        if (model.Kind is MarkdownBlockKind.Code or MarkdownBlockKind.Raw)
            return new BlockEditResult(false, index, caret);
        caret = Math.Clamp(caret, 0, model.Text.Length);
        var next = new MarkdownBlockModel
        {
            Kind = model.Kind == MarkdownBlockKind.Heading ? MarkdownBlockKind.Paragraph : model.Kind,
            Text = model.Text[caret..],
            IndentLevel = model.IndentLevel,
            QuoteDepth = model.QuoteDepth,
            IsTask = model.IsTask,
            IsOrdered = model.IsOrdered,
            OrderedNumber = model.OrderedNumber + (model.IsOrdered ? 1 : 0),
            Source = model.Kind == MarkdownBlockKind.ListItem ? model.Source : null
        };
        model.Text = model.Text[..caret];
        var focusIndex = index + 1;
        if (model.Kind == MarkdownBlockKind.ListItem && model.Text.Length == 0)
        {
            // 空列表项回车：降级为普通段落（焦点留在当前块）。
            model.Kind = MarkdownBlockKind.Paragraph;
            model.IsTask = model.IsOrdered = false;
            focusIndex = index;
        }
        else
        {
            _models.Insert(index + 1, next);
            if (model.IsOrdered)
            {
                for (var following = index + 2; following < _models.Count; following++)
                {
                    var item = _models[following];
                    if (!ReferenceEquals(item.Source, model.Source)) break;
                    if (item.IsOrdered && item.IndentLevel == model.IndentLevel) item.OrderedNumber++;
                }
            }
        }

        return new BlockEditResult(true, focusIndex, 0);
    }

    /// <summary>
    /// Delete（光标在块末）：与下一相邻块合并，光标落在合并点。不可合并返回 null。
    /// </summary>
    public BlockEditResult? MergeForward(int index)
    {
        if (index < 0 || index >= _models.Count - 1) return null;
        var current = _models[index];
        if (current.Kind is MarkdownBlockKind.Code or MarkdownBlockKind.Raw) return null;
        var next = _models[index + 1];
        if (next.Kind is MarkdownBlockKind.Code or MarkdownBlockKind.Raw) return null;
        var caret = current.Text.Length;
        current.Text += next.Text;
        _models.RemoveAt(index + 1);
        return new BlockEditResult(true, index, caret);
    }

    /// <summary>
    /// 退格（光标在块首或块内首位置）：标题/列表/引用降级为段落，或与相邻块合并。
    /// 返回焦点指令；无法合并时返回 null（UI 保留默认退格行为）。
    /// </summary>
    public BlockEditResult? Backspace(int index)
    {
        if (index < 0 || index >= _models.Count) return null;
        var current = _models[index];
        if (current.Kind is MarkdownBlockKind.Code or MarkdownBlockKind.Raw) return null;
        if (current.Kind is MarkdownBlockKind.Heading or MarkdownBlockKind.ListItem or MarkdownBlockKind.Quote)
        {
            // 块首退格：降级为普通段落（保留文本）。
            current.Kind = MarkdownBlockKind.Paragraph;
            current.HeadingLevel = current.QuoteDepth = current.IndentLevel = 0;
            current.IsTask = current.IsOrdered = false;
            return new BlockEditResult(true, index, 0);
        }

        var adjacent = index - 1;
        if (adjacent < 0 || adjacent >= _models.Count
            || _models[adjacent].Kind is not (MarkdownBlockKind.Paragraph or MarkdownBlockKind.Heading
                or MarkdownBlockKind.ListItem or MarkdownBlockKind.Quote))
            return null;
        var left = _models[adjacent];
        var caret = left.Text.Length;
        left.Text += current.Text;
        _models.RemoveAt(index);
        return new BlockEditResult(true, adjacent, caret);
    }
}
