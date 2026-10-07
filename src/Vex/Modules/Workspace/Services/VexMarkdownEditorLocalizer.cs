using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// Vex 的 i18n 适配层：把库编辑器的文案键映射到 <see cref="VexL"/> 资源键。
/// 库内不绑定任何 i18n 框架，切换语言仍由 Vex 的本地化实现负责。
/// </summary>
public sealed class VexMarkdownEditorLocalizer : CodeWF.Markdown.Editor.Services.IMarkdownEditorLocalizer
{
    private readonly IAppLocalizer _localizer;

    public VexMarkdownEditorLocalizer(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public string Get(CodeWF.Markdown.Editor.Services.MarkdownEditorText text) => _localizer.Get(GetKey(text));

    public string Format(CodeWF.Markdown.Editor.Services.MarkdownEditorText text, params object?[] args) =>
        _localizer.Format(GetKey(text), args);

    private static string GetKey(CodeWF.Markdown.Editor.Services.MarkdownEditorText text) => text switch
    {
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.BoldPlaceholder => VexL.EditorTemplateBoldText,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.ItalicPlaceholder => VexL.EditorTemplateItalicText,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.InlineCodePlaceholder => VexL.EditorTemplateInlineCode,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.LinkPlaceholder => VexL.EditorTemplateLinkText,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.ImageAltPlaceholder => VexL.EditorTemplateImageAltText,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.CodeFencePlaceholder => VexL.EditorTemplateCodeFence,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.MathPlaceholder => VexL.EditorTemplateMath,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.TableColumn => VexL.EditorTemplateTableColumn,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.TableValue => VexL.EditorTemplateTableValue,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.TableItem => VexL.EditorTemplateTableItem,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.TableDescription => VexL.EditorTemplateTableDescription,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.EnterSearchTextFirst => VexL.StatusEnterSearchTextFirst,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.SearchNoMatchFormat => VexL.EditorSearchNoMatchFormat,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.SearchFoundOnLineFormat => VexL.EditorSearchFoundOnLineFormat,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.SearchFoundWrappedOnLineFormat => VexL.EditorSearchFoundWrappedOnLineFormat,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.SearchReplacedNextFormat => VexL.EditorSearchReplacedNextFormat,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.SearchReplacedAllFormat => VexL.EditorSearchReplacedAllFormat,
        CodeWF.Markdown.Editor.Services.MarkdownEditorText.SearchMatchCountFormat => VexL.EditorSearchMatchCountFormat,
        _ => VexL.EditorSearchInvalidRegexFormat
    };
}
