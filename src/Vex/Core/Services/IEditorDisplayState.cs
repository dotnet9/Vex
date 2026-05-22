namespace Vex.Core.Services;

public interface IEditorDisplayState
{
    event EventHandler? Changed;

    double EditorFontSize { get; }

    bool ShowLineNumbers { get; }

    void Update(double editorFontSize, bool showLineNumbers);
}
