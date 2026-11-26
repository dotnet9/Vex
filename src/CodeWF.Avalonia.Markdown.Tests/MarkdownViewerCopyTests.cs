using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 阅读视图复制链路回归：Ctrl+C 的处理挂 viewer 的 Tunnel KeyDown，要求键盘焦点
/// 在 viewer 子树内，而块内原生选择（SelectableTextBlock）不取焦点、宿主焦点也未必
/// 在阅读区——此前 Ctrl+C 在阅读视图无声失效。修复：点击阅读区时收编焦点（延后到
/// bubble 之后落，避免被原生点击焦点清除）；复制级联：跨块选择 → 块内原生选择 →
/// 整篇渲染文本。本测试手动构造 KeyDown 路由事件（headless 键盘管线不派发原始输入），
/// 直接驱动 tunnel 处理器验证级联顺序与剪贴板结果。
/// 文档里的「带 &lt;b&gt;…&lt;/b&gt;」段落走回退路径（内联 HTML 不被段桥支持），
/// 专门保住「块内原生选择」级联的覆盖面。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerCopyTests(AvaloniaPlatformFixture platform)
{
    private const string Document =
        "# 标题\n\n第一段正文内容，用于块内选择测试。\n\n带 <b>内联 HTML</b> 的段落保留可选文本块路径。\n\n第二段正文内容，用于跨块选择测试。\n\n第三段正文内容。";

    private static readonly System.Reflection.BindingFlags InstancePrivate =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private static void RaiseCtrlC(global::Avalonia.Interactivity.Interactive source)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = source,
            Key = Key.C,
            KeyModifiers = KeyModifiers.Control,
        };
        source.RaiseEvent(args);
    }

    private static string? ClipboardText(MarkdownViewer viewer) =>
        TopLevel.GetTopLevel(viewer)!.Clipboard!.TryGetDataAsync().GetAwaiter().GetResult()
            ?.TryGetValueAsync(DataFormat.Text).GetAwaiter().GetResult();

    private static void SelectNative(SelectableTextBlock stb, int start, int end)
    {
        stb.SelectionStart = start;
        stb.SelectionEnd = end;
    }

    private static void SelectAcrossBlocks(MarkdownViewer viewer, Control from)
    {
        // 走真实选择控制器：Begin 在 from 位置，Extend 到文档中部（internal 类，测试工程可见）。
        var controller = (CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownSelectionController)
            typeof(MarkdownViewer).GetField("_selectionController", InstancePrivate)!.GetValue(viewer)!;
        var host = (Visual)typeof(MarkdownViewer).GetField("_documentHost", InstancePrivate)!.GetValue(viewer)!;
        var fromPoint = from.TranslatePoint(new Point(4, from.Bounds.Height / 2), host)!.Value;
        var toPoint = new Point(fromPoint.X + 60, Math.Min(fromPoint.Y + 80, host.Bounds.Height - 1));
        controller.Begin(fromPoint);
        controller.Extend(toPoint);
        typeof(MarkdownViewer).GetMethod("UpdateSelectionState", InstancePrivate)!.Invoke(viewer, []);
    }

    [Fact]
    public void CtrlC_WithCrossBlockSelection_CopiesSelection()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window);
            try
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                viewer.Markdown = Document;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                viewer.Focus();
                var from = (Control)FindTextControl(viewer, "第一段")!;
                SelectAcrossBlocks(viewer, from);
                Assert.True(viewer.HasSelection, "控制器拖选应产生跨块选区");
                var selected = viewer.SelectedText;
                Assert.False(string.IsNullOrEmpty(selected));

                RaiseCtrlC(from);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(selected, ClipboardText(viewer));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CtrlC_WithNativeBlockSelection_CopiesBlockSelection()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window);
            try
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                viewer.Markdown = Document;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                viewer.Focus();
                // 回退路径段落（内联 HTML）保留了 SelectableTextBlock：块内原生选择在此验证。
                var target = (SelectableTextBlock)FindTextControl(viewer, "<b>")!;
                SelectNative(target, 4, 10);
                var native = target.SelectedText;
                Assert.False(string.IsNullOrEmpty(native), "原生选区应已设置");
                Assert.False(viewer.HasSelection, "块内原生选择不属于跨块控制器");

                RaiseCtrlC(target);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(native, ClipboardText(viewer));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CtrlC_WithoutSelection_DoesNothing()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window);
            try
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                viewer.Markdown = Document;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                viewer.Focus();
                // 剪贴板是 headless 全局 stub：清掉上一条测试的残留，隔离断言。
                TopLevel.GetTopLevel(viewer)!.Clipboard!.ClearAsync().GetAwaiter().GetResult();
                var anyBlock = FindTextControl(viewer, "第一段")!;
                RaiseCtrlC(anyBlock);
                Dispatcher.UIThread.RunJobs();

                // 无选区不得兜底复制整篇——误点一下再按 Ctrl+C 就拿到全文，体感失控。
                Assert.Null(ClipboardText(viewer));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Control? FindTextControl(MarkdownViewer viewer, string marker) =>
        (Control?)viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains(marker) == true
                || tb.Inlines?.OfType<Span>().Any(sp => sp.Inlines.OfType<Run>().Any(r => (r.Text ?? "").Contains(marker))) == true
                || tb.Inlines?.OfType<Run>().Any(r => (r.Text ?? "").Contains(marker)) == true)
        ?? viewer.GetVisualDescendants().OfType<MarkdownTextSegmentView>()
            .FirstOrDefault(segment => segment.PlainText.Contains(marker));

    private static MarkdownViewer CreateViewer(out Window window)
    {
        var viewer = new MarkdownViewer();
        window = new Window { Width = 500, Height = 420, Content = viewer };
        window.Show();
        return viewer;
    }
}
