using AvaloniaEdit;

using CodeWF.Avalonia.Markdown.Editor.Services;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownFormattingCleanerTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void Clear_RemovesSemanticInlineFormatting()
    {
        const string markdown = "**粗 *斜*** ~~删~~ `a_b*c` [链接](https://example.com/a_(b)) "
            + "![图](image.png) <span style=\"color:red\">红</span>";

        Assert.Equal("粗 斜 删 a_b*c 链接 图 红", MarkdownFormattingCleaner.Clear(markdown));
    }

    [Fact]
    public void Clear_PreservesLiteralPunctuationAndEscapedMarkers()
    {
        const string markdown = "snake_case 2 * 3 \\*literal\\* \\_under\\_ \\[bracket\\]";

        Assert.Equal("snake_case 2 * 3 *literal* _under_ [bracket]", MarkdownFormattingCleaner.Clear(markdown));
    }

    [Fact]
    public void Clear_RemovesBlockMarkersWithoutFlatteningCode()
    {
        const string markdown = "# 标题\n\n- 一\n- 二\n\n> 引用\n\n"
            + "| A | B |\n| - | - |\n| 1 | 2 |\n\n"
            + "```csharp\nvar value = a_b * c;\n```\n";

        Assert.Equal("标题\n一\n二\n引用\nA | B\n1 | 2\nvar value = a_b * c;\n",
            MarkdownFormattingCleaner.Clear(markdown));
    }

    [Fact]
    public void Clear_RemovesHtmlTagsButKeepsVisibleTextAndBreaks()
    {
        const string markdown = "<span>Hello <em>world</em><br>next</span>";

        Assert.Equal("Hello world\nnext", MarkdownFormattingCleaner.Clear(markdown));
    }

    [Fact]
    public void Clear_PreservesUnmatchedMarkdownMarkers()
    {
        const string markdown = "literal _ marker and * marker and ` marker";

        Assert.Equal(markdown, MarkdownFormattingCleaner.Clear(markdown));
    }

    [Fact]
    public void ClearFormatting_OnlyChangesSelectionAndSelectsPlainText()
    {
        platform.Run(() =>
        {
            var editor = new TextEditor { Text = "before **bold** after" };
            editor.SelectionStart = 7;
            editor.SelectionLength = 8;

            new MarkdownEditorMutationService().ClearFormatting(editor);

            Assert.Equal("before bold after", editor.Text);
            Assert.Equal(7, editor.SelectionStart);
            Assert.Equal(4, editor.SelectionLength);
            Assert.Equal(11, editor.CaretOffset);
        });
    }

    [Fact]
    public void ClearFormatting_WithCaretOnly_ClearsCurrentLine()
    {
        platform.Run(() =>
        {
            var editor = new TextEditor { Text = "first\n## heading\nlast" };
            editor.CaretOffset = 9;

            new MarkdownEditorMutationService().ClearFormatting(editor);

            Assert.Equal("first\nheading\nlast", editor.Text);
            Assert.Equal(6, editor.SelectionStart);
            Assert.Equal(7, editor.SelectionLength);
        });
    }
}
