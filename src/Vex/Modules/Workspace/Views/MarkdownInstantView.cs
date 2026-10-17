using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Data;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Workspace.Views;

public sealed class MarkdownInstantView : UserControl
{
    public MarkdownLiveEditorView Editor { get; } = new()
    {
        HeaderText = null, ChipText = null, EditorFontSize = 14.5, CodeFontFamily = Vex.Core.Services.VexFonts.Code, DocumentMaxWidth = 1280, DocumentBlockSpacing = 0, DocumentPadding = new Thickness(44, 34, 44, 70),
        EditorBackgroundKey = "VexPreviewBackgroundBrush", EditorForegroundKey = "VexEditorForegroundBrush"
    };
    private ShellWorkbenchViewModel? _workbench;
    private ShellWindowLayoutViewModel? _layout;
    public MarkdownInstantView()
    {
        var grid = new Grid();
        grid.Bind(BackgroundProperty, this.GetResourceObservable("VexPreviewBackgroundBrush"));
        grid.Children.Add(Editor); Content = grid;
        AttachedToVisualTree += (_, _) => EventBus.Default.Subscribe(this);
        DetachedFromVisualTree += (_, _) => EventBus.Default.Unsubscribe(this);
        SizeChanged += (_, _) => UpdatePadding();
        Editor.SelectionChanged += (_, _) => CenterCaret();
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) =>
        {
            _workbench?.DetachLiveEditor(Editor); _workbench = null;
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = null;
        };
    }
    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_workbench?.IsInstantEditing == true) Editor.NavigateToSourceLine(command.Line);
    }
    private void Attach()
    {
        var layout = (DataContext as MainWindowViewModel)?.Layout;
        if (!ReferenceEquals(layout, _layout))
        {
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = layout;
            if (layout is not null) layout.PropertyChanged += LayoutChanged;
            UpdatePadding();
        }
        var workbench = (DataContext as MainWindowViewModel)?.Workbench;
        if (ReferenceEquals(workbench, _workbench)) return;
        _workbench?.DetachLiveEditor(Editor);
        _workbench = workbench;
        workbench?.AttachLiveEditor(Editor);
    }

    private void LayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellWindowLayoutViewModel.IsTypewriterMode)) return;
        UpdatePadding(); CenterCaret();
    }

    private void UpdatePadding()
    {
        var narrow = Bounds.Width < 850;
        Editor.DocumentPadding = new Thickness(narrow ? 32 : 44, narrow ? 26 : 34,
            narrow ? 32 : 44, _layout?.IsTypewriterMode == true ? Math.Max(70, Editor.Bounds.Height / 2) : narrow ? 60 : 70);
    }

    private void CenterCaret()
    {
        if (_layout?.IsTypewriterMode != true || !IsEffectivelyVisible) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_layout?.IsTypewriterMode != true || !IsEffectivelyVisible) return;
            var box = Editor.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.IsEffectivelyVisible);
            var presenter = box?.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
            var scroll = Editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (presenter is null || scroll is null) return;
            var caret = presenter.TextLayout.HitTestTextPosition(box!.CaretIndex);
            if (presenter.TranslatePoint(caret.Center, scroll) is not { } point) return;
            scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y + point.Y - scroll.Viewport.Height / 2));
        }, DispatcherPriority.Background);
    }
}
