using Vex.Modules.Workspace.Services;

using Xunit;

namespace Vex.Core.Tests;

public sealed class WorkspaceEditHistoryTests
{
    [Fact]
    public void InvalidateRedo_ForLiveOwnedEdit_PreservesHostBaseline()
    {
        var history = new WorkspaceEditHistory();
        history.Reset("a");
        history.Observe("ab");
        Assert.Equal("a", history.Undo());
        Assert.True(history.CanRedo);

        history.InvalidateRedo();

        Assert.False(history.CanRedo);
        Assert.False(history.CanUndo);
        Assert.Null(history.Undo());
    }

    [Fact]
    public void Observe_AfterLiveSession_StoresSingleInstantEntry()
    {
        var history = new WorkspaceEditHistory();
        history.Reset("a");
        history.Observe("ab");
        history.BreakGroup();

        history.Observe("abc");
        history.Observe("abcd");

        Assert.Equal("ab", history.Undo());
        Assert.Equal("a", history.Undo());
        Assert.Null(history.Undo());
        Assert.Equal("ab", history.Redo());
        Assert.Equal("abcd", history.Redo());
    }
}
