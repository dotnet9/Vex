namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>
/// 编辑器模板文案（占位符与表格骨架）。由 <see cref="IMarkdownEditorLocalizer"/> 提供，
/// 表格模板按调用时机即时读取，宿主切换语言后无需重建动作服务。
/// </summary>
public interface IMarkdownEditorTemplateService
{
    string BoldPlaceholder { get; }

    string ItalicPlaceholder { get; }

    string InlineCodePlaceholder { get; }

    string LinkPlaceholder { get; }

    string LinkUrlPlaceholder { get; }

    string ImageAltPlaceholder { get; }

    string ImageTargetPlaceholder { get; }

    string ImageInsertion { get; }

    string CodeFencePlaceholder { get; }

    string TableInsertion { get; }

    string MathPlaceholder { get; }
}

/// <summary>基于 <see cref="IMarkdownEditorLocalizer"/> 与 <see cref="MarkdownEditorOptions"/> 的默认模板实现。</summary>
public sealed class MarkdownEditorTemplateService : IMarkdownEditorTemplateService
{
    private readonly IMarkdownEditorLocalizer _localizer;
    private readonly MarkdownEditorOptions _options;

    public MarkdownEditorTemplateService(IMarkdownEditorLocalizer localizer, MarkdownEditorOptions options)
    {
        _localizer = localizer;
        _options = options;
    }

    public string BoldPlaceholder => _localizer.Get(MarkdownEditorText.BoldPlaceholder);

    public string ItalicPlaceholder => _localizer.Get(MarkdownEditorText.ItalicPlaceholder);

    public string InlineCodePlaceholder => _localizer.Get(MarkdownEditorText.InlineCodePlaceholder);

    public string LinkPlaceholder => _localizer.Get(MarkdownEditorText.LinkPlaceholder);

    public string LinkUrlPlaceholder => _options.LinkUrlPlaceholder;

    public string ImageAltPlaceholder => _localizer.Get(MarkdownEditorText.ImageAltPlaceholder);

    public string ImageTargetPlaceholder => _options.ImageTargetPlaceholder;

    public string ImageInsertion => $"![{ImageAltPlaceholder}]({ImageTargetPlaceholder})";

    public string CodeFencePlaceholder => _localizer.Get(MarkdownEditorText.CodeFencePlaceholder);

    public string TableInsertion
    {
        get
        {
            var column = _localizer.Get(MarkdownEditorText.TableColumn);
            var value = _localizer.Get(MarkdownEditorText.TableValue);
            var item = _localizer.Get(MarkdownEditorText.TableItem);
            var description = _localizer.Get(MarkdownEditorText.TableDescription);
            return $"\n| {column} | {value} |\n| --- | --- |\n| {item} | {description} |\n";
        }
    }

    public string MathPlaceholder => _localizer.Get(MarkdownEditorText.MathPlaceholder);
}
