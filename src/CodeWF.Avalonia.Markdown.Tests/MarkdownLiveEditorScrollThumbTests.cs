using CodeWF.Avalonia.Markdown.Editor.Controls;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 即时编辑滚动条回归：空白点击落光标的 Tunnel 不得吞掉滚动条 thumb 的
/// PointerPressed（此前把它当文档空白接管并 FocusBlock，thumb 收不到按下
/// 事件，滚动条只剩滚轮能滚、鼠标拖不动）。修复：ScrollBar chrome 放行。
/// 观测点用 SelectionChanged（FocusBlock 的副作用）：修复前 thumb 点击被当
/// 文档空白接管 → 触发落光标；修复后放行给 ScrollBar 自行处理（ScrollBar
/// 的 OnPointerPressed 自会标记 Handled）→ 不触发落光标。
/// 本测试手动构造路由事件（headless 输入管线不派发原始指针输入）。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownLiveEditorScrollThumbTests(AvaloniaPlatformFixture platform)
{
    private static PointerPressedEventArgs PressOn(Visual source, Visual root, Point position) =>
        new(source, new Pointer(1, PointerType.Mouse, true), root, position,
            timestamp: 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None);

    [Fact]
    public void PressOnScrollBar_DoesNotStealCaretAndBlankClickStillWorks()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 500, Height = 300, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                view.SetText("# 标题\n\n一段内容。");
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                // 独立 ScrollBar 挂进块宿主面板（viewer 子树内，事件路由经过 viewer tunnel）。
                var bar = new ScrollBar { Orientation = Orientation.Vertical, Maximum = 100, ViewportSize = 50 };
                (view.Content as Panel)!.Children.Add(bar);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                var caretStolen = 0;
                view.SelectionChanged += (_, _) => caretStolen++;

                // 点击滚动条：不得被当文档空白接管（不落光标）。
                // 注意必须从 bar 自己 RaiseEvent：Route 以调用者为根并强制改写 Source，
                // 从 view 调用会把 Source 改成 view（等于模拟不出"点击发生在 bar 上"）。
                var thumbPress = PressOn(bar, window, new Point(300, 100));
                bar.RaiseEvent(thumbPress);
                Assert.Equal(0, caretStolen);

                // 对照：点击文档空白仍应接管并落光标（Typora 行为保持）。
                var blankPress = PressOn(view, window, new Point(300, 260));
                view.RaiseEvent(blankPress);
                Assert.True(caretStolen > 0, "文档空白点击应接管并落光标");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
