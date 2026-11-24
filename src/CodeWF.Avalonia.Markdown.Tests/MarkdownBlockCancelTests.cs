using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownBlockCancelTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void Escape_RestoresModelEditorAndHostText()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                view.SetText("原文");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(0);
                view.InsertText("修改");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("原文修改", view.Text);

                var block = FindBlock(view, 0)!;
                var args = new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Source = block.EditingInput,
                    Key = Key.Escape,
                };
                block.EditingInput.RaiseEvent(args);
                Dispatcher.UIThread.RunJobs();

                Assert.True(args.Handled);
                Assert.Equal("原文", block.Model.Text);
                Assert.Equal("原文", block.EditingInput.Text);
                Assert.Equal("原文", view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SyncEditorFromModel_RawBlock_UsesRawText()
    {
        platform.Run(() =>
        {
            var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Raw, Raw = "<div>old</div>" };
            var block = new MarkdownBlockView(model, isCode: false, new MarkdownBlockViewOptions());
            var window = new Window { Width = 600, Height = 200, Content = block };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                block.BeginEdit(0);
                model.Raw = "<div>new</div>";
                block.SyncEditorFromModel(5);

                Assert.Equal("<div>new</div>", block.EditingInput.Text);
                Assert.Equal("<div>new</div>", model.Raw);
                Assert.Equal(string.Empty, model.Text);
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
