using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 代码围栏信息串解析结果：<c>```cs src/App.cs#L10-20</c> 拆出语言、路径与行段。
/// 只用于标题展示与下载文件名，未知部分原样忽略。
/// </summary>
internal sealed record MarkdownCodeFenceInfo(string? Language, string? Path, int? StartLine, int? EndLine)
{
    /// <summary>
    /// Markdig 把围栏信息拆成 <see cref="FencedCodeBlock.Info" />（首词）与
    /// <see cref="FencedCodeBlock.Arguments" />（其余），标题需要两者拼回整串。
    /// </summary>
    public static MarkdownCodeFenceInfo From(CodeBlock codeBlock)
    {
        if (codeBlock is not FencedCodeBlock fenced)
        {
            return new MarkdownCodeFenceInfo(null, null, null, null);
        }

        var info = string.IsNullOrWhiteSpace(fenced.Arguments)
            ? fenced.Info
            : string.IsNullOrWhiteSpace(fenced.Info)
                ? fenced.Arguments
                : $"{fenced.Info} {fenced.Arguments}";
        return Parse(info);
    }

    public static MarkdownCodeFenceInfo Parse(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return new MarkdownCodeFenceInfo(null, null, null, null);
        }

        var parts = info.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var language = parts.Length > 0 ? parts[0] : null;
        string? path = null;
        int? start = null, end = null;
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Contains('=') || part.StartsWith('{'))
            {
                continue;
            }

            var hash = part.IndexOf("#L", StringComparison.Ordinal);
            if (hash >= 0)
            {
                var range = part[(hash + 2)..].Split('-', 'L');
                if (int.TryParse(range[0], out var lineStart))
                {
                    start = lineStart;
                    end = range.Length > 1 && int.TryParse(range[^1], out var lineEnd) ? lineEnd : lineStart;
                }

                part = part[..hash];
            }

            if ((part.Contains('/') || part.Contains('.')) && path is null)
            {
                path = part;
            }
        }

        return new MarkdownCodeFenceInfo(language, path, start, end);
    }

    /// <summary>标题文本：语言，信息串带路径时追加 “ · 路径#L起-止”。</summary>
    public string ToTitle(string? fallbackLanguage = null)
    {
        var title = string.IsNullOrWhiteSpace(Language) ? fallbackLanguage ?? "text" : Language;
        if (Path is { } path)
        {
            title += " · " + path;
            if (StartLine is { } startLine)
            {
                title += $"#L{startLine}";
                if (EndLine is { } endLine && endLine != startLine)
                {
                    title += $"-{endLine}";
                }
            }
        }

        return title;
    }
}
