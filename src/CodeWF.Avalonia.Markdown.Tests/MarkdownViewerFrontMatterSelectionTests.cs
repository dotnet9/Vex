using CodeWF.Avalonia.Markdown.Controls;

using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 阅读视图元数据（front matter）可选复制回归：键值此前用普通 TextBlock
/// 渲染——不支持选择，也无法被跨块选择控制器投影字形（TextSegments 只认
/// SelectableTextBlock），阅读模式下元数据选不了复制不了。改用
/// SelectableTextBlock 后：块内原生选择可用，且自动纳入 Ctrl+C 复制级联
/// （跨块选择 → 块内原生选择 → 整篇兜底）的第二层。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerFrontMatterSelectionTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void FrontMatter_TableLayout_KeysValuesAndBadges_AreSelectable()
    {
        platform.Run(() =>
        {
            var viewer = new MarkdownViewer();
            var window = new Window { Width = 500, Height = 420, Content = viewer };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                viewer.Markdown = "---\ntitle: 测试标题\nslug: test-front-matter\ncategories:\n  - .NET\n  - Avalonia\ntags:\n  - 性能\n---\n\n正文段落。";
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

                var selectables = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().ToList();
                // 表格键列与值列均为可选文本块。
                Assert.Contains(selectables, tb => (tb.Text ?? string.Empty) == "title");
                Assert.Contains(selectables, tb => (tb.Text ?? string.Empty) == "测试标题");
                Assert.Contains(selectables, tb => (tb.Text ?? string.Empty) == "slug");

                // categories/tags 列表值渲染为徽章（可选中），无 "- " 原始前缀。
                Assert.Contains(selectables, tb => (tb.Text ?? string.Empty) == ".NET");
                Assert.Contains(selectables, tb => (tb.Text ?? string.Empty) == "Avalonia");
                Assert.Contains(selectables, tb => (tb.Text ?? string.Empty) == "性能");
                Assert.DoesNotContain(selectables, tb => (tb.Text ?? string.Empty).StartsWith("- "));

                // 原生选区可设置（选择能力成立的直接证据）。
                var title = selectables.First(tb => (tb.Text ?? string.Empty) == "测试标题");
                title.SelectionStart = 0;
                title.SelectionEnd = title.Text!.Length;
                Assert.Equal("测试标题", title.SelectedText);

                // 表格形态：元数据卡内是两列 Grid（键列 Auto + 值列 *），行数 = 条目数。
                var card = viewer.GetVisualDescendants().OfType<Border>()
                    .First(b => b.Classes.Contains("MdFrontMatter"));
                var grid = card.Child is Panel panel
                    ? panel.Children.OfType<global::Avalonia.Controls.Grid>().First()
                    : throw new Xunit.Sdk.XunitException("元数据卡内应存在表格 Grid");
                Assert.Equal(2, grid.ColumnDefinitions.Count);
                Assert.Equal(4, grid.RowDefinitions.Count); // title/slug/categories/tags
                Assert.Equal("Auto", grid.ColumnDefinitions[0].Width.ToString());
            }
            finally
            {
                window.Close();
            }
        });
    }
}
