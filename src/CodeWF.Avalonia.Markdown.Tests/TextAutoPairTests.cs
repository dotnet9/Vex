using CodeWF.Avalonia.Markdown.Editor;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class TextAutoPairTests
{
    [Theory]
    [InlineData('*')]
    [InlineData('_')]
    [InlineData('~')]
    public void Emphasis_SecondMarkerCreatesPlaceholderWithCaretInMiddle(char marker)
    {
        var first = TextAutoPair.HandleTextInput(string.Empty, 0, 0, marker.ToString());
        Assert.Null(first);

        var source = marker.ToString();
        var second = TextAutoPair.HandleTextInput(source, 1, 0, marker.ToString());

        Assert.NotNull(second);
        Assert.Equal(new TextAutoPairResult(0, 1, new string(marker, 4), 2), second);
    }

    [Theory]
    [InlineData('*')]
    [InlineData('_')]
    [InlineData('~')]
    public void Emphasis_SelectionIsWrappedWithDoubleMarker(char marker)
    {
        var markerText = marker.ToString();
        var pair = new string(marker, 2);

        var result = TextAutoPair.HandleTextInput("选中文本", 0, 4, markerText);

        Assert.Equal(new TextAutoPairResult(0, 4, pair + "选中文本" + pair, 8), result);
    }

    [Fact]
    public void Emphasis_TypingClosingMarkersSkipsExistingPair()
    {
        const string source = "**text**";

        var first = TextAutoPair.HandleTextInput(source, 6, 0, "*");
        var second = TextAutoPair.HandleTextInput(source, 7, 0, "*");

        Assert.Equal(new TextAutoPairResult(6, 0, string.Empty, 7), first);
        Assert.Equal(new TextAutoPairResult(7, 0, string.Empty, 8), second);
    }

    [Theory]
    [InlineData('*')]
    [InlineData('_')]
    [InlineData('~')]
    public void Backspace_EmptyEmphasisPlaceholderDeletesAllFourMarkers(char marker)
    {
        var source = new string(marker, 4);

        var result = TextAutoPair.HandleBackspace(source, 2, 0);

        Assert.Equal(new TextAutoPairResult(0, 4, string.Empty, 0), result);
    }

    [Fact]
    public void Backspace_SingleEmphasisMarkerUsesNormalDeletion()
    {
        Assert.Null(TextAutoPair.HandleBackspace("**", 1, 0));
    }

    [Fact]
    public void Brackets_KeepPairingAndSelectionWrapping()
    {
        Assert.Equal(new TextAutoPairResult(0, 0, "()", 1),
            TextAutoPair.HandleTextInput(string.Empty, 0, 0, "("));
        Assert.Equal(new TextAutoPairResult(0, 3, "(abc)", 5),
            TextAutoPair.HandleTextInput("abc", 0, 3, "("));
        Assert.Equal(new TextAutoPairResult(0, 2, string.Empty, 0),
            TextAutoPair.HandleBackspace("()", 1, 0));
    }

    [Fact]
    public void IndexedSource_OnlyRequiresCharactersAroundSelection()
    {
        var result = TextAutoPair.HandleTextInput(
            100, 50, 2, "*", index => index == 50 || index == 51 ? 'x' : throw new InvalidOperationException(),
            (start, length) => new string('x', length));

        Assert.Equal(new TextAutoPairResult(50, 2, "**xx**", 56), result);
    }
}
