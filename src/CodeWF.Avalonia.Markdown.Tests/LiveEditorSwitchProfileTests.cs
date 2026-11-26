using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Threading;

using CodeWF.Avalonia.Markdown.Editor.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 即时编辑模式切文件耗时诊断：用户默认即时编辑，切换文件走 LiveEditor.SetText
/// （与阅读视图 MarkdownViewer 完全不同的管线）。分别量 docs 目录中小/巨型文档
/// 的 SetText 墙钟时间，定位顿挫是否在即时编辑路径。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class LiveEditorSwitchProfileTests(AvaloniaPlatformFixture platform)
{
	private static readonly string DocsDir = ResolveRepositoryPath("docs");

	private static string? LoadDoc(string fileName) =>
		System.IO.Path.Combine(DocsDir, fileName) is { } path && System.IO.File.Exists(path)
			? System.IO.File.ReadAllText(path)
			: null;

	/// <summary>仓库内路径随测试源码定位解析，不绑定具体盘符/检出位置。</summary>
	private static string ResolveRepositoryPath(string relativePath,
		[System.Runtime.CompilerServices.CallerFilePath] string testFile = "") =>
		System.IO.Path.GetFullPath(System.IO.Path.Combine(
			System.IO.Path.GetDirectoryName(testFile)!, "..", "..", relativePath));

	[Fact]
	public void SetText_Profile_OnRealDocs() => platform.Run(() =>
	{
		var view = new MarkdownLiveEditorView();
		var window = new Window { Width = 900, Height = 650, Content = view };
		window.Show();
		try
		{
			Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

			// 预热
			view.SetText("# 预热\n\n预热段落内容。");
			for (var pass = 0; pass < 20; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}

			var report = new System.Text.StringBuilder();
			foreach (var (name, text) in new[]
			         {
				         ("测试文档.md", LoadDoc("测试文档.md")),
				         ("Markdown功能示例.md", LoadDoc("Markdown功能示例.md")),
			         })
			{
				Assert.NotNull(text);

				// ① SetText 本体（同步）：解析 + 全部块模型 + 块视图构建。
				var clock = Stopwatch.StartNew();
				view.SetText(text);
				clock.Stop();
				var setTextMs = clock.ElapsedMilliseconds;

				// ② 布局收敛（首帧可见）。
				var layoutClock = Stopwatch.StartNew();
				for (var pass = 0; pass < 40; pass++)
				{
					Dispatcher.UIThread.RunJobs();
					window.UpdateLayout();
				}
				layoutClock.Stop();

				report.AppendLine($"[{name}] 行={text!.Split('\n').Length} SetText={setTextMs}ms 布局收敛={layoutClock.ElapsedMilliseconds}ms");
			}

			// 基准报告写入仓库 artifacts（已 gitignore），路径随测试源码定位解析。
			var reportPath = System.IO.Path.Combine(ResolveRepositoryPath(
				System.IO.Path.Combine("artifacts", "liveeditor-profile-report.txt")));
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath)!);
			System.IO.File.WriteAllText(reportPath, report.ToString());
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});
}
