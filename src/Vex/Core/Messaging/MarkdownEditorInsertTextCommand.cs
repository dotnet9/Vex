namespace Vex.Core.Messaging;

/// <summary>在编辑器当前插入点插入文本（图片拖入插入 ![](...) 等场景）。</summary>
public sealed class MarkdownEditorInsertTextCommand : CodeWF.EventBus.Command
{
    public MarkdownEditorInsertTextCommand(string text)
    {
        Text = text;
    }

    public string Text { get; }
}
