namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>编辑器动作与搜索结果的本地化文案标识。</summary>
public enum MarkdownEditorText
{
    BoldPlaceholder,
    ItalicPlaceholder,
    InlineCodePlaceholder,
    LinkPlaceholder,
    ImageAltPlaceholder,
    CodeFencePlaceholder,
    MathPlaceholder,
    TableColumn,
    TableValue,
    TableItem,
    TableDescription,
    EnterSearchTextFirst,
    SearchNoMatchFormat,
    SearchFoundOnLineFormat,
    SearchFoundWrappedOnLineFormat,
    SearchReplacedNextFormat,
    SearchReplacedAllFormat,
    SearchMatchCountFormat,
    SearchInvalidRegexFormat
}

/// <summary>
/// 编辑器文案接口；默认实现使用库内嵌 JSON 并跟随全局语言，宿主无需另外提供映射。
/// </summary>
public interface IMarkdownEditorLocalizer
{
    /// <summary>取单条文案。</summary>
    string Get(MarkdownEditorText text);

    /// <summary>取带参数的格式化文案；<paramref name="args"/> 按顺序填入占位符。</summary>
    string Format(MarkdownEditorText text, params object?[] args);
}
