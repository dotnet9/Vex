namespace Vex.Core.Messaging;

public enum EditorActionKind
{
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Undo, Gesture = "Ctrl+Z")]
    Undo,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Redo, Gesture = "Ctrl+Y")]
    Redo,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Cut, Gesture = "Ctrl+X")]
    Cut,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Copy, Gesture = "Ctrl+C")]
    Copy,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Paste, Gesture = "Ctrl+V")]
    Paste,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.SelectAll, Gesture = "Ctrl+A")]
    SelectAll,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.CopyPlainText, Gesture = "Ctrl+Shift+C")]
    CopyPlainText,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Find, Gesture = "Ctrl+F")]
    ShowFindPanel,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Replace, Gesture = "Ctrl+H")]
    ShowReplacePanel,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Paragraph)]
    Paragraph,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Heading1)]
    Heading1,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Heading2)]
    Heading2,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Heading3)]
    Heading3,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Heading4)]
    Heading4,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Heading5)]
    Heading5,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Heading6)]
    Heading6,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.UnorderedList)]
    UnorderedList,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.OrderedList)]
    OrderedList,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.TaskList)]
    TaskList,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Quote)]
    Quote,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Table)]
    Table,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.CodeFence)]
    CodeFence,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.MathBlock)]
    MathBlock,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.HorizontalRule)]
    HorizontalRule,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.SmartNewLine)]
    SmartNewLine,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Indent)]
    Indent,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Outdent)]
    Outdent,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Link)]
    Link,
    [VexCommand(VexCommandKeys.GroupInsert, VexCommandKeys.Image)]
    Image,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.Bold, Gesture = "Ctrl+B")]
    Bold,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.Italic, Gesture = "Ctrl+I")]
    Italic,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.Strikethrough, Gesture = "Ctrl+Shift+5")]
    Strikethrough,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.Highlight, Gesture = "Ctrl+Shift+H")]
    Highlight,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.Superscript, Gesture = "Ctrl+Shift+=")]
    Superscript,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.Subscript, Gesture = "Ctrl+Shift+-")]
    Subscript,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.InlineCode, Gesture = "Ctrl+Shift+`")]
    InlineCode,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.ClearFormatting)]
    ClearFormatting,
    [VexCommand(VexCommandKeys.GroupFormat, VexCommandKeys.ClearSpanStyles)]
    ClearSpanStyles,
    [VexCommand(VexCommandKeys.GroupView, VexCommandKeys.FocusEditor)]
    FocusEditor,
}
