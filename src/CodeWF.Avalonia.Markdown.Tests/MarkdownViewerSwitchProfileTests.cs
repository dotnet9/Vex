using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Threading;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 切文件耗时分解诊断（性能测试，断言只做健全性检查，数字输出到测试输出供基准报告摘录）：
/// 分别打开 docs 目录的中小文档与巨型文档（Markdown功能示例.md 8268 行），
/// 量解析、首屏同步建块、分帧续建各阶段与整篇完成的墙钟时间，定位切换顿挫的构成。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerSwitchProfileTests(AvaloniaPlatformFixture platform)
{
	private static readonly string DocsDir = ResolveRepositoryPath("docs");
	private const int SyncBlocks = 40;
	private const int ChunkSize = 24;

	private static readonly System.Reflection.BindingFlags InstancePrivate =
		System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

	private static string? LoadDoc(string fileName) =>
		System.IO.Path.Combine(DocsDir, fileName) is { } path && System.IO.File.Exists(path)
			? System.IO.File.ReadAllText(path)
			: null;

	/// <summary>仓库内路径随测试源码定位解析，不绑定具体盘符/检出位置。</summary>
	private static string ResolveRepositoryPath(string relativePath,
		[System.Runtime.CompilerServices.CallerFilePath] string testFile = "") =>
		System.IO.Path.GetFullPath(System.IO.Path.Combine(
			System.IO.Path.GetDirectoryName(testFile)!, "..", "..", relativePath));

	private static int BuiltCount(MarkdownViewer viewer) =>
		((System.Collections.IList)typeof(MarkdownViewer).GetField("_renderedBlocks",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
		.GetValue(viewer)!).Count;

	private static int ModelCount(MarkdownViewer viewer)
	{
		var model = typeof(MarkdownViewer).GetField("_renderedModel",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(viewer)!;
		return (int)(model.GetType().GetProperty("Blocks")!.GetValue(model)!
			.GetType().GetProperty("Count")!.GetValue(model.GetType().GetProperty("Blocks")!.GetValue(model))!);
	}

	/// <summary>
	/// 泵到建块数量连续稳定（解析在调度器异步完成、分帧续建逐帧推进，以「计数不再变化」
	/// 判定收敛）；返回自 start 的墙钟毫秒。
	/// </summary>
	private static long PumpUntilStable(Window window, MarkdownViewer viewer, Stopwatch clock, int maxSeconds = 30)
	{
		var stable = 0;
		var last = -1;
		while (clock.Elapsed.TotalSeconds < maxSeconds)
		{
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();
			var current = BuiltCount(viewer);
			stable = current == last ? stable + 1 : 0;
			last = current;
			if (stable >= 12)
			{
				break;
			}
		}
		return clock.ElapsedMilliseconds;
	}

	private readonly System.Text.StringBuilder Report = new();

	private static object PeekParse(MarkdownViewer viewer, string text)
	{
		var scheduler = typeof(MarkdownViewer).GetField("_scheduler", InstancePrivate)!.GetValue(viewer)!;
		var parseNow = scheduler.GetType().GetMethod("ParseNow")!;
		return parseNow.Invoke(scheduler, [text, null])!;
	}

	private static System.Collections.IList PeekBuild(MarkdownViewer viewer, object model, string text)
	{
		var method = typeof(MarkdownViewer).GetMethods(InstancePrivate)
			.First(m => m.Name == "CreateRenderedBlocks");
		return (System.Collections.IList)method.Invoke(viewer, [model.GetType().GetProperty("Blocks")!.GetValue(model), text, 0])!;
	}

	[Fact]
	public void SwitchProfile_SmallAndHugeDocs() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer();
		var window = new Window { Width = 900, Height = 650, Content = viewer };
		window.Show();
		try
		{
			Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

			// —— 预热：字体与整形管线（对齐真实使用：应用启动后早有渲染发生） ——
			viewer.Markdown = "# 预热\n\n预热段落内容。";
			for (var pass = 0; pass < 20; pass++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}

			foreach (var (name, text) in new[]
			         {
				         (Name: "测试文档.md", Text: LoadDoc("测试文档.md")),
				         (Name: "Markdown功能示例.md", Text: LoadDoc("Markdown功能示例.md")),
			         })
			{
				Assert.NotNull(text);

				// ① 解析成本（同步 ParseNow，换文档时 UI 线程 blocking 的第一段）。
				var parseClock = Stopwatch.StartNew();
				var parsed = PeekParse(viewer, text);
				parseClock.Stop();

				// ② 全量建块成本（旧路径每次切换一次性付出的部分）。
				var buildClock = Stopwatch.StartNew();
				var rendered = PeekBuild(viewer, parsed!, text);
				buildClock.Stop();

				// ③ 端到端：赋值 → 分帧全部落地稳定。
				var switchClock = Stopwatch.StartNew();
				viewer.Markdown = text;
				PumpUntilStable(window, viewer, switchClock);
				switchClock.Stop();
				var builtNow = BuiltCount(viewer);
				var isQueued = (bool)(typeof(MarkdownViewer).GetField("_isRenderQueued", InstancePrivate)!.GetValue(viewer) ?? false);
				var renderedMarkdown = (string?)typeof(MarkdownViewer).GetField("_renderedMarkdown", InstancePrivate)!.GetValue(viewer);
				Report.AppendLine($"   [诊断] isQueued={isQueued} renderedMarkdown前16={renderedMarkdown?[..Math.Min(16, renderedMarkdown?.Length ?? 0)]} builtNow={builtNow}");

				Report.AppendLine($"[{name}] 行={text!.Split('\n').Length} 块={builtNow} | " +
					$"解析={parseClock.ElapsedMilliseconds}ms 全量建块={buildClock.ElapsedMilliseconds}ms（{rendered.Count} 块） | " +
					$"端到端切换={switchClock.ElapsedMilliseconds}ms");
			}
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}

		// 基准报告写入仓库 artifacts（已 gitignore），路径随测试源码定位解析。
		var reportPath = System.IO.Path.Combine(ResolveRepositoryPath(
			System.IO.Path.Combine("artifacts", "switch-profile-report.txt")));
		System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath)!);
		System.IO.File.WriteAllText(reportPath, Report.ToString());
	});
}
