using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Threading;

using CodeWF.Avalonia.Markdown.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 换文档分帧建块回归：首屏同步只建前 ChunkedBuildSyncBlocks 块（切换不再一次性重建
/// 整篇控件树——顿挫感的来源），其余按 Background 优先级分帧续建且保序补齐；
/// 同文档重渲染（主题/字号/缩放）保持同步整建；滚动插队推进游标不重复不乱序；
/// 首帧建块开销有界（性能语义：切文档首帧成本显著低于整篇同步建）。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerChunkedRenderTests(AvaloniaPlatformFixture platform)
{
	private const int ParagraphCount = 300;
	private const int SyncBlocks = 40;
	private const int ChunkSize = 24;

	private static string BuildDocument() => string.Join("\n\n",
		Enumerable.Range(0, ParagraphCount).Select(i => $"Paragraph {i} with enough body text to occupy layout space."));

	private static int BuiltCount(MarkdownViewer viewer) =>
		((System.Collections.IList)typeof(MarkdownViewer).GetField("_renderedBlocks",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
		.GetValue(viewer)!).Count;

	private static (int Generation, int NextModelIndex, bool Active) ChunkState(MarkdownViewer viewer)
	{
		var type = typeof(MarkdownViewer);
		const System.Reflection.BindingFlags privateInstance =
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var generation = (int)(type.GetField("_chunkedBuildGeneration", privateInstance)!.GetValue(viewer) ?? 0);
		var nextIndex = (int)(type.GetField("_chunkedBuildNextModelIndex", privateInstance)!.GetValue(viewer) ?? 0);
		var active = (bool)(type.GetField("_chunkedBuildActive", privateInstance)!.GetValue(viewer) ?? false);
		return (generation, nextIndex, active);
	}

	private static string PlainTextAt(MarkdownViewer viewer, int index)
	{
		var blocks = (System.Collections.IList)typeof(MarkdownViewer).GetField("_renderedBlocks",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.GetValue(viewer)!;
		return (string)blocks[index]!.GetType().GetProperty("PlainText")!.GetValue(blocks[index])!;
	}

	private static void Pump(Window window, int passes = 40)
	{
		for (var pass = 0; pass < passes; pass++)
		{
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();
		}
	}

	[Fact]
	public void DocumentSwitch_BuildsFirstChunkSync_ThenCompletesInOrderInBackground() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer();
		var window = new Window { Width = 500, Height = 420, Content = viewer };
		window.Show();
		try
		{
			Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

			viewer.Markdown = BuildDocument();
			// 只推进 Render 优先级一拍：同步首屏（40 块）已建，Background 续建帧尚未跑。
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
			window.UpdateLayout();
			Assert.Equal(SyncBlocks, BuiltCount(viewer));
			var (_, nextIndex, active) = ChunkState(viewer);
			Assert.True(active);
			Assert.Equal(SyncBlocks, nextIndex);

			Pump(window);
			Assert.Equal(ParagraphCount, BuiltCount(viewer));
			var (_, nextAfter, activeAfter) = ChunkState(viewer);
			Assert.False(activeAfter);
			Assert.Equal(ParagraphCount, nextAfter);

			// 保序：首块与末块的纯文本对应首尾段落。
			Assert.Contains("Paragraph 0", PlainTextAt(viewer, 0));
			Assert.Contains($"Paragraph {ParagraphCount - 1}", PlainTextAt(viewer, ParagraphCount - 1));
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	[Fact]
	public void SameDocumentRerender_BuildsEverythingSynchronously() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer();
		var window = new Window { Width = 500, Height = 420, Content = viewer };
		window.Show();
		try
		{
			Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
			viewer.Markdown = BuildDocument();
			Pump(window);
			Assert.Equal(ParagraphCount, BuiltCount(viewer));

			// 同文档重渲染（如主题/字号变化触发的整建）：同步全量，无分帧。
			viewer.Rerender();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
			window.UpdateLayout();
			Assert.Equal(ParagraphCount, BuiltCount(viewer));
			var (_, _, active) = ChunkState(viewer);
			Assert.False(active);
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	[Fact]
	public void ScrollBoost_AdvancesBuildImmediately_WithoutDuplicateOrReorder() => platform.Run(() =>
	{
		var viewer = new MarkdownViewer();
		var window = new Window { Width = 500, Height = 420, Content = viewer };
		window.Show();
		try
		{
			Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
			viewer.Markdown = BuildDocument();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
			window.UpdateLayout();
			Assert.Equal(SyncBlocks, BuiltCount(viewer));

			// 滚动插队：直接推进一个同步切片（与滚动事件处理器同款语义，游标即互斥）。
			typeof(MarkdownViewer).GetMethod("BuildNextChunk",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
				.Invoke(viewer, [ChunkState(viewer).Generation, ChunkSize]);
			var boosted = BuiltCount(viewer);
			Assert.True(boosted > SyncBlocks, $"boost expected >{SyncBlocks}, got {boosted}");
			Assert.Contains($"Paragraph {boosted - 1}", PlainTextAt(viewer, boosted - 1));

			Pump(window);
			Assert.Equal(ParagraphCount, BuiltCount(viewer));
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	[Fact]
	public void HugeDocument_FirstSyncBuildIsBounded_RegardlessOfSize() => platform.Run(() =>
	{
		// 性能语义（确定性断言，不含计时噪声）：分帧路径的首帧同步工作量有界——
		// 无论文档多大，第一帧只建 ChunkedBuildSyncBlocks 块，切换不再有 O(整篇) 的同步停顿。
var document = string.Join(Environment.NewLine + Environment.NewLine,
			Enumerable.Range(0, 1500)
			.Select(i => $"Paragraph {i} with enough body text to occupy layout space."));
		var viewer = new MarkdownViewer();
		var window = new Window { Width = 800, Height = 600, Content = viewer };
		window.Show();
		try
		{
			Dispatcher.UIThread.RunJobs(); window.UpdateLayout();

			viewer.Markdown = document;
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
			window.UpdateLayout();
			Assert.Equal(SyncBlocks, BuiltCount(viewer));

			// 分帧续建补齐全量，且保序。
			Pump(window, 400);
			Assert.Equal(1500, BuiltCount(viewer));
			Assert.Contains("Paragraph 0", PlainTextAt(viewer, 0));
			Assert.Contains("Paragraph 1499", PlainTextAt(viewer, 1499));
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});
}
