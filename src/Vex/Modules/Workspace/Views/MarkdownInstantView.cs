using Avalonia;
using Avalonia.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Workspace.Views;

public sealed class MarkdownInstantView : UserControl
{
    public MarkdownLiveEditorView Editor { get; } = new()
    {
        HeaderText = null, ChipText = null, EditorFontSize = 15,
        EditorBackgroundKey = "VexPreviewBackgroundBrush", EditorForegroundKey = "VexEditorForegroundBrush"
    };
    private ShellWorkbenchViewModel? _workbench;
    public MarkdownInstantView()
    {
        Content = Editor;
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) => { _workbench?.DetachLiveEditor(Editor); _workbench = null; };
    }
    private void Attach()
    {
        var workbench = (DataContext as MainWindowViewModel)?.Workbench;
        if (ReferenceEquals(workbench, _workbench)) return;
        _workbench?.DetachLiveEditor(Editor);
        _workbench = workbench;
        workbench?.AttachLiveEditor(Editor);
    }
}
