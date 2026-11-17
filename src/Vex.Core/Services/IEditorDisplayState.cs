namespace Vex.Core.Services;

public interface IEditorDisplayState
{
    event EventHandler? Changed;

    double EditorFontSize { get; }

    bool ShowLineNumbers { get; }

    /// <summary>自动配对开关（默认开启）。</summary>
    bool EnableAutoPair { get; }

    void Update(double editorFontSize, bool showLineNumbers, bool enableAutoPair);
}
