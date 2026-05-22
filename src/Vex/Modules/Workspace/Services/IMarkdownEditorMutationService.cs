using AvaloniaEdit;

namespace Vex.Modules.Workspace.Services;

public interface IMarkdownEditorMutationService
{
    void WrapSelection(TextEditor editor, string prefix, string suffix, string placeholder);

    void InsertText(TextEditor editor, string insertion);

    void IndentSelection(TextEditor editor);

    void OutdentSelection(TextEditor editor);

    void ClearFormatting(TextEditor editor);

    void PrefixCurrentLine(TextEditor editor, string prefix);
}
