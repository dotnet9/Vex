using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CodeWF.EventBus;
using CodeWF.Markdown.Editor.Controls;
using Vex.Core.Messaging;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// 源码编辑器宿主视图：AvaloniaEdit 宿主、语法着色、自动配对、动作与查找全部由
/// <see cref="MarkdownEditorView"/> 提供，这里只做 Shell 消息总线、显示偏好与快捷键的桥接。
/// </summary>
public partial class MarkdownEditorView : UserControl
{
    public MarkdownEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        AttachedToVisualTree += (_, _) => AttachViewModel();
        DetachedFromVisualTree += (_, _) => ViewModel?.DetachEditor(MarkdownEditor);
        // 控件正文变化统一由 ViewModel 回抛，文档载入期间的回抛会在 ViewModel 里被抑制。
        MarkdownEditor.MarkdownChanged += (_, _) => ViewModel?.PublishTextChanged();
        MarkdownEditor.SearchResultProduced += (_, result) =>
            EventBus.Default.Publish(
                new EditorSearchResultCommand(result.Message, result.CurrentIndex, result.TotalCount));
        MarkdownEditor.Navigated += (_, caret) =>
            ViewModel?.PublishSelectionChanged(caret.Line, caret.Column, caret.LineCount);
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
