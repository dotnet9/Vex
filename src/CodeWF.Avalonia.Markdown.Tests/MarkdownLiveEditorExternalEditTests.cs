using CodeWF.Avalonia.Markdown.Editor;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;

using global::Avalonia.Controls;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 对齐式外部替换（预览勾选任务回写走 <see cref="MarkdownLiveEditorView.ApplyExternalEdit"/>）：
/// <para>
/// 1. 未变组（前缀/后缀）复用模型与视图：编辑态、活动块与已输入内容跨替换保留；
/// 2. 撤销历史保留且外部改写本身可撤销（与源码编辑器一致），不再整篇重置清空历史；
/// 3. 结构性中段变化（段落→标题等）同样收敛为正确全文。
/// </para>
/// 注意：Avalonia 12 的 TextBox.TextChanged 经 Dispatcher 异步触发，凡触发编辑链的
/// 操作之后断言前必须 <see cref="Dispatcher.UIThread.RunJobs"/> 排空。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownLiveEditorExternalEditTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void ApplyExternalEdit_TaskToggle_PreservesEditingStateAndUndo()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            string? raised = null;
            view.MarkdownChanged += (_, text) => raised = text;
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.SetText("- [ ] 甲\n\n段落一\n\n段落二");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                view.FocusBlock(2);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.InsertText("尾");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("- [ ] 甲\n\n段落一\n\n段落二尾", view.Text);

                // 预览勾选回写：只改首块任务状态，中段无变化、后缀组平移复用。
                view.ApplyExternalEdit("- [x] 甲\n\n段落一\n\n段落二尾", 2, 3);

                Assert.Equal("- [x] 甲\n\n段落一\n\n段落二尾", view.Text);
                Assert.Equal("- [x] 甲\n\n段落一\n\n段落二尾", raised);
                Assert.Equal(2, view.ActiveBlockIndex);

                // 编辑态仍在：继续输入落在原块。
                view.InsertText("再");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("- [x] 甲\n\n段落一\n\n段落二尾再", view.Text);

                // 撤销逐级可回：先回「外部改写后」（键入「再」是一步），再回「改写前」（勾选恢复未打）。
                view.ExecuteAsync(MarkdownEditorAction.Undo);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("- [x] 甲\n\n段落一\n\n段落二尾", view.Text);
                view.ExecuteAsync(MarkdownEditorAction.Undo);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("- [ ] 甲\n\n段落一\n\n段落二尾", view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyExternalEdit_MiddleStructuralChange_KeepsSuffixBlocks()
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
                view.SetText("# 标\n\n甲\n\n乙");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(2);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.InsertText("尾");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("# 标\n\n甲\n\n乙尾", view.Text);

                // 中段段落转标题：前缀（# 标）与后缀（乙尾）组不动。
                view.ApplyExternalEdit("# 标\n\n## 甲改\n\n乙尾", 4, 1);

                Assert.Equal("# 标\n\n## 甲改\n\n乙尾", view.Text);
                Assert.Equal(2, view.ActiveBlockIndex);
                view.InsertText("续");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("# 标\n\n## 甲改\n\n乙尾续", view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyExternalEdit_ActiveBlockInMiddle_LosesFocusGracefully()
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
                view.SetText("甲\n\n乙");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(1);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(1, view.ActiveBlockIndex);

                // 被编辑块本身被外部改写：编辑态让位，全文正确，不崩溃。
                view.ApplyExternalEdit("甲\n\n乙改\n\n丙", 2, 1);

                Assert.Equal("甲\n\n乙改\n\n丙", view.Text);
                Assert.True(view.BlockCount >= 3);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
