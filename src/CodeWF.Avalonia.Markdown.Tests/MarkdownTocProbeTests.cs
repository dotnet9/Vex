using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// [TOC] 与行内图片渲染探针：dump 视觉树实际产物（条目文本/缩进/前景色、行内图片容器），
/// 用于对照回归前后差异。输出写文件供人工比对。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownTocProbeTests(AvaloniaPlatformFixture platform)
{
	[Fact]
	public void Probe_TocAndInlineImage() => platform.Run(() =>
	{
		var doc = "[TOC]\n\n# A 标题\n\n## B 标题\n\n行内图片：![演示](demo.svg)。正文。\n";
		var viewer = new MarkdownViewer { Markdown = doc };
		var window = new Window { Width = 600, Height = 400, Content = viewer };
		window.Show();
		try
		{
			for (var pass = 0; pass < 30; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}

			var dump = new System.Text.StringBuilder();
			foreach (var stb in viewer.GetVisualDescendants().OfType<SelectableTextBlock>())
			{
				var text = stb.Text ?? string.Join("", stb.Inlines?.OfType<Run>().Select(r => r.Text ?? "") ?? []);
				var fg = stb.GetValue(TextBlock.ForegroundProperty) as ISolidColorBrush;
				var classes = string.Join(",", stb.Classes);
				dump.AppendLine($"STB fg=#{fg?.Color.ToString() ?? "null"} classes=[{classes}] text=\"{text}\"");
			}

			foreach (var container in viewer.GetVisualDescendants().OfType<InlineUIContainer>())
			{
				dump.AppendLine($"InlineUIContainer child={container.Child?.GetType().Name ?? "null"}");
			}

			System.IO.File.WriteAllText(@"E:\github\temp_files\vex-build\toc-probe-report.txt", dump.ToString());
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});
}
