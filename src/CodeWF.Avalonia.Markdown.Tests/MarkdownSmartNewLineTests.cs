using CodeWF.Avalonia.Markdown.Editor.Services;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class MarkdownSmartNewLineTests
{
    [Theory]
    [InlineData("> - 列表项", "\n> - ")]
    [InlineData("> > 3. 第三项", "\n> > 4. ")]
    [InlineData("> - [x] 完成", "\n> - [ ] ")]
    [InlineData("  - 嵌套项", "\n  - ")]
    [InlineData("10) 第十项", "\n11) ")]
    [InlineData("> 引用", "\n> ")]
    [InlineData("普通文本", "\n")]
    public void CreateChange_ComposesQuoteAndListPrefixes(string line, string expected)
    {
        var change = MarkdownSmartNewLine.CreateChange(line, line.Length, 0);

        Assert.Equal(line.Length, change.Start);
        Assert.Equal(0, change.Length);
        Assert.Equal(expected, change.Text);
    }

    [Theory]
    [InlineData("- ", "\n")]
    [InlineData("> - ", ">\n")]
    [InlineData("> > - [x] ", "> >\n")]
    [InlineData("> > ", ">\n")]
    public void CreateChange_EmptyPrefixExitsInnermostStructure(string line, string expected)
    {
        var change = MarkdownSmartNewLine.CreateChange(line, line.Length, 0);

        Assert.Equal(0, change.Start);
        Assert.Equal(line.Length, change.Length);
        Assert.Equal(expected, change.Text);
    }

    [Fact]
    public void CreateChange_WhitespaceOnlyLine_KeepsIndentationAfterNewLine()
    {
        const string text = "   ";

        var change = MarkdownSmartNewLine.CreateChange(text, text.Length, 0);

        Assert.Equal(text.Length, change.Start);
        Assert.Equal(0, change.Length);
        Assert.Equal("\n   ", change.Text);
    }

    [Fact]
    public void CreateChange_ReplacesSelectionAfterContinuationPrefix()
    {
        const string text = "> - 旧内容";

        var change = MarkdownSmartNewLine.CreateChange(text, 5, 2);

        Assert.Equal(5, change.Start);
        Assert.Equal(2, change.Length);
        Assert.Equal("\n> - ", change.Text);
    }
}
