using System.Windows.Input;
using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>选区浮动工具条视图模型：命令与行内样式面板；文案走 XAML 的 I18n 标记扩展（随语言切换自动重取）。</summary>
internal sealed class MarkdownSelectionToolbarViewModel
{
    internal MarkdownSelectionToolbarViewModel(Action<MarkdownEditorAction> format, Action<string, string> apply,
        Func<string, string?> query, Action clear, Action close)
    {
        FormatCommand = new MarkdownFormattingCommand(value => { format((MarkdownEditorAction)value!); close(); });
        ClearCommand = new MarkdownFormattingCommand(_ => { clear(); close(); });
        FontSize = new("font-size", Apply, Clear, query);
        Foreground = new("color", Apply, Clear, query);
        Background = new("background-color", Apply, Clear, query);
        void Apply(string property, string value) { apply(property, value); close(); }
        void Clear() { clear(); close(); }
    }
    public ICommand FormatCommand { get; }
    public ICommand ClearCommand { get; }
    public MarkdownSpanStylePickerViewModel FontSize { get; }
    public MarkdownSpanStylePickerViewModel Foreground { get; }
    public MarkdownSpanStylePickerViewModel Background { get; }
}
