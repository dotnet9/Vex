using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorTemplateService : IMarkdownEditorTemplateService
{
    private readonly IAppLocalizer _localizer;

    public MarkdownEditorTemplateService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public string BoldPlaceholder => Text(VexL.EditorTemplateBoldText);

    public string ItalicPlaceholder => Text(VexL.EditorTemplateItalicText);

    public string InlineCodePlaceholder => Text(VexL.EditorTemplateInlineCode);

    public string LinkPlaceholder => Text(VexL.EditorTemplateLinkText);

    public string LinkUrlPlaceholder => "https://example.com";

    public string ImageAltPlaceholder => Text(VexL.EditorTemplateImageAltText);

    public string ImageTargetPlaceholder => "image.png";

    public string ImageInsertion => $"![{ImageAltPlaceholder}]({ImageTargetPlaceholder})";

    public string CodeFencePlaceholder => Text(VexL.EditorTemplateCodeFence);

    public string TableInsertion
    {
        get
        {
            var column = Text(VexL.EditorTemplateTableColumn);
            var value = Text(VexL.EditorTemplateTableValue);
            var item = Text(VexL.EditorTemplateTableItem);
            var description = Text(VexL.EditorTemplateTableDescription);
            // 表格模板按当前语言即时读取，切换语言后无需重建动作服务。
            return $"\n| {column} | {value} |\n| --- | --- |\n| {item} | {description} |\n";
        }
    }

    public string MathPlaceholder => Text(VexL.EditorTemplateMath);

    private string Text(string key) => _localizer.Get(key);
}
