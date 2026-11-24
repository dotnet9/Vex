using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using CodeWF.Avalonia.Markdown.Editor.Services;

using global::Avalonia.Controls;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownLiveEditorHistoryTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void UndoRedo_RestoresSelectionBeforeAndAfterEdit()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                view.SetText("**bold** tail");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(0);
                var editor = FindBlock(view, 0)!.EditingInput;
                editor.CaretIndex = 6;
                editor.SelectionStart = 2;
                editor.SelectionEnd = 6;

                view.InsertText("x");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**x** tail", view.Text);
                Assert.Equal((3, 0), view.Selection);

                view.ExecuteAsync(MarkdownEditorAction.Undo);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**bold** tail", view.Text);
                Assert.Equal((2, 4), view.Selection);
                Assert.Equal("bold", view.SelectedText);

                view.ExecuteAsync(MarkdownEditorAction.Redo);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**x** tail", view.Text);
                Assert.Equal((3, 0), view.Selection);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RestoreState_IntoNewView_PreservesRedoStack()
    {
        platform.Run(() =>
        {
            var original = new MarkdownLiveEditorView();
            var restored = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = original };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                original.SetText("**bold** tail");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                original.FocusBlock(0);
                var editor = FindBlock(original, 0)!.EditingInput;
                editor.CaretIndex = 6;
                editor.SelectionStart = 2;
                editor.SelectionEnd = 6;
                original.InsertText("x");
                Dispatcher.UIThread.RunJobs();
                Assert.True(original.Undo());
                Dispatcher.UIThread.RunJobs();
                var state = original.CaptureState();

                window.Content = restored;
                restored.SetText("discard");
                Dispatcher.UIThread.RunJobs();
                restored.RestoreState(state);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**bold** tail", restored.Text);
                Assert.Equal((2, 4), restored.Selection);

                Assert.True(restored.Redo());
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**x** tail", restored.Text);
                Assert.Equal((3, 0), restored.Selection);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RestoreState_ClampsSelectionToRestoredBlock()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                view.SetText("discard");
                Dispatcher.UIThread.RunJobs();
                view.RestoreState(new MarkdownLiveEditorState(
                    new MarkdownLiveEditorSnapshot("abc", 0, -5, 99), [], []));
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("abc", view.Text);
                Assert.Equal((0, 3), view.Selection);
                Assert.Equal("abc", view.SelectedText);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GroupedUndoRedo_RestoresFirstBeforeAndLastAfterSelection()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                view.SetText("abcdef");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(0);
                var editor = FindBlock(view, 0)!.EditingInput;
                editor.CaretIndex = 3;
                editor.SelectionStart = 1;
                editor.SelectionEnd = 3;
                view.InsertText("X");
                Dispatcher.UIThread.RunJobs();
                view.InsertText("Y");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("aXYdef", view.Text);
                Assert.Equal((3, 0), view.Selection);

                Assert.True(view.Undo());
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("abcdef", view.Text);
                Assert.Equal((1, 2), view.Selection);
                Assert.Equal("bc", view.SelectedText);

                Assert.True(view.Redo());
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("aXYdef", view.Text);
                Assert.Equal((3, 0), view.Selection);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RestoreState_IntoNewView_PreservesUndoRedoAndSelection()
    {
        platform.Run(() =>
        {
            var original = new MarkdownLiveEditorView();
            var restored = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = original };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                original.SetText("**bold** tail");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                original.FocusBlock(0);
                var editor = FindBlock(original, 0)!.EditingInput;
                editor.CaretIndex = 6;
                editor.SelectionStart = 2;
                editor.SelectionEnd = 6;
                original.InsertText("x");
                Dispatcher.UIThread.RunJobs();
                var state = original.CaptureState();

                window.Content = restored;
                restored.SetText("discard");
                Dispatcher.UIThread.RunJobs();
                restored.RestoreState(state);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("**x** tail", restored.Text);
                Assert.Equal((3, 0), restored.Selection);

                restored.ExecuteAsync(MarkdownEditorAction.Undo);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**bold** tail", restored.Text);
                Assert.Equal((2, 4), restored.Selection);

                restored.ExecuteAsync(MarkdownEditorAction.Redo);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("**x** tail", restored.Text);
                Assert.Equal((3, 0), restored.Selection);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static MarkdownBlockView? FindBlock(MarkdownLiveEditorView view, int index)
    {
        var field = typeof(MarkdownLiveEditorView).GetField("_blockViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var views = (System.Collections.IList?)field?.GetValue(view);
        if (views is null || index >= views.Count) return null;
        var find = typeof(MarkdownLiveEditorView).GetMethod("FindBlockView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return find?.Invoke(null, [views[index]!]) as MarkdownBlockView;
    }
}
