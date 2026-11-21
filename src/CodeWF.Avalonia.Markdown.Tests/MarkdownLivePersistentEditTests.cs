using CodeWF.Avalonia.Markdown.Editor.Controls;

using global::Avalonia.Controls;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// P1 连续所见即所得·代码块常驻编辑（Typora 对齐）：代码块失焦不翻回渲染态——
/// 其编辑态视觉与预览同形，模型逐键同步无内容可丢；Mermaid 源码覆盖不在此列。
/// 注意 Avalonia 12 TextBox.TextChanged 异步触发，编辑链断言前须 RunJobs。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownLivePersistentEditTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void CodeBlock_KeepsEditingStateAfterFocusLeaves()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.SetText("段落\n\n```csharp\nvar a = 1;\n```\n\n尾段");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                view.FocusBlock(1); // 代码块
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(view.ActiveBlockIndex == 1);

                view.InsertText("var b = 2;\n");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Contains("var b = 2;", view.Text);

                // 焦点移走（点入别的块）：代码块不翻态，仍处编辑态且模型同步。
                view.FocusBlock(0);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(0, view.ActiveBlockIndex);
                var blockView = FindBlock(view, 1);
                Assert.NotNull(blockView);
                Assert.True(blockView!.IsEditing, "代码块失焦后应保持编辑态（常驻）");

                // 回到代码块继续编辑：无需重新进入，直接续写。
                view.FocusBlock(1);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.InsertText("var c = 3;\n");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Contains("var c = 3;", view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ParagraphAlsoStaysEditing_AfterCodeBlockVisit()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.SetText("```csharp\nvar a = 1;\n```\n\n段落甲");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                view.FocusBlock(0);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(1);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.InsertText("乙");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.Equal(1, view.ActiveBlockIndex);
                var paragraph = FindBlock(view, 1);
                Assert.True(paragraph!.IsEditing, "纯文本块常驻编辑（既有行为保持）");
                var code = FindBlock(view, 0);
                Assert.True(code!.IsEditing, "代码块也保持常驻");
                Assert.Contains("段落甲乙", view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg.MarkdownBlockView? FindBlock(MarkdownLiveEditorView view, int index)
    {
        var field = typeof(MarkdownLiveEditorView).GetField("_blockViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var views = (System.Collections.IList?)field?.GetValue(view);
        if (views is null || index >= views.Count) return null;
        var find = typeof(MarkdownLiveEditorView).GetMethod("FindBlockView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return find?.Invoke(null, [views[index]!]) as CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg.MarkdownBlockView;
    }
}
