using CodeWF.Avalonia.Markdown;
using CodeWF.Avalonia.Markdown.Controls;

using global::Avalonia.Controls;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 阅读视图右键菜单回归：
/// ① 默认无右键菜单——默认菜单曾含「复制渲染文本」（一键把整篇渲染文本压进
/// 剪贴板并覆盖原内容），误触代价过高已退役；复制主路径由 Ctrl+C 级联承担。
/// ② 宿主显式开启 IsDefaultContextMenuEnabled 时，菜单只有「复制选中文本」
/// 一项，且 Header 必须是本地化文本——此前四个 i18n JSON 被写坏成双 UTF-8
/// BOM，JsonDocument.Parse 静默失败 → 库内嵌翻译整体加载失败 → 菜单显示
/// 「CodeWF.Avalonia.Markdown.MarkdownL.*」键名。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerContextMenuLocalizationTests(AvaloniaPlatformFixture platform)
{
    private static MarkdownViewer CreateViewer(Window window)
    {
        var viewer = new MarkdownViewer();
        window.Content = viewer;
        window.Show();
        return viewer;
    }

    [Fact]
    public void DefaultContextMenu_IsRetired()
    {
        platform.Run(() =>
        {
            var window = new Window { Width = 500, Height = 420 };
            try
            {
                var viewer = CreateViewer(window);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                viewer.Markdown = "# 标题\n\n正文段落。";
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                Assert.True(viewer.ContextMenu is null, "默认不应提供右键菜单");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OptInContextMenu_HasOnlySelectionCopy_WithLocalizedHeader()
    {
        platform.Run(() =>
        {
            var window = new Window { Width = 500, Height = 420 };
            try
            {
                var viewer = CreateViewer(window);
                viewer.IsDefaultContextMenuEnabled = true;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                viewer.Markdown = "# 标题\n\n正文段落。";
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                var menu = viewer.ContextMenu;
                Assert.NotNull(menu);
                var items = menu!.ItemsSource!.Cast<MenuItem>().ToList();
                var headers = items.Select(item => item.Header as string ?? string.Empty).ToList();

                foreach (var header in headers)
                {
                    Assert.False(header.StartsWith("CodeWF.Avalonia.Markdown."),
                        $"菜单文案出现原始键名：{header}");
                }

                Assert.True(headers.Count == 1, $"显式开启时应只有一项，实际：{string.Join(", ", headers)}");
                Assert.True(
                    headers[0].Contains("复制") || headers[0].Contains("Copy") || headers[0].Contains("複製") || headers[0].Contains("コピー"),
                    $"唯一项应为「复制选中文本」的本地化文案，实际：{headers[0]}");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
