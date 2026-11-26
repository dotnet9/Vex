using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

using Markdig;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 自绘文本段回归：内联桥的文本/样式区间/链接区间构建与回退判定，
/// 标题渲染器段路径接入（支持内联 → 段控件；含图片等长尾内联 → 回退旧路径）。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownSegmentContentTests(AvaloniaPlatformFixture platform)
{
	private static MarkdownPipeline TestPipeline() => new MarkdownPipelineBuilder()
		.UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough
			| Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Subscript
			| Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Superscript
			| Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Marked)
		.UseMathematics()
		.Build();
	private static (MarkdownSegmentContent Content, IReadOnlyList<MarkdownLinkSpan> Links) BuildHeading(string headingText)
	{
		var document = Markdig.Markdown.Parse($"## {headingText}", TestPipeline());
		var heading = Assert.IsType<Markdig.Syntax.HeadingBlock>(document.First());
		Assert.True(MarkdownSegmentContentBuilder.TryBuild(heading.Inline, softBreakAsNewline: true,
			out var content, out var links));
		return (content, links);
	}

	[Fact]
	public void PlainHeading_ProducesBareText()
	{
		var (content, links) = BuildHeading("纯文本标题");
		Assert.Equal("纯文本标题", content.Text);
		Assert.Empty(content.Styles);
		Assert.Empty(links);
	}

	[Fact]
	public void BoldAndCode_ProduceStyleSpans()
	{
		var (content, _) = BuildHeading("**粗体** 与 `code` 与 *斜体*");
		Assert.Equal("粗体 与 code 与 斜体", content.Text);
		Assert.Contains(content.Styles, s => s is { Style.Bold: true } && content.Text.Substring(s.Start, s.Length) == "粗体");
		Assert.Contains(content.Styles, s => s is { Style.Code: true } && content.Text.Substring(s.Start, s.Length) == "code");
		Assert.Contains(content.Styles, s => s is { Style.Italic: true } && content.Text.Substring(s.Start, s.Length) == "斜体");
	}

	[Fact]
	public void StrikeAndHighlight_ProduceStyleSpans()
	{
		var (content, _) = BuildHeading("~~划线~~ 与 ==高亮==");
		Assert.Equal("划线 与 高亮", content.Text);
		Assert.Contains(content.Styles, s => s.Style.Strike && content.Text.Substring(s.Start, s.Length) == "划线");
		Assert.Contains(content.Styles, s => s.Style.Highlight && content.Text.Substring(s.Start, s.Length) == "高亮");
	}

	[Fact]
	public void Link_ProducesLinkSpanWithText()
	{
		var (content, links) = BuildHeading("[官网](https://vex.codewf.com)");
		Assert.Equal("官网", content.Text);
		var link = Assert.Single(links);
		Assert.Equal("https://vex.codewf.com", link.Url);
		Assert.Equal(0, link.Start);
		Assert.Equal(2, link.End);
		Assert.Contains(content.Styles, s => s.Style.Link && s.Start == 0 && s.Length == 2);
	}

	[Fact]
	public void ImageInline_IsUnsupported_FallsBack()
	{
		Assert.False(MarkdownSegmentContentBuilder.TryBuild(ParseInlines("![图](x.png)"), true, out _, out _));
	}

	[Fact]
	public void MathInline_IsUnsupported_FallsBack()
	{
		Assert.False(MarkdownSegmentContentBuilder.TryBuild(ParseInlines("$x^2$"), true, out _, out _));
	}

	[Fact]
	public void Subscript_IsUnsupported_FallsBack()
	{
		Assert.False(MarkdownSegmentContentBuilder.TryBuild(ParseInlines("H~2~O"), true, out _, out _));
	}

	private static Markdig.Syntax.Inlines.ContainerInline ParseInlines(string markdown)
	{
		var document = Markdig.Markdown.Parse(markdown, TestPipeline());
		var paragraph = Assert.IsType<Markdig.Syntax.ParagraphBlock>(document.First());
		return paragraph.Inline!;
	}

	[Fact]
	public void HeadingRenderer_UsesSegmentForSupportedHeading() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer { Markdown = "## 纯文本标题" };
		var window = new Window { Width = 400, Height = 300, Content = viewer };
		window.Show();
		try
		{
			for (var pass = 0; pass < 20; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}

			Assert.Contains(viewer.GetVisualDescendants().OfType<MarkdownTextSegmentView>(),
				segment => segment.PlainText == "纯文本标题");
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	[Fact]
	public void HeadingRenderer_FallsBackToSelectableText_WhenImageInline() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer { Markdown = "## ![图](x.png) 标题" };
		var window = new Window { Width = 400, Height = 300, Content = viewer };
		window.Show();
		try
		{
			for (var pass = 0; pass < 20; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}

			Assert.DoesNotContain(viewer.GetVisualDescendants().OfType<MarkdownTextSegmentView>(),
				segment => segment.PlainText.Contains("标题"));
			Assert.Contains(viewer.GetVisualDescendants().OfType<SelectableTextBlock>(),
				block => (block.Inlines?.FirstOrDefault() as Run)?.Text?.Contains("图") == true || block.Text?.Contains("图") == true);
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	[Fact]
	public void SegmentSelection_VisualIntegration_RoundsTrip() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer { Markdown = "## 可选中的标题" };
		var window = new Window { Width = 400, Height = 300, Content = viewer };
		window.Show();
		try
		{
			for (var pass = 0; pass < 20; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}

			var segment = viewer.GetVisualDescendants().OfType<MarkdownTextSegmentView>()
				.First(candidate => candidate.PlainText == "可选中的标题");
			var selection = (CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownSelectionController)
				typeof(MarkdownViewer).GetField("_selectionController",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
				.GetValue(viewer)!;
			var host = (Visual)typeof(MarkdownViewer).GetField("_documentHost",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
				.GetValue(viewer)!;

			var local = segment.TranslatePoint(new Point(4, segment.Bounds.Height / 2), host)!.Value;
			selection.Begin(local);
			var far = segment.TranslatePoint(
				new Point(segment.Bounds.Width - 4, segment.Bounds.Height / 2), host)!.Value;
			selection.Extend(far);
			Assert.True(selection.HasSelection, "自绘段选区应生效");
			Assert.Contains("可选中的", selection.SelectedText);
			selection.Clear();
			Assert.False(selection.HasSelection);
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});
}
