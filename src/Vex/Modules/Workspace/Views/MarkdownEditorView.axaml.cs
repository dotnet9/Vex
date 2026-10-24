using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;
using CodeWF.Toolkit.EventBus;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Core.Messaging;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Shell.ViewModels;
using Vex.Controls;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// 源码编辑器宿主视图：AvaloniaEdit 宿主、语法着色、自动配对、动作与查找全部由
/// <see cref="MarkdownEditorView"/> 提供，这里只做 Shell 消息总线、显示偏好与快捷键的桥接。
/// </summary>
public partial class MarkdownEditorView : UserControl
{
    private ShellWindowLayoutViewModel? _layout;
    public MarkdownEditorView()
    {
        InitializeComponent();
        MenuIconCatalog.Apply((ContextMenu)MarkdownEditor.ContextMenu!);
        MarkdownEditor.Editor.FontFamily = Vex.Core.Services.VexFonts.Code;
        DataContextChanged += (_, _) => AttachViewModel();
        AttachedToVisualTree += (_, _) => AttachViewModel();
        DetachedFromVisualTree += (_, _) =>
        {
            ViewModel?.DetachEditor(MarkdownEditor);
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = null;
        };
        MarkdownEditor.Editor.TextArea.Caret.PositionChanged += (_, _) => CenterCaret();
        // 控件正文变化统一由 ViewModel 回抛，文档载入期间的回抛会在 ViewModel 里被抑制。
        MarkdownEditor.MarkdownChanged += (_, _) => ViewModel?.PublishTextChanged();
        MarkdownEditor.SearchResultProduced += (_, result) =>
            EventBus.Default.Publish(
                new EditorSearchResultCommand(result.Message, result.CurrentIndex, result.TotalCount));
        MarkdownEditor.Navigated += (_, caret) =>
            ViewModel?.PublishSelectionChanged(caret.Line, caret.Column, caret.LineCount);
        EventBus.Default.Subscribe<MarkdownDocumentSwitchedCommand>(OnDocumentSwitched);
    }

    private void OnDocumentSwitched(MarkdownDocumentSwitchedCommand command)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var editor = MarkdownEditor.Editor;
            if (editor.Document is null) return;
            editor.CaretOffset = 0;
            editor.TextArea.Caret.BringCaretToView();
            var scroll = editor.FindAncestorOfType<ScrollViewer>(true);
            if (scroll is not null) scroll.Offset = new Avalonia.Vector(scroll.Offset.X, 0);
        }, DispatcherPriority.Loaded);
    }

    private MarkdownEditorViewModel? ViewModel => DataContext as MarkdownEditorViewModel;

    private void AttachViewModel()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.AttachEditor(MarkdownEditor);
        var layout = (this.FindAncestorOfType<Window>()?.DataContext as MainWindowViewModel)?.Layout;
        if (!ReferenceEquals(layout, _layout))
        {
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = layout;
            if (_layout is not null) _layout.PropertyChanged += LayoutChanged;
        }
        MarkdownEditor.Editor.Options.AllowScrollBelowDocument = _layout?.IsTypewriterMode == true;
    }

    private void LayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellWindowLayoutViewModel.IsTypewriterMode)) return;
        MarkdownEditor.Editor.Options.AllowScrollBelowDocument = _layout?.IsTypewriterMode == true;
        CenterCaret();
    }

    private void CenterCaret()
    {
        if (_layout?.IsTypewriterMode != true || !IsEffectivelyVisible) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_layout?.IsTypewriterMode != true || !IsEffectivelyVisible) return;
            var editor = MarkdownEditor.Editor;
            editor.UpdateLayout();
            editor.TextArea.TextView.EnsureVisualLines();
            var caret = editor.TextArea.Caret.CalculateCaretRectangle();
            if (editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scroll)
                scroll.Offset = new Avalonia.Vector(scroll.Offset.X, Math.Max(0, caret.Center.Y - scroll.Viewport.Height / 2));
        }, DispatcherPriority.Background);
    }

    // Ctrl/Cmd+Shift+C 复制为纯文本；Enter 智能换行与 Tab 缩进由库控件内部处理。
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.C && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            ViewModel?.CopyPlainText();
        }
    }

    private void OnCopyPlainTextClick(object? sender, RoutedEventArgs e) => ViewModel?.CopyPlainText();
}
