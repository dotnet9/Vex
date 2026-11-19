using System.Text;

using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;

using AvaloniaEdit;

namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>
/// 默认动作实现：撤销/重做/剪贴板、加粗斜体等包裹、标题与列表前缀、代码围栏、表格、公式、
/// 智能换行与缩进。
/// <para>
/// 粘贴时可选择接入 <see cref="IMarkdownHtmlPasteConverter"/>（宿主引用
/// <c>CodeWF.Avalonia.Markdown.Export</c> 后把 <c>MarkdownHtmlClipboard.Html2Markdown</c> 接进来），
/// 从而支持「粘贴网页内容自动转 Markdown」；未接入时走普通文本粘贴。
/// </para>
/// </summary>
public sealed class MarkdownEditorActionService : IMarkdownEditorActionService
{
    private readonly IMarkdownEditorTemplateService _templates;
    private readonly IMarkdownEditorMutationService _textMutationService;
    private readonly MarkdownEditorOptions _options;
    private readonly IMarkdownHtmlPasteConverter? _htmlPaste;

    public MarkdownEditorActionService(
        IMarkdownEditorTemplateService templates,
        IMarkdownEditorMutationService textMutationService,
        MarkdownEditorOptions options,
        IMarkdownHtmlPasteConverter? htmlPaste = null)
    {
        _templates = templates;
        _textMutationService = textMutationService;
        _options = options;
        _htmlPaste = htmlPaste;
    }

