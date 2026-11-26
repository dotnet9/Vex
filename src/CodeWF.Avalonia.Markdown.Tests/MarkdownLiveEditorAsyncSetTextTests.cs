using System.Reflection;

using CodeWF.Avalonia.Markdown.Editor.Controls;

using Avalonia.Controls;
using Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 巨文档异步 SetText 的抑制不变量：解析转后台期间（>200KB 阈值），编辑器 Text 仍是旧内容，
/// 此时光标/选区事件必须被抑制（RaiseSelectionChanged 直接返回）——否则宿主会把旧文本
/// （启动场景为空串）回推进文档模型，曾把即时布局下打开的巨文档清空并标成已修改。
/// 解析完成后抑制解除，选区事件恢复正常发布。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownLiveEditorAsyncSetTextTests(AvaloniaPlatformFixture platform)
{
    private static readonly System.Reflection.BindingFlags InstancePrivate =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private static string LargeMarkdown()
    {
        var paragraph = "这一段正文足够长，用于把文档撑过巨文档异步解析阈值。\n\n";
        var builder = new System.Text.StringBuilder("# 巨文档异步加载\n\n");
        while (builder.Length < 240_000) builder.Append(paragraph);
        return builder.ToString();
    }

    [Fact]
    public void AsyncSetText_SuppressesSelectionEventsUntilParsed()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            try
            {
                var markdownChanged = 0;
                var selectionChanged = 0;
                view.MarkdownChanged += (_, _) => markdownChanged++;
                view.SelectionChanged += (_, _) => selectionChanged++;
                var raiseSelection = (Action)view.GetType()
                    .GetMethod("RaiseSelectionChanged", InstancePrivate)!
                    .CreateDelegate(typeof(Action), view);

                var pending = LargeMarkdown();
                Assert.True(pending.Length > 200_000, "fixture must exceed the async parse threshold");
                view.SetText(pending);

                // 异步在途：选区事件必须被抑制（修复点：SetMarkdown len=0 清空缺陷）。
                raiseSelection();
                Assert.Equal(0, selectionChanged);
                Assert.Equal(0, markdownChanged);

                // 等后台解析应用完成（Text 就位）。
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (view.Text != pending && deadline.ElapsedMilliseconds < 30_000)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(10);
                }
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Equal(pending, view.Text);

                // 解析完成后抑制解除，选区事件恢复发布。
                raiseSelection();
                Assert.Equal(1, selectionChanged);
                Assert.Equal(0, markdownChanged);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
