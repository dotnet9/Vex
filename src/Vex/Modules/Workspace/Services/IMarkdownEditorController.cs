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

    void Execute(EditorActionCommand command);

    void NavigateTo(NavigateToLineCommand command);
}