    public async Task ExecuteAsync(TextEditor editor, MarkdownEditorAction action, Action<Action> runTextMutation)
    {
        switch (action)
        {
            case MarkdownEditorAction.Undo:
                runTextMutation(() => editor.Undo());
                break;
            case MarkdownEditorAction.Redo:
                runTextMutation(() => editor.Redo());
                break;
            case MarkdownEditorAction.Cut:
                runTextMutation(editor.Cut);
                break;
            case MarkdownEditorAction.Copy:
                editor.Copy();
                break;
            case MarkdownEditorAction.Paste:
                if (!await TryPasteHtmlAsMarkdownAsync(editor, runTextMutation))
                {
                    runTextMutation(editor.Paste);
                }

                break;
            case MarkdownEditorAction.SelectAll:
                editor.SelectAll();
                break;
            case MarkdownEditorAction.Bold:
                WrapSelection(editor, "**", "**", _templates.BoldPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.Italic:
                WrapSelection(editor, "*", "*", _templates.ItalicPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.Strikethrough:
                WrapSelection(editor, "~~", "~~", _templates.BoldPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.Highlight:
                WrapSelection(editor, "==", "==", _templates.BoldPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.Superscript:
                WrapSelection(editor, "^", "^", _templates.BoldPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.Subscript:
                WrapSelection(editor, "~", "~", _templates.BoldPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.InlineCode:
                WrapSelection(editor, "`", "`", _templates.InlineCodePlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.Link:
                MutateEditor(
                    editor,
                    currentEditor => _textMutationService.InsertLink(
                        currentEditor,
                        _templates.LinkPlaceholder,
                        _templates.LinkUrlPlaceholder),
                    runTextMutation);
                break;
            case MarkdownEditorAction.Image:
                MutateEditor(
                    editor,
                    currentEditor => _textMutationService.InsertImage(
                        currentEditor,
                        _templates.ImageAltPlaceholder,
                        _templates.ImageTargetPlaceholder),
                    runTextMutation);
                break;
            case MarkdownEditorAction.ClearFormatting:
                MutateEditor(editor, _textMutationService.ClearFormatting, runTextMutation);
                break;
            case MarkdownEditorAction.Paragraph:
                PrefixCurrentLine(editor, string.Empty, runTextMutation);
                break;
            case MarkdownEditorAction.Heading1:
                PrefixCurrentLine(editor, "# ", runTextMutation);
                break;
            case MarkdownEditorAction.Heading2:
                PrefixCurrentLine(editor, "## ", runTextMutation);
                break;
            case MarkdownEditorAction.Heading3:
                PrefixCurrentLine(editor, "### ", runTextMutation);
                break;
            case MarkdownEditorAction.Heading4:
                PrefixCurrentLine(editor, "#### ", runTextMutation);
                break;
            case MarkdownEditorAction.Heading5:
                PrefixCurrentLine(editor, "##### ", runTextMutation);
                break;
            case MarkdownEditorAction.Heading6:
                PrefixCurrentLine(editor, "###### ", runTextMutation);
                break;
            case MarkdownEditorAction.Quote:
                PrefixCurrentLine(editor, "> ", runTextMutation);
                break;
            case MarkdownEditorAction.UnorderedList:
                PrefixCurrentLine(editor, "- ", runTextMutation);
                break;
            case MarkdownEditorAction.OrderedList:
                PrefixCurrentLine(editor, "1. ", runTextMutation);
                break;
            case MarkdownEditorAction.TaskList:
                PrefixCurrentLine(editor, "- [ ] ", runTextMutation);
                break;
            case MarkdownEditorAction.CodeFence:
                WrapSelection(
                    editor,
                    $"```{_options.CodeFenceLanguage}\n",
                    "\n```",
                    _templates.CodeFencePlaceholder,
                    runTextMutation);
                break;
            case MarkdownEditorAction.Table:
                MutateEditor(
                    editor,
                    currentEditor => _textMutationService.InsertTable(currentEditor, _templates.TableInsertion),
                    runTextMutation);
                break;
            case MarkdownEditorAction.MathBlock:
                WrapSelection(editor, "$$\n", "\n$$", _templates.MathPlaceholder, runTextMutation);
                break;
            case MarkdownEditorAction.HorizontalRule:
                InsertText(editor, "\n---\n", runTextMutation);
                break;
            case MarkdownEditorAction.SmartNewLine:
                MutateEditor(editor, _textMutationService.InsertSmartNewLine, runTextMutation);
                break;
            case MarkdownEditorAction.Indent:
                MutateEditor(editor, _textMutationService.IndentSelection, runTextMutation);
                break;
            case MarkdownEditorAction.Outdent:
                MutateEditor(editor, _textMutationService.OutdentSelection, runTextMutation);
                break;
            case MarkdownEditorAction.ClearSpanStyles:
                MutateEditor(editor, _textMutationService.ClearSpanStyles, runTextMutation);
                break;
            case MarkdownEditorAction.FocusEditor:
                editor.Focus();
                break;
        }
    }

    public void ExecuteSpan(TextEditor editor, string property, string value, Action<Action> runTextMutation)
    {
        MutateEditor(editor, currentEditor => _textMutationService.WrapSpan(currentEditor, property, value), runTextMutation);
    }

    private async Task<bool> TryPasteHtmlAsMarkdownAsync(TextEditor editor, Action<Action> runTextMutation)
    {
        if (_htmlPaste is null)
        {
            return false;
        }

        try
        {
            var clipboard = TopLevel.GetTopLevel(editor)?.Clipboard;
            if (clipboard is null)
            {
                return false;
            }

            var htmlContent = await TryGetClipboardHtmlAsync(clipboard);
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return false;
            }

            var markdown = _htmlPaste.Html2Markdown(htmlContent);
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return false;
            }

            runTextMutation(() => _textMutationService.InsertText(editor, markdown));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> TryGetClipboardHtmlAsync(IClipboard clipboard)
    {
        var htmlFormat = DataFormat.CreateStringPlatformFormat(MarkdownHtmlPasteFormats.HtmlMime);
        var html = await clipboard.TryGetValueAsync(htmlFormat);
        if (!string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var macHtmlFormat = DataFormat.CreateStringPlatformFormat(MarkdownHtmlPasteFormats.MacHtml);
        html = await clipboard.TryGetValueAsync(macHtmlFormat);
        if (!string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var windowsHtmlFormat = DataFormat.CreateBytesPlatformFormat(MarkdownHtmlPasteFormats.WindowsHtml);
        var windowsHtml = await clipboard.TryGetValueAsync(windowsHtmlFormat);
        return windowsHtml is { Length: > 0 }
            ? Encoding.UTF8.GetString(windowsHtml)
            : null;
    }

    private void WrapSelection(
        TextEditor editor,
        string prefix,
        string suffix,
        string placeholder,
        Action<Action> runTextMutation)
    {
        MutateEditor(
            editor,
            currentEditor => _textMutationService.WrapSelection(currentEditor, prefix, suffix, placeholder),
            runTextMutation);
    }

    private void InsertText(TextEditor editor, string insertion, Action<Action> runTextMutation)
    {
        MutateEditor(editor, currentEditor => _textMutationService.InsertText(currentEditor, insertion), runTextMutation);
    }

    private void PrefixCurrentLine(TextEditor editor, string prefix, Action<Action> runTextMutation)
    {
        MutateEditor(editor, currentEditor => _textMutationService.PrefixCurrentLine(currentEditor, prefix), runTextMutation);
    }

    private static void MutateEditor(TextEditor editor, Action<TextEditor> mutation, Action<Action> runTextMutation)
    {
        runTextMutation(() => mutation(editor));
    }
}
