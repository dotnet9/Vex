using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using CodeWF.Avalonia.Markdown.Core.Blocks;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public class MarkdownSpanRoundTripTests
{
    [Fact]
    public void Parse_then_Write_preserves_untouched_block_with_spans()
    {
        const string markdown = "# 标题\n\n带<span style=\"color:#D52515\">颜色</span>的段落。\n";
        var blocks = MarkdownBlockParser.Parse(markdown);
        Assert.Equal(markdown, MarkdownBlockParser.Write(blocks));
    }

    [Fact]
    public void Paragraph_edit_rewrites_canonical_text_with_spans_via_source_slice()
    {
        const string markdown = "带<span style=\"color:#D52515\">颜色</span>的段落。";
        var blocks = MarkdownBlockParser.Parse(markdown);
        var paragraph = blocks.Single(block => block.Kind == MarkdownBlockKind.Paragraph);
        // 段落 Text 是源文切片（含标签原文）——编辑其他部分时本块按 canonical 重写不丢标签。
        Assert.Equal(markdown, paragraph.Text);
        Assert.Contains("<span style=\"color:#D52515\">颜色</span>", MarkdownBlockParser.Write(blocks));
    }

    [Fact]
    public void AppendInline_keeps_html_in_table_cells()
    {
        const string markdown = "| 列一 | 列二 |\n| --- | --- |\n| <span style=\"color:#D52515\">红</span> | 内容 |";
        var blocks = MarkdownBlockParser.Parse(markdown);
        var table = blocks.Single(block => block.Kind == MarkdownBlockKind.Table);
        Assert.Equal("<span style=\"color:#D52515\">红</span>", table.Cells[1][0]);
    }
}
