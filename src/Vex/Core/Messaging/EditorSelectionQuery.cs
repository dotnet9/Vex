using CodeWF.EventBus;

namespace Vex.Core.Messaging;

public sealed class EditorSelectionQuery : Query<EditorSelectionInfo>
{
    public override EditorSelectionInfo Result { get; set; } = new(string.Empty, 0, 0);
}

public sealed record EditorSelectionInfo(string Text, int StartOffset, int Length);
