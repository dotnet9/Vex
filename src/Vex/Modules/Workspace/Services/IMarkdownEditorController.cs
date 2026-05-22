using AvaloniaEdit;
using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.Services;

public interface IMarkdownEditorController
{
    void Attach(TextEditor editor);

    void Detach(TextEditor editor);

    void SyncText(string? markdown);

    void PublishTextChanged();

    void Execute(EditorActionCommand command);

    void NavigateTo(NavigateToLineCommand command);
}
