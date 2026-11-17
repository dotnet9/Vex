namespace Vex.Core.Messaging;

/// <summary>表格结构动作，语义与原型 document-operations.js 的 changeTable 一致。</summary>
public enum MarkdownTableAction
{
    RowAbove,
    RowBelow,
    RowDelete,
    RowUp,
    RowDown,
    ColBefore,
    ColAfter,
    ColDelete,
    ColLeft,
    ColRight,
    AlignLeft,
    AlignCenter,
    AlignRight,
}
