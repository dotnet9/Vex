using AvaloniaEdit;

namespace Vex.Modules.Workspace.Services;

public interface IMarkdownEditorMutationService
{
    void WrapSelection(TextEditor editor, string prefix, string suffix, string placeholder);

    void InsertText(TextEditor editor, string insertion);

    void InsertLink(TextEditor editor, string textPlaceholder, string urlPlaceholder);

    void InsertImage(TextEditor editor, string altPlaceholder, string targetPlaceholder);

    void InsertTable(TextEditor editor, string fallbackInsertion);

    void IndentSelection(TextEditor editor);

    void OutdentSelection(TextEditor editor);

    void ClearFormatting(TextEditor editor);

    void PrefixCurrentLine(TextEditor editor, string prefix);
}
