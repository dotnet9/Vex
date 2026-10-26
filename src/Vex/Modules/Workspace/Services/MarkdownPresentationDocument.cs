using Avalonia;
using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Vex.Modules.Workspace.Services;

/// <summary>只保存章节范围，翻页时才截取正文；演示不改写当前文档。</summary>
public sealed class MarkdownPresentationDocument
{
    public sealed record Section(int Index, string Title, int Level, int Start, int End)
    {
        /// <summary>原型 nav button：左内距 12 + 每降一级再进 10，这里只记增量，基础 12 由行样式给。</summary>
        public Thickness Indent => new(Math.Max(0, Level - 1) * 10, 0, 0, 0);

        /// <summary>原型按钮文本是「页码␣␣标题」。</summary>
        public string NavLabel => $"{Index + 1}  {Title}";
    }

    private readonly string _source;
    private readonly (int Start, int End)[] _definitions;
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();
    public IReadOnlyList<Section> Sections { get; }

    public MarkdownPresentationDocument(string source, string fallbackTitle)
    {
        _source = source;
        var document = Markdown.Parse(source, Pipeline);
        var headings = document.OfType<HeadingBlock>().ToArray();
        var contentStart = document.FirstOrDefault() is YamlFrontMatterBlock yaml ? yaml.Span.End + 1 : 0;
        _definitions = document.Descendants().Where(block => block is LinkReferenceDefinition or Footnote)
            .Where(block => block.Span.Start >= 0 && block.Span.End >= block.Span.Start)
            .Select(block =>
            {
                // Footnote 的 Span 可从内容开始，合成引用节点也可能指向引用处；
                // 只保留实际定义行，并取回定义前缀及完整的续行。
                var start = block.Span.Start == 0 ? 0 : source.LastIndexOf('\n', block.Span.Start - 1) + 1;
                var end = source.IndexOf('\n', Math.Min(source.Length, block.Span.End + 1));
                return (Start: start, End: end < 0 ? source.Length : end + 1);
            })
            .Where(span => System.Text.RegularExpressions.Regex.IsMatch(source[span.Start..span.End], @"^ {0,3}\[[^\]\r\n]+\]:"))
            .Distinct().ToArray();
        var sections = new List<Section>();
        if (headings.Length == 0 || !string.IsNullOrWhiteSpace(source[contentStart..headings[0].Span.Start]))
            sections.Add(new(0, fallbackTitle, 1, contentStart, headings.FirstOrDefault()?.Span.Start ?? source.Length));
        for (var i = 0; i < headings.Length; i++)
        {
            var heading = headings[i];
            var header = source[heading.Span.Start..(heading.Span.End + 1)];
            sections.Add(new(sections.Count, Markdown.ToPlainText(MarkdownChapterDocument.HeaderTitle(header)).Trim(), heading.Level,
                heading.Span.Start, i + 1 < headings.Length ? headings[i + 1].Span.Start : source.Length));
        }
        Sections = sections;
    }

    public string MarkdownAt(int index)
    {
        var section = Sections[index];
        var markdown = _source[section.Start..section.End];
        foreach (var definition in _definitions)
            if (definition.Start < section.Start || definition.Start >= section.End)
                markdown += "\n\n" + _source[definition.Start..definition.End];
        return markdown;
    }
}
