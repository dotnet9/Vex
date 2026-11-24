using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class MarkdownTextRopeTests
{
    [Fact]
    public void Replace_AcrossLeafBoundaries_MatchesStringModel()
    {
        var rope = MarkdownTextRope.FromString(new string('a', 4096) + "\n中间\n" + new string('b', 4096));
        var expected = rope.ToString();
        var random = new Random(1729);

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var start = random.Next(expected.Length + 1);
            var length = random.Next(expected.Length - start + 1);
            var replacement = iteration % 17 == 0
                ? new string((char)('a' + iteration % 26), random.Next(0, 5000))
                : $"#{iteration}\n";
            rope = rope.Replace(start, length, replacement);
            expected = expected[..start] + replacement + expected[(start + length)..];

            Assert.Equal(expected.Length, rope.Length);
            Assert.Equal(1 + expected.AsSpan().Count('\n'), rope.LineCount);
            Assert.Equal(expected, rope.ToString());
            Assert.True(rope.ContentEquals(expected));
            Assert.False(rope.ContentEquals(expected + "x"));

            var substringStart = random.Next(expected.Length + 1);
            var substringLength = random.Next(expected.Length - substringStart + 1);
            Assert.Equal(expected.Substring(substringStart, substringLength),
                rope.Substring(substringStart, substringLength));

            var offset = random.Next(expected.Length + 1);
            var prefix = expected[..offset];
            var line = 1 + prefix.AsSpan().Count('\n');
            var lastNewLine = prefix.LastIndexOf('\n');
            Assert.Equal((line, offset - lastNewLine), rope.GetLineColumn(offset));
        }
    }

    [Fact]
    public void Replace_DeleteWholeDocument_RestoresEmptyRope()
    {
        var rope = MarkdownTextRope.FromString("第一行\n第二行").Replace(0, 7, string.Empty);

        Assert.Equal(0, rope.Length);
        Assert.Equal(1, rope.LineCount);
        Assert.Equal(string.Empty, rope.ToString());
        Assert.Equal(string.Empty, rope.Substring(0, 10));
        Assert.Equal((1, 1), rope.GetLineColumn(100));

        rope = rope.Replace(0, 0, "重新开始");
        Assert.Equal("重新开始", rope.ToString());
    }

    [Fact]
    public void LazySnapshot_RetainsCapturedRopeVersionWithoutMaterializingCurrentText()
    {
        var models = MarkdownBlockParser.Parse("第一段\n\n第二段\n\n第三段");
        var buffer = new MarkdownLiveTextBuffer(models);
        buffer.Reset();

        models[1].Text = "第二段修改";
        Assert.True(buffer.Splice(1));
        Assert.False(buffer.IsTextMaterialized);

        var (materializer, length) = buffer.CaptureText();
        var snapshot = new MarkdownLiveEditorSnapshot(materializer, length, 1, 2, 4);
        Assert.False(buffer.IsTextMaterialized);
        Assert.Equal("第一段\n\n第二段修改\n\n第三段".Length, snapshot.TextLength);

        models[2].Text = "第三段也修改";
        Assert.True(buffer.Splice(2));
        Assert.Equal("第一段\n\n第二段修改\n\n第三段", snapshot.Text);
        Assert.Equal(snapshot.Text, materializer());
        Assert.False(buffer.IsTextMaterialized);

        Assert.Equal("第一段\n\n第二段修改\n\n第三段也修改", buffer.Text);
        Assert.True(buffer.IsTextMaterialized);
    }

    [Fact]
    public void Reset_EquivalentText_DoesNotAdvanceTextVersion()
    {
        var models = MarkdownBlockParser.Parse("第一段\n\n第二段");
        var buffer = new MarkdownLiveTextBuffer(models);
        buffer.Reset();
        var version = buffer.Version;

        buffer.Reset();
        Assert.Equal(version, buffer.Version);

        models[1].Text = "第二段修改";
        buffer.Reset();
        Assert.Equal(version + 1, buffer.Version);
    }

    [Fact]
    public void RepeatedLongDocumentEdits_KeepExactTextAndGroupOffsets()
    {
        var first = new string('甲', 9000);
        var markdown = $"# 标题\n\n{first}\n\n中段\n\n尾段";
        var models = MarkdownBlockParser.Parse(markdown);
        var buffer = new MarkdownLiveTextBuffer(models);
        buffer.Reset();
        var expected = markdown;
        var random = new Random(2718);

        for (var iteration = 0; iteration < 80; iteration++)
        {
            var index = 1 + random.Next(models.Count - 1);
            var value = $"{new string('乙', iteration % 13)}\n第{iteration}次编辑";
            models[index].Text = value;
            expected = MarkdownBlockParser.Write(models);
            Assert.True(buffer.Splice(index));
            Assert.False(buffer.IsTextMaterialized);
            Assert.Equal(expected.Length, buffer.TextLength);
            Assert.Equal(1 + expected.AsSpan().Count('\n'), buffer.LineCount);

            var offset = 0;
            for (var modelIndex = 0; modelIndex < models.Count; modelIndex++)
            {
                var bounds = buffer.GetGroupBounds(modelIndex);
                Assert.NotNull(bounds);
                var group = bounds!.Value;
                if (group.Start < offset) continue;
                Assert.Equal(offset, group.Start);
                offset += group.OldLength;
            }
            Assert.Equal(expected.Length, offset);
        }

        Assert.Equal(expected, buffer.Text);
        Assert.Equal(expected.Length, buffer.TextLength);
        Assert.Equal(1 + expected.AsSpan().Count('\n'), buffer.LineCount);
    }
}
