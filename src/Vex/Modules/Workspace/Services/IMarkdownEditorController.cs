using Avalonia.Input;

using AvaloniaEdit;

using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.Services;

public interface IMarkdownEditorController
{
    void Attach(TextEditor editor);

    void Detach(TextEditor editor);

    void SyncText(string? markdown);

    void PublishTextChanged();

    /// <summary>自动配对开关（宿主显示偏好下发）。</summary>
    void SetAutoPairEnabled(bool enabled);

    /// <summary>处理退格：命中空配对时删除两侧并返回 true。</summary>
    bool TryHandleAutoPairBackspace();

    /// <summary>在当前插入点插入文本（不覆盖选区时保持撤销栈一致）。</summary>
    void InsertText(string text);

    /// <summary>复制为纯文本：有选区复制选区，否则复制整篇文档文本。</summary>
    Task CopyPlainTextAsync();

    /// <summary>
    /// 应用外部改写（预览任务勾选回写）：长度一致时只替换目标区间并保持光标，
    /// 否则整篇同步，保证编辑器与文档状态一致。
    /// </summary>
    void ApplyExternalEdit(string markdown, int start, int length);

    void Execute(EditorActionCommand command);

    void NavigateTo(NavigateToLineCommand command);
}
