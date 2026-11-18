using CodeWF.Avalonia.Markdown.Shared.Rendering;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public class MarkdownSpanOperationsTests
{
    private const string Sentence = "普通的文字一段";

    [Fact]
    public void Wrap_wraps_selection_with_property()
    {
        var (text, start, end) = MarkdownSpanOperations.Wrap(Sentence, 0, 3, "color", "#D52515");
        Assert.Equal("<span style=\"color:#D52515\">普通的</span>文字一段", text);
        Assert.Equal("<span style=\"color:#D52515\">".Length, start);
        Assert.Equal(start + "普通的".Length, end);
    }

    [Fact]
    public void Wrap_merges_into_adjacent_span()
    {
        var wrapped = "<span style=\"color:#D52515\">普通</span>的文字一段";
        var openTag = "<span style=\"color:#D52515\">";
        var (text, start, _) = MarkdownSpanOperations.Wrap(wrapped, openTag.Length, openTag.Length + 2, "font-size", "18px");
        Assert.Equal("<span style=\"color:#D52515;font-size:18px\">普通</span>的文字一段", text);
        Assert.Equal("<span style=\"color:#D52515;font-size:18px\">".Length, start);
    }

    [Fact]
    public void Wrap_toggles_off_same_value()
    {
        var wrapped = "<span style=\"color:#D52515\">普通</span>的文字一段";
        var openTag = "<span style=\"color:#D52515\">";
        var (text, start, end) = MarkdownSpanOperations.Wrap(wrapped, openTag.Length, openTag.Length + 2, "color", "#D52515");
        Assert.Equal("普通的文字一段", text);
        Assert.Equal(0, start);
        Assert.Equal(2, end);
    }

    [Fact]
    public void Wrap_without_toggle_keeps_same_value()
    {
        var wrapped = "<span style=\"color:#D52515\">普通</span>的文字一段";
        var openTag = "<span style=\"color:#D52515\">";
        var (text, _, _) = MarkdownSpanOperations.Wrap(wrapped, openTag.Length, openTag.Length + 2, "color", "#d52515", toggle: false);
        Assert.Equal(wrapped, text);
    }

    [Fact]
    public void Wrap_ignores_earlier_spans_in_window()
    {
        // 200 字符窗口内更早的 span 不得干扰：只有紧贴选区的开标签参与合并。
        var earlier = "<span style=\"color:#0064FA\">一</span>" + new string('前', 40);
        var text = earlier + "选中";
        var (updated, _, _) = MarkdownSpanOperations.Wrap(text, text.Length - 2, text.Length, "color", "#D52515");
        Assert.EndsWith("<span style=\"color:#D52515\">选中</span>", updated);
        Assert.StartsWith("<span style=\"color:#0064FA\">一</span>", updated);
    }

    [Fact]
    public void Clear_strips_all_span_tags_in_range()
    {
        var text = "前<span style=\"color:#D52515\">中</span>后<span style=\"font-size:18px\">尾</span>";
        var (updated, start, end) = MarkdownSpanOperations.Clear(text, 0, text.Length);
        Assert.Equal("前中后尾", updated);
        Assert.Equal(0, start); // 原起点在首个标签之前，位置不动
        Assert.Equal(end, updated.Length);
    }

    [Fact]
    public void CurrentValue_reads_adjacent_span_style()
    {
        var wrapped = "<span style=\"color:#D52515;font-size:18px\">普通</span>的文字一段";
        var openTag = "<span style=\"color:#D52515;font-size:18px\">";
        Assert.Equal("#D52515", MarkdownSpanOperations.CurrentValue(wrapped, openTag.Length, openTag.Length + 2, "color"));
        Assert.Equal("18px", MarkdownSpanOperations.CurrentValue(wrapped, openTag.Length, openTag.Length + 2, "font-size"));
        Assert.Null(MarkdownSpanOperations.CurrentValue(wrapped, openTag.Length, openTag.Length + 2, "background-color"));
        Assert.Null(MarkdownSpanOperations.CurrentValue("普通", 0, 2, "color"));
    }

    [Fact]
    public void Wrap_rejects_unknown_property()
    {
        var (text, start, end) = MarkdownSpanOperations.Wrap(Sentence, 0, 2, "behavior", "url(x)");
        Assert.Equal(Sentence, text);
        Assert.Equal(0, start);
        Assert.Equal(2, end);
    }
}
