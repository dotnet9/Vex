using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Markup.Xaml.MarkupExtensions;
using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 无高亮器时的长代码分块正文：超过阈值按 <see cref="MarkdownCodeChunking.LinesPerChunk"/>
/// 行切块，流式追加只重建尾部变化的块，前面的块保留控件实例与排版结果。
/// </summary>
internal sealed class PlainCodeChunkBody : StackPanel, IMarkdownCodeBodyUpdate
{
    private readonly IMarkdownRenderContext _context;
    private readonly List<SelectableTextBlock> _chunks = [];
    private string[] _lines;

    public PlainCodeChunkBody(IMarkdownRenderContext context, string code)
    {
        Orientation = global::Avalonia.Layout.Orientation.Vertical;
        _context = context;
        _lines = SplitLines(code);
        for (var chunk = 0; chunk < ChunkCount(_lines.Length); chunk++)
        {
            _chunks.Add(CreateChunkBlock());
        }

        for (var chunk = 0; chunk < _chunks.Count; chunk++)
        {
            FillChunk(chunk, _lines);
        }
    }

    public bool TryUpdate(string code)
    {
        var lines = SplitLines(code);
        if (lines.Length <= MarkdownCodeChunking.ThresholdLines)
        {
            return false;
        }

        var firstChanged = 0;
        var shared = Math.Min(_lines.Length, lines.Length);
        while (firstChanged < shared && _lines[firstChanged] == lines[firstChanged])
        {
            firstChanged++;
        }

        var chunkCount = ChunkCount(lines.Length);
        for (var chunk = firstChanged / MarkdownCodeChunking.LinesPerChunk; chunk < chunkCount; chunk++)
        {
            if (chunk >= _chunks.Count)
            {
                _chunks.Add(CreateChunkBlock());
            }

            FillChunk(chunk, lines);
        }

        while (_chunks.Count > chunkCount)
        {
            Children.Remove(_chunks[^1]);
            _chunks.RemoveAt(_chunks.Count - 1);
        }

        _lines = lines;
        return true;
    }

    internal static string[] SplitLines(string code) =>
        code.TrimEnd().Replace("\r\n", "\n").Split('\n');

    private static int ChunkCount(int lineCount) =>
        (lineCount + MarkdownCodeChunking.LinesPerChunk - 1) / MarkdownCodeChunking.LinesPerChunk;

    private SelectableTextBlock CreateChunkBlock()
    {
        var textBlock = _context.CreateSelectableText("MdCodeBlockPlain");
        textBlock.FontFamily = _context.CodeFontFamily;
        textBlock.FontSize = _context.CodeBlockFontSize;
        textBlock.LineHeight = _context.CodeBlockLineHeight;
        textBlock.TextWrapping = TextWrapping.NoWrap;
        textBlock[!SelectableTextBlock.ForegroundProperty] =
            new DynamicResourceExtension(MarkdownStyleKeys.TextBrushResource);
        Children.Add(textBlock);
        return textBlock;
    }

    private void FillChunk(int chunk, string[] lines)
    {
        var start = chunk * MarkdownCodeChunking.LinesPerChunk;
        var end = Math.Min(start + MarkdownCodeChunking.LinesPerChunk, lines.Length);
        var inlines = new InlineCollection();
        for (var line = start; line < end; line++)
        {
            if (line > start)
            {
                inlines.Add(new LineBreak());
            }

            inlines.Add(new Run(lines[line]));
        }

        _chunks[chunk].Inlines = inlines;
    }
}
