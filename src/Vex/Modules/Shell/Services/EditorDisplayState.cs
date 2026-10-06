using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class EditorDisplayState : IEditorDisplayState
{
    private double _editorFontSize = 15;
    private bool _showLineNumbers;
    private bool _enableAutoPair = true;

    public event EventHandler? Changed;

    public double EditorFontSize => _editorFontSize;

    public bool ShowLineNumbers => _showLineNumbers;

    public bool EnableAutoPair => _enableAutoPair;

    public void Update(double editorFontSize, bool showLineNumbers, bool enableAutoPair)
    {
        if (Math.Abs(_editorFontSize - editorFontSize) < 0.01
            && _showLineNumbers == showLineNumbers
            && _enableAutoPair == enableAutoPair)
        {
            return;
        }

        _editorFontSize = editorFontSize;
        _showLineNumbers = showLineNumbers;
        _enableAutoPair = enableAutoPair;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
