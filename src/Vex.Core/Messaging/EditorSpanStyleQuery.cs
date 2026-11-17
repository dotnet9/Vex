using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

/// <summary>查询当前选区所在的行内样式值（紧贴同款 span 时返回，供面板预填），否则空串。</summary>
public sealed class EditorSpanStyleQuery : Query<EditorSpanStyleInfo>
{
    public override EditorSpanStyleInfo Result { get; set; } = new(string.Empty);

    public EditorSpanStyleQuery(string property) => Property = property;

    public string Property { get; }
}

public sealed record EditorSpanStyleInfo(string Value);
