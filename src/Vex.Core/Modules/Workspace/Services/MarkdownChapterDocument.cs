using Markdig;
using Markdig.Syntax;

namespace Vex.Modules.Workspace.Services;

/// <summary>章节使用 AST 的实际源码范围；正文、围栏代码、换行和子章节随整章移动。</summary>
public sealed class MarkdownChapterDocument
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();
    public sealed record Chapter(int Start, int HeaderEnd, int BodyEnd, int End, int Level, int Line, string Title, int? Parent);
    public string Text { get; }
    public string NewLine { get; }
    public IReadOnlyList<Chapter> Chapters { get; }
    public MarkdownChapterDocument(string text)
    {
        Text = text; NewLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var headings = Markdown.Parse(text, Pipeline).OfType<HeadingBlock>().ToArray();
        var chapters = new List<Chapter>();
        var ancestors = new Stack<Chapter>();
        for (var i = 0; i < headings.Length; i++)
        {
            var h = headings[i];
            var headerEnd = h.Span.End + 1;
            while (headerEnd < text.Length && text[headerEnd] != '\n') headerEnd++;
            if (headerEnd < text.Length) headerEnd++;
            var end = headings.Skip(i + 1).FirstOrDefault(next => next.Level <= h.Level)?.Span.Start ?? text.Length;
            while (ancestors.TryPeek(out var parent) && parent.Level >= h.Level) ancestors.Pop();
            var chapter = new Chapter(h.Span.Start, headerEnd, i + 1 < headings.Length ? headings[i + 1].Span.Start : text.Length,
                end, h.Level, h.Line + 1, HeaderTitle(text[h.Span.Start..headerEnd]), ancestors.TryPeek(out var p) ? p.Start : null);
            chapters.Add(chapter); ancestors.Push(chapter);
        }
        Chapters = chapters;
    }
    public Chapter? At(int start) => Chapters.FirstOrDefault(chapter => chapter.Start == start);
    public string Body(Chapter chapter) => Text[chapter.HeaderEnd..chapter.BodyEnd].Trim('\r', '\n');
    public string Rename(Chapter chapter, string title) => Replace(chapter.Start, chapter.HeaderEnd,
        new string('#', chapter.Level) + " " + title.Replace('\r', ' ').Replace('\n', ' ').Trim() + NewLine);
    public string SetBody(Chapter chapter, string body) => Replace(chapter.HeaderEnd, chapter.BodyEnd,
        NewLine + body.Trim('\r', '\n') + NewLine + NewLine);
    public string Delete(Chapter chapter, bool keepContent) => Replace(chapter.Start, keepContent ? chapter.HeaderEnd : chapter.End, "");
    public (string Text, int Start) Add(Chapter? chapter, bool child, string title)
    {
        var level = chapter is null ? 1 : child ? chapter.Level + 1 : chapter.Level;
        if (level > 6) throw new InvalidOperationException("H6");
        var start = chapter is null ? Text.Length : child ? chapter.BodyEnd : chapter.End;
        var prefix = start == 0 || Text[..start].EndsWith(NewLine + NewLine, StringComparison.Ordinal) ? "" : NewLine;
        if (start > 0 && Text[start - 1] != '\n') prefix += NewLine;
        var insert = prefix + new string('#', level) + " " + title + NewLine + NewLine;
        return (Replace(start, start, insert), start + prefix.Length);
    }
    public IReadOnlyList<Chapter> Siblings(Chapter chapter) => Chapters.Where(item => item.Parent == chapter.Parent).ToArray();
    public (string Text, int Start) Move(Chapter chapter, Chapter? target, string position)
    {
        if (target is not null && target.Start >= chapter.Start && target.Start < chapter.End)
            return (Text, chapter.Start);
        var level = target is null ? 1 : position == "child" ? target.Level + 1 : target.Level;
        var delta = level - chapter.Level;
        var descendants = Chapters.Where(item => item.Start >= chapter.Start && item.Start < chapter.End).ToArray();
        if (descendants.Any(item => item.Level + delta is < 1 or > 6)) throw new InvalidOperationException("H6");
        var moved = Text[chapter.Start..chapter.End];
        if (delta != 0)
            foreach (var item in descendants.Reverse())
            {
                var header = Text[item.Start..item.HeaderEnd];
                var title = HeaderTitle(header);
                moved = moved.Remove(item.Start - chapter.Start, item.HeaderEnd - item.Start)
                    .Insert(item.Start - chapter.Start, new string('#', item.Level + delta) + " " + title + NewLine);
            }
        var insertion = target is null ? Text.Length : position == "before" ? target.Start : target.End;
        if (insertion >= chapter.End) insertion -= chapter.End - chapter.Start;
        var remaining = Replace(chapter.Start, chapter.End, "");
        var prefix = insertion > 0 && remaining[insertion - 1] != '\n' ? NewLine + NewLine : "";
        if (!moved.EndsWith(NewLine, StringComparison.Ordinal)) moved += NewLine;
        return (remaining.Insert(insertion, prefix + moved), insertion + prefix.Length);
    }
    public static string HeaderTitle(string header)
    {
        var first = header.Split('\n')[0].Trim();
        if (first.StartsWith('#'))
        {
            var title = first.TrimStart('#').TrimStart();
            var last = title.Length;
            while (last > 0 && title[last - 1] == '#') last--;
            if (last > 0 && last < title.Length && char.IsWhiteSpace(title[last - 1])) title = title[..last].TrimEnd();
            return title;
        }
        return first;
    }
    private string Replace(int start, int end, string replacement) => Text[..start] + replacement + Text[end..];
}
