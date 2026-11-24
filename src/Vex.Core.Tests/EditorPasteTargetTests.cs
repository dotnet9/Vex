using Vex.Modules.Workspace.Services;

using Xunit;

namespace Vex.Core.Tests;

public sealed class EditorPasteTargetTests
{
    [Fact]
    public void TryReplace_WhenTargetUnchanged_ReplacesCapturedSelection()
    {
        var target = new EditorPasteTarget("note.md", "hello world", 6, 5, false);

        var changed = target.TryReplace("note.md", "hello world", 6, 5, false, "Vex");

        Assert.NotNull(changed);
        Assert.Equal("hello Vex", changed);
    }

    [Fact]
    public void TryReplace_WhenSelectionUnchanged_InsertsAtCaret()
    {
        var target = new EditorPasteTarget(null, "alpha", 2, 0, true);

        var changed = target.TryReplace(null, "alpha", 2, 0, true, "beta");

        Assert.NotNull(changed);
        Assert.Equal("albetapha", changed);
    }

    [Theory]
    [InlineData("other.md", "hello world", 6, 5, false)]
    [InlineData("note.md", "hello brave world", 6, 5, false)]
    [InlineData("note.md", "hello world", 5, 5, false)]
    [InlineData("note.md", "hello world", 6, 4, false)]
    [InlineData("note.md", "hello world", 6, 5, true)]
    public void TryReplace_WhenTargetChanged_Aborts(string? path, string markdown, int start, int length, bool instant)
    {
        var target = new EditorPasteTarget("note.md", "hello world", 6, 5, false);

        Assert.Null(target.TryReplace(path, markdown, start, length, instant, "Vex"));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(12, 0)]
    [InlineData(6, 6)]
    public void TryReplace_WhenRangeInvalid_Aborts(int start, int length)
    {
        var target = new EditorPasteTarget("note.md", "hello world", start, length, false);

        Assert.Null(target.TryReplace("note.md", "hello world", start, length, false, "Vex"));
    }
}
