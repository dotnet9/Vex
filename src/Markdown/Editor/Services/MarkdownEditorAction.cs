namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>
/// 编辑器动作种类（库内定义，宿主可自行映射自己的枚举/快捷键）。
/// </summary>
public enum MarkdownEditorAction
{
    Undo,
    Redo,
    Cut,
    Copy,
    Paste,
    SelectAll,
    CopyPlainText,
    Bold,
    Italic,
    InlineCode,
    Link,
    Image,
    ClearFormatting,
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Quote,
    UnorderedList,
    OrderedList,
    TaskList,
    CodeFence,
    Table,
    MathBlock,
    HorizontalRule,
    SmartNewLine,
    Indent,
    Outdent,
    ClearSpanStyles,
    FocusEditor,
    Strikethrough,
    Highlight,
    Superscript,
    Subscript
}
