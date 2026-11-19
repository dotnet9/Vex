namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>使用库内嵌资源提供编辑器文案，自动跟随全局语言与宿主同键覆盖。</summary>
public sealed class MarkdownEditorLocalizer : IMarkdownEditorLocalizer
{
    public string Get(MarkdownEditorText text)
    {
        var key = text switch
        {
            MarkdownEditorText.BoldPlaceholder => MarkdownL.EditorBoldPlaceholder,
            MarkdownEditorText.ItalicPlaceholder => MarkdownL.EditorItalicPlaceholder,
            MarkdownEditorText.InlineCodePlaceholder => MarkdownL.EditorInlineCodePlaceholder,
            MarkdownEditorText.LinkPlaceholder => MarkdownL.EditorLinkPlaceholder,
            MarkdownEditorText.ImageAltPlaceholder => MarkdownL.EditorImageAltPlaceholder,
            MarkdownEditorText.CodeFencePlaceholder => MarkdownL.EditorCodeFencePlaceholder,
            MarkdownEditorText.MathPlaceholder => MarkdownL.EditorMathPlaceholder,
            MarkdownEditorText.TableColumn => MarkdownL.EditorTableColumn,
            MarkdownEditorText.TableValue => MarkdownL.EditorTableValue,
            MarkdownEditorText.TableItem => MarkdownL.EditorTableItem,
            MarkdownEditorText.TableDescription => MarkdownL.EditorTableDescription,
            MarkdownEditorText.EnterSearchTextFirst => MarkdownL.EditorEnterSearchTextFirst,
            MarkdownEditorText.SearchNoMatchFormat => MarkdownL.EditorSearchNoMatchFormat,
            MarkdownEditorText.SearchFoundOnLineFormat => MarkdownL.EditorSearchFoundOnLineFormat,
            MarkdownEditorText.SearchFoundWrappedOnLineFormat => MarkdownL.EditorSearchFoundWrappedOnLineFormat,
            MarkdownEditorText.SearchReplacedNextFormat => MarkdownL.EditorSearchReplacedNextFormat,
            MarkdownEditorText.SearchReplacedAllFormat => MarkdownL.EditorSearchReplacedAllFormat,
            MarkdownEditorText.SearchMatchCountFormat => MarkdownL.EditorSearchMatchCountFormat,
            MarkdownEditorText.SearchInvalidRegexFormat => MarkdownL.EditorSearchInvalidRegexFormat,
            _ => null
        };
        return key is null ? string.Empty : MarkdownLocalization.GetResource(key);
    }

    public string Format(MarkdownEditorText text, params object?[] args)
    {
        var template = Get(text);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
