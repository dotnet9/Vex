using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public class MarkdownSpanInlineTests
{
    [Fact]
    public void Parse_extracts_span_styled_runs()
    {
        var runs = MarkdownTextRunParser.Parse("<span style=\"color:#D52515;font-size:18px\">红字</span>后文");
        Assert.True(runs.Count >= 2);
        Assert.Equal("红字", runs[0].Text);
        Assert.Equal("#D52515", runs[0].Foreground);
        Assert.Equal(18, runs[0].FontSize);
        Assert.Equal("后文", runs[^1].Text);
        Assert.Null(runs[^1].Foreground);
    }

    [Fact]
    public void Parse_nests_span_styles_with_inheritance()
    {
        var runs = MarkdownTextRunParser.Parse(
            "<span style=\"color:#0064FA;background-color:#ECF7EC\">外<span style=\"font-size:20px\">内</span></span>");
        Assert.Equal("外", runs[0].Text);
        Assert.Equal("#0064FA", runs[0].Foreground);
        Assert.Null(runs[0].FontSize);
        Assert.Equal("内", runs[1].Text);
        Assert.Equal("#0064FA", runs[1].Foreground); // 继承外层
        Assert.Equal(20, runs[1].FontSize);          // 内层覆盖
    }

    [Fact]
    public void Parse_maps_text_offsets_skipping_hidden_tags()
    {
        const string markdown = "<span style=\"color:#D52515\">红字</span>后文";
        // 可见偏移 2（"红字"之后）→ 源码偏移应落在 "</span>" 起点前（红字内容结束处）。
        var sourceOffset = MarkdownTextRunParser.GetSourceOffset(markdown, 2);
        Assert.Equal(markdown.IndexOf("</span>", StringComparison.Ordinal), sourceOffset);
        // 反向：源码偏移在开标签内部 → 可见偏移 0。
        Assert.Equal(0, MarkdownTextRunParser.GetTextOffset(markdown, 3));
    }

    [Fact]
    public void Parse_bold_inside_span_combines_flags_and_style()
    {
        var runs = MarkdownTextRunParser.Parse("<span style=\"color:#D52515\">**红粗**</span>");
        Assert.True(runs.Count >= 1);
        Assert.Equal("红粗", runs[0].Text);
        Assert.True(runs[0].Bold);
        Assert.Equal("#D52515", runs[0].Foreground);
    }
}
