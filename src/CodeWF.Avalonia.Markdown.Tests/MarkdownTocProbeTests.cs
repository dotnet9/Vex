using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;

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
		var doc = "[TOC]\n\n# A 标题\n\n## B 标题\n\n链接：[Avalonia](https://x) 与行内图片：![演示](demo.svg)。\n";
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
				foreach (var span in stb.Inlines?.OfType<global::Avalonia.Controls.Documents.Span>() ?? [])
				{
					var spanFg = span.GetValue(TextBlock.ForegroundProperty) as ISolidColorBrush;
					var spanText = string.Join("", span.Inlines.OfType<Run>().Select(r => r.Text ?? ""));
					dump.AppendLine($"  SPAN fg=#{spanFg?.Color.ToString() ?? "null"} text=\"{spanText}\"");
				}
			}

			foreach (var container in viewer.GetVisualDescendants().OfType<InlineUIContainer>())
			{
				dump.AppendLine($"InlineUIContainer child={container.Child?.GetType().Name ?? "null"}");
			}

			// 探针报告写入仓库 artifacts（已 gitignore），路径随测试源码定位解析。
			var reportPath = System.IO.Path.Combine(ResolveRepositoryPath(
				System.IO.Path.Combine("artifacts", "toc-probe-report.txt")));
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath)!);
			System.IO.File.WriteAllText(reportPath, dump.ToString());
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	/// <summary>仓库内路径随测试源码定位解析，不绑定具体盘符/检出位置。</summary>
	private static string ResolveRepositoryPath(string relativePath,
		[System.Runtime.CompilerServices.CallerFilePath] string testFile = "") =>
		System.IO.Path.GetFullPath(System.IO.Path.Combine(
			System.IO.Path.GetDirectoryName(testFile)!, "..", "..", relativePath));

	[Fact]
	public void Probe_TocNumbers_ToggleChangesText() => platform.Run(() =>
	{
		var doc = "[TOC]\n\n# A 标题\n\n## B 标题\n";
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

			// TOC 条目是 SelectableTextBlock.MdListMarker（CreateToc 创建），不是段控件。
			string Dump() => string.Join(" | ", viewer.GetVisualDescendants()
				.OfType<SelectableTextBlock>()
				.Where(stb => stb.Classes.Contains("MdListMarker"))
				.Select(stb => stb.Text ?? ""));

			var before = Dump();
			viewer.ShowTocNumbers = true;
			viewer.RerenderTocBlocks();
			for (var pass = 0; pass < 20; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}
			var after = Dump();
			// 序号诊断：直接调 WithNumbers，验证编号计算与 renderer 前缀条件
			var items = new System.Collections.Generic.List<CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownOutlineItem>
			{
				new(1, "A", 1, 0),
				new(2, "B", 2, 10),
			};
			var numbered = CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownOutlineNumbering.WithNumbers(items.ToArray());
			var numbers = string.Join(",", numbered.Select(n => n.Level + "=" + n.Number));
			var report = "before=[" + before + "]" + Environment.NewLine
				+ "after=[" + after + "]" + Environment.NewLine
				+ "showProp=" + viewer.ShowTocNumbers
				+ " numbers=[" + numbers + "]";
			System.IO.File.WriteAllText(ProbeDumpPath("toc-number-probe.txt"), report);
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	/// <summary>诊断转储落仓内 gitignored artifacts——不绑机器私有盘符（旧实现写死 E:\，无该盘的机器必失败）。</summary>
	private static string ProbeDumpPath(string fileName,
		[System.Runtime.CompilerServices.CallerFilePath] string testFile = "")
	{
		var artifacts = System.IO.Path.GetFullPath(
			System.IO.Path.Combine(System.IO.Path.GetDirectoryName(testFile)!, "..", "..", "artifacts"));
		System.IO.Directory.CreateDirectory(artifacts);
		return System.IO.Path.Combine(artifacts, fileName);
	}
}
