using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Workspace.Views;

/// <summary>Owns the selected chapter draft, commit boundary and inspector focus.</summary>
public partial class MindMapNodeInspectorView : UserControl
{
    private bool _refreshing;
    private int? _inspectedStart;
    private ShellWorkbenchViewModel? Workbench => (DataContext as MainWindowViewModel)?.Workbench;
    public event Action<string>? ChapterCommandRequested;
    public event Action? CanvasFocusRequested;

    public MindMapNodeInspectorView() => InitializeComponent();

    public void Refresh()
    {
        _refreshing = true;
        try
        {
            _inspectedStart = Workbench?.SelectedNode?.SourceStart;
            ChapterTitleInput.Text = Workbench?.ChapterTitle;
            ChapterBodyInput.Text = Workbench?.ChapterBody;
        }
        finally { _refreshing = false; }
    }

    public void Flush()
    {
        if (_refreshing || Workbench is not { IsChapterSelected: true } state
            || state.SelectedNode?.SourceStart != _inspectedStart) return;
        var title = ChapterTitleInput.Text;
        var body = ChapterBodyInput.Text;
        var titleChanged = title is not null && title != state.ChapterTitle;
        var bodyChanged = body is not null && body != state.ChapterBody;
        if (titleChanged) state.CommitChapterTitle(title!);
        if (bodyChanged) state.CommitChapterBody(body!);
    }

    public void FocusTitle() => ChapterTitleInput.Focus();
    private void TitleLostFocus(object? sender, RoutedEventArgs e) => Flush();
    private void BodyLostFocus(object? sender, RoutedEventArgs e) => Flush();
    private void TitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Flush(); CanvasFocusRequested?.Invoke(); e.Handled = true; }
        if (e.Key == Key.Escape) { Refresh(); CanvasFocusRequested?.Invoke(); e.Handled = true; }
    }
    private void ChapterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string command }) ChapterCommandRequested?.Invoke(command);
    }
}
