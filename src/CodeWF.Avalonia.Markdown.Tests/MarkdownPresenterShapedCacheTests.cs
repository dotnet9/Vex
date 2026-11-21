using Avalonia.Media.TextFormatting;

using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// TextRunCache 增量重排（Avalonia 12.1.6+）等价性面：经缓存增量重建的版面
/// 必须与全量失效重建完全一致（高度、行数、逐字符命中位置），任何差异都是
/// 缓存失效策略漏了某个影响 shaping 的输入。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownPresenterShapedCacheTests(AvaloniaPlatformFixture platform)
{
    private const double Tolerance = 1e-4;

    private sealed record LayoutMetrics(double Height, int LineCount, double[] X, double[] Y, double[] W, double[] H);

    private static LayoutMetrics CaptureMetrics(MarkdownInlinePresenter presenter, System.Reflection.MethodInfo create)
    {
        var layout = (TextLayout)create.Invoke(presenter, null)!;
        var length = presenter.Text?.Length ?? 0;
        var xs = new double[length + 1];
        var ys = new double[length + 1];
        var ws = new double[length + 1];
        var hs = new double[length + 1];
        for (var i = 0; i <= length; i++)
        {
            var rect = layout.HitTestTextPosition(i);
            xs[i] = rect.Left; ys[i] = rect.Top; ws[i] = rect.Width; hs[i] = rect.Height;
        }
        return new LayoutMetrics(layout.Height, layout.TextLines.Count, xs, ys, ws, hs);
    }

    private static void AssertSame(LayoutMetrics left, LayoutMetrics right, string stage)
    {
        Assert.True(Math.Abs(left.Height - right.Height) < Tolerance, $"{stage}: 高度不一致 {left.Height} vs {right.Height}");
        Assert.Equal(left.LineCount, right.LineCount);
        Assert.Equal(left.X.Length, right.X.Length);
        for (var i = 0; i < left.X.Length; i++)
        {
            Assert.True(Math.Abs(left.X[i] - right.X[i]) < Tolerance
                && Math.Abs(left.Y[i] - right.Y[i]) < Tolerance
                && Math.Abs(left.W[i] - right.W[i]) < Tolerance
                && Math.Abs(left.H[i] - right.H[i]) < Tolerance,
                $"{stage}: 命中位置 #{i} 不一致 ({left.X[i]},{left.Y[i]}) vs ({right.X[i]},{right.Y[i]})");
        }
    }

    private static void InvalidateCache(MarkdownInlinePresenter presenter)
    {
        var field = typeof(MarkdownInlinePresenter).GetField("_runCache",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        (field?.GetValue(presenter) as TextRunCache)?.Invalidate();
    }

    [Theory]
    [InlineData("append")]
    [InlineData("middleInsert")]
    [InlineData("middleDelete")]
    [InlineData("codeSpan")]
    [InlineData("imageFallback")]
    public void IncrementalRebuild_MatchesFreshRebuild(string stage)
    {
        platform.Run(() =>
        {
            var options = new CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg.MarkdownBlockViewOptions
            {
                FontSize = 15,
                CodeFontFamily = global::Avalonia.Media.FontFamily.Parse("Consolas, Microsoft YaHei UI"),
            };
            var presenter = new MarkdownInlinePresenter(options, plain: false);
            presenter.FontSize = 15;
            var create = typeof(MarkdownInlinePresenter).GetMethod("CreateTextLayout",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(create);

            var text = stage switch
            {
                "append" => "这是一段用于等价性验证的正文文字，包含中文与 ascii 123 混排，长度足够触发换行行为。" + new string('字', 600),
                "middleInsert" => "前半段正文。" + new string('字', 300) + "后半段正文。",
                "middleDelete" => "前半段正文。" + new string('字', 300) + "后半段正文。",
                "codeSpan" => "包含 `行内代码` 与 **粗体** 混排的段落，" + new string('字', 400),
                "imageFallback" => "含 ![缺失图](missing.png) 与 $E=mc^2$ 的段落，" + new string('字', 300),
                _ => throw new ArgumentOutOfRangeException(nameof(stage)),
            };
            presenter.Text = text;
            var baseline = CaptureMetrics(presenter, create!);

            var edited = stage switch
            {
                "append" => text + "追加的尾巴",
                "middleInsert" => text.Insert(text.Length / 2, "插入的中段内容"),
                "middleDelete" => text.Remove(text.Length / 2, 40),
                "codeSpan" => text + "更多**粗体**与 `代码` 混排",
                "imageFallback" => text + "再添一张 ![远程图](https://example.invalid/x.png)",
                _ => text,
            };
            presenter.Text = edited;
            var incremental = CaptureMetrics(presenter, create!);

            // 全量失效重建（正确性基准）
            InvalidateCache(presenter);
            var fresh = CaptureMetrics(presenter, create);

            AssertSame(incremental, fresh, $"{stage}: 增量缓存重建 vs 全量重建");
            // 基线（编辑前）仅用于确认初始版面已建立，不与编辑后比较（文本不同，度量必然不同）。
        });
    }
}
