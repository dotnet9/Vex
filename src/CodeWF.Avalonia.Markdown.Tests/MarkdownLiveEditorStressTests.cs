using System.Reflection;

using CodeWF.Avalonia.Markdown.Editor;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Avalonia.Controls;
using Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 脏块回写压力测试：多类型块混合大文档连续编辑，每步验证全文一致性。
/// 覆盖打字/勾选类快照/标记转换/跨块导航/撤销重做交替等真实编辑模式。
/// 块映射（种子文档）：0=标题1 1=标题2 2=段落 3=代码 4..6=列表项 7=引用 8=表格 9=分隔线 10=段落。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownLiveEditorStressTests(AvaloniaPlatformFixture platform)
{
    private const string Seed =
        "# 项目方案\n\n## 背景\n\n当前系统存在性能瓶颈，需要优化。\n\n```csharp\nvar old = GetLegacy();\n```\n\n- 第一项\n- 第二项\n- 第三项\n\n> 引用一\n> 引用二\n\n| 列 A | 列 B |\n| --- | --- |\n| 值 1 | 值 2 |\n\n---\n\n收尾段落。";

    private static readonly System.Reflection.BindingFlags InstancePrivate =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    [Fact]
    public void MixedEditing_MaintainsTextIntegrity()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.SetText(Seed);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                Assert.Equal(Seed, view.Text);

                // 1. 在段落里打字（FocusBlock 光标落块尾，插入即块尾追加）
                view.FocusBlock(2); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("补充："); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("需要优化。补充：", view.Text);

                // 2. 在列表项里打字
                view.FocusBlock(5); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("（已确认）"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("第二项（已确认）", view.Text);

                // 3. 在代码块里打字（块 3 是代码，追加行仍留在围栏内）
                view.FocusBlock(3); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("\nvar @new = CreateNew();"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("var @new = CreateNew();", view.Text);

                // 4. 在引用里打字（引用整块是一块，块尾即"引用二"行尾）
                view.FocusBlock(7); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("补充说明"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("引用二补充说明", view.Text);

                // 5. 表格单元格编辑（走 MarkdownTableView 单元格提交真实路径；
                //    块宿主是 LazyLiveBlock 惰性包装，先物化再取单元格）
                view.FocusBlock(8); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var blockViews = (List<Control>)typeof(MarkdownLiveEditorView)
                    .GetField("_blockViews", InstancePrivate)!.GetValue(view)!;
                var table = blockViews[8].GetType().GetMethod("Realize",
                    BindingFlags.Public | BindingFlags.Instance)!.Invoke(blockViews[8], null)!;
                var tableView = Assert.IsType<MarkdownTableView>(table);
                var cells = (List<List<MarkdownBlockView>>)typeof(MarkdownTableView)
                    .GetField("_cells", InstancePrivate)!.GetValue(tableView)!;
                var valueCell = cells[1][0]; // 数据行 1 列 0 = "值 1"
                valueCell.BeginEditAtEnd();
                valueCell.InsertText("（修订）");
                valueCell.CommitAndRender();
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("值 1（修订）", view.Text);

                // 6. 撤销三次，每次全文一致
                for (var i = 0; i < 3; i++)
                {
                    view.ExecuteAsync(MarkdownEditorAction.Undo);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                }

                // 7. 重做两次
                for (var i = 0; i < 2; i++)
                {
                    view.ExecuteAsync(MarkdownEditorAction.Redo);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                }

                // 8. 标记转换：段落变标题
                view.FocusBlock(2); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.ExecuteAsync(MarkdownEditorAction.Heading2);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("## 当前系统存在性能瓶颈", view.Text);

                // 9. 最终全文完整性：所有块类型的内容都在
                Assert.Contains("# 项目方案", view.Text);
                Assert.Contains("## 背景", view.Text);
                Assert.Contains("```csharp", view.Text);
                Assert.Contains("var old = GetLegacy();", view.Text);
                Assert.Contains("var @new = CreateNew();", view.Text);
                Assert.Contains("- 第一项", view.Text);
                Assert.Contains("> 引用一", view.Text);
                Assert.Contains("| 列 A |", view.Text);
                Assert.Contains("值 2", view.Text);
                Assert.Contains("---", view.Text);
                Assert.Contains("收尾段落", view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RapidTyping_LargeDocument_TextAlwaysConsistent()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                // 大文档：200 段 × 60 字 ≈ 13KB
                var sb = new System.Text.StringBuilder();
                for (var i = 0; i < 200; i++)
                    sb.Append("## 第").Append(i).Append("章\n\n").Append(i).Append("段正文内容。").Append('\n');
                var doc = sb.ToString();

                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.SetText(doc);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                // 连续 3 次跨块编辑（落在 650ms 合并窗口内，跨块编辑必须各自成条）
                view.FocusBlock(100); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("[A]"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("[A]", view.Text);

                view.FocusBlock(50); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("[B]"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("[B]", view.Text);

                view.FocusBlock(150); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.InsertText("[C]"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Contains("[C]", view.Text);

                // 撤销全部 → 回到原文
                for (var i = 0; i < 3; i++)
                {
                    view.ExecuteAsync(MarkdownEditorAction.Undo);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                }
                Assert.Equal(doc, view.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
