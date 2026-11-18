using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 行内图片单表面（阶段 3）：含图段落块构造后编辑器应持有源码全文，
/// 挂树布局后 InlinePresenter 建版面时图片进入叠绘清单（几何 + 位图就绪）。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class InlineImageBlockTests(AvaloniaPlatformFixture platform)
{
    /// <summary>1×1 PNG（内嵌字节，规避 headless 下 RenderTargetBitmap.Save 的 Skia 缺口）。</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    [Fact]
    public void ImageParagraph_Construction_KeepsSourceInEditor()
    {
        platform.Run(() =>
        {
            const string source = "行内图片段落 ![示例图](inline-probe.png) 尾文字";
            var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = source };
            var view = new MarkdownBlockView(model, isCode: false, new MarkdownBlockViewOptions());
            var editor = view.GetLogicalDescendants().OfType<TextBox>().First();
            Assert.Equal(source, editor.Text);
        });
    }

    [Fact]
    public void ImageRun_Parse_CarriesSpanAndAlt()
    {
        const string source = "前文 ![示例图](a.png) 后文";
        var runs = MarkdownTextRunParser.Parse(source);
        // 字面文本 run 必须保留（图片前后文正常渲染的关键）。
        Assert.Contains(runs, r => r.ImageUrl is null && r.Text.StartsWith("前文", StringComparison.Ordinal));
        Assert.Contains(runs, r => r.ImageUrl is null && r.Text.Contains("后文", StringComparison.Ordinal));
        var image = runs.First(r => r.ImageUrl is not null);
        Assert.Equal("a.png", image.ImageUrl);
        Assert.Equal("示例图", image.Text);
        var spanStart = source.IndexOf("![", StringComparison.Ordinal);
        Assert.Equal(spanStart, image.SourceStart);
        Assert.Equal(source.IndexOf("](a.png)", StringComparison.Ordinal) + "](a.png)".Length - spanStart,
            image.SourceLength);
    }

    [Fact]
    public void ImagePresenter_Layout_ProducesOverlayGeometry()
    {
        platform.Run(async () =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "inline-image-tests.png");
            File.WriteAllBytes(path, TinyPng);

            const string source = "前文 ![示例图](inline-image-tests.png) 后文";
            var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = source };
            var options = new MarkdownBlockViewOptions { ImageBasePath = System.IO.Path.GetDirectoryName(path) };
            var view = new MarkdownBlockView(model, isCode: false, options);
            var window = new Window { Width = 800, Height = 200, Content = new Border { Child = view } };
            window.Show();
            try
            {
                window.UpdateLayout();
                var presenter = view.GetVisualDescendants()
                    .FirstOrDefault(p => p.GetType().Name == "MarkdownInlinePresenter");
                Assert.NotNull(presenter);
                var overlays = presenter!.GetType()
                    .GetField("_imageOverlays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(presenter) as System.Collections.IEnumerable;
                Assert.NotNull(overlays);
                Assert.True(overlays!.Cast<object>().Any(), "Image span should produce overlay geometry");
            }
            finally
            {
                window.Close();
            }
            await System.Threading.Tasks.Task.CompletedTask;
        });
    }
}
