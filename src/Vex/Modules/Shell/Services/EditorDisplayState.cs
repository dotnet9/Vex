using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class EditorDisplayState : IEditorDisplayState
{
    private double _editorFontSize = 15;
    private bool _showLineNumbers;

    public event EventHandler? Changed;

    public double EditorFontSize => _editorFontSize;

    public bool ShowLineNumbers => _showLineNumbers;

    public void Update(double editorFontSize, bool showLineNumbers)
    {
        if (Math.Abs(_editorFontSize - editorFontSize) < 0.01 && _showLineNumbers == showLineNumbers)
        {
            return;
        }

        _editorFontSize = editorFontSize;
        _showLineNumbers = showLineNumbers;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
