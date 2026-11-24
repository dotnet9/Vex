using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class MarkdownCodeSerializationTests
{
    [Theory]
    [InlineData("- 父项\n  ```csharp\n  old\n  ```\n", "  ```csharp\n  new\n  ```")]
    [InlineData("1. 父项\n   ```csharp\n   old\n   ```\n", "   ```csharp\n   new\n   ```")]
    [InlineData("> ```csharp\n> old\n> ```\n", "> ```csharp\n> new\n> ```")]
    [InlineData("> - 父项\n>   ```csharp\n>   old\n>   ```\n", ">   ```csharp\n>   new\n>   ```")]
    public void EditCode_PreservesContainerPrefix(string source, string expected)
    {
        var models = MarkdownBlockParser.Parse(source);
        var code = Assert.Single(models, model => model.Kind == MarkdownBlockKind.Code);
        code.Text = "new";

        var written = MarkdownBlockParser.Write(models);

        Assert.Contains(expected, written);
        var reparsed = MarkdownBlockParser.Parse(written);
        Assert.Equal("new", Assert.Single(reparsed, model => model.Kind == MarkdownBlockKind.Code).Text);
    }

    [Fact]
    public void EditCode_PreservesLongFenceAndEmbeddedTripleBackticks()
    {
        const string source = "````csharp\nbefore\n```\nafter\n````\n";
        var models = MarkdownBlockParser.Parse(source);
        var code = Assert.Single(models);
        code.Text += "\nmore";

        var written = MarkdownBlockParser.Write(models);
        var reparsed = MarkdownBlockParser.Parse(written);

        Assert.Contains("````csharp\n", written);
        Assert.Equal(code.Text, Assert.Single(reparsed).Text);
    }

    [Fact]
    public void EditCode_PreservesTildeFence()
    {
        var models = MarkdownBlockParser.Parse("~~~text\nold\n~~~\n");
        models[0].Text = "new";

        var written = MarkdownBlockParser.Write(models);

        Assert.Contains("~~~text\nnew\n~~~", written);
    }

    [Fact]
    public void EditIndentedCode_DoesNotTurnIntoFencedBlock()
    {
        var models = MarkdownBlockParser.Parse("    old\n    code\n");
        var code = Assert.Single(models);
        Assert.False(code.IsFenced);
        code.Text = "new\ncontent";

        var written = MarkdownBlockParser.Write(models);
        var reparsed = MarkdownBlockParser.Parse(written);

        Assert.Contains("    new\n    content", written);
        Assert.DoesNotContain("```", written);
        Assert.False(Assert.Single(reparsed).IsFenced);
        Assert.Equal(code.Text, Assert.Single(reparsed).Text);
    }

    [Fact]
    public void EditQuotedIndentedCode_PreservesQuoteAndIndent()
    {
        var models = MarkdownBlockParser.Parse(">     old\n>     code\n");
        var code = Assert.Single(models);
        code.Text = "new";

        var written = MarkdownBlockParser.Write(models);

        Assert.Contains(">     new", written);
        Assert.DoesNotContain("```", written);
    }
}
