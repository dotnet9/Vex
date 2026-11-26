using CodeWF.Avalonia.Markdown.Controls;

using System.Diagnostics;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 长文档阅读视图「虚拟化开 / 关」加载剖析：量首屏（视口全覆盖且内容就绪）墙钟、
/// 首屏物化数与整篇物化数，报告写入仓库 artifacts（已 gitignore）供基准摘录。
/// 断言只钉确定性结论（开的物化数远小于总块数、关的等于总块数、两者视口都全覆盖），
/// 墙钟只打印不断言——时间在 CI 上会抖，拿它做门槛必然制造不稳定测试。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerVirtualizationProfileTests(AvaloniaPlatformFixture platform)
{
	private static readonly string DocsDir = ResolveRepositoryPath("docs");
	private static readonly string ArtifactsDir = ResolveRepositoryPath("artifacts");

	private static string? LoadDoc(string fileName) =>
		System.IO.Path.Combine(DocsDir, fileName) is { } path && System.IO.File.Exists(path)
			? System.IO.File.ReadAllText(path)
			: null;

	/// <summary>仓库内路径随测试源码定位解析，不绑定具体盘符/检出位置。</summary>
	private static string ResolveRepositoryPath(string relativePath,
		[System.Runtime.CompilerServices.CallerFilePath] string testFile = "") =>
		System.IO.Path.GetFullPath(System.IO.Path.Combine(
			System.IO.Path.GetDirectoryName(testFile)!, "..", "..", relativePath));

	public static System.Collections.Generic.IEnumerable<object?[]> LargeDocuments()
	{
		yield return [LoadDoc("Markdown功能示例.md")];
	}

	[Theory]
	[MemberData(nameof(LargeDocuments))]
	public void FirstViewportReady_VirtualizationOnVersusOff(string? text)
	{
		Assert.False(string.IsNullOrEmpty(text), "仓库基准文档 docs/Markdown功能示例.md 缺失");
		var report = new System.Text.StringBuilder();
		var (onReadyMs, onRealized, onTotal) = Measure(text!, virtualized: true, report);
		var (offReadyMs, offRealized, offTotal) = Measure(text!, virtualized: false, report);
		report.Insert(0, $"[虚拟化剖析] 字符={text!.Length:N0} 虚拟化开: 首屏={onReadyMs:F0}ms 物化={onRealized}/{onTotal} | "
			+ $"虚拟化关: 首屏={offReadyMs:F0}ms 物化={offRealized}/{offTotal}");

		Assert.True(onTotal > 200 && offTotal == onTotal, $"两种模式解析同一文档：on={onTotal} off={offTotal}");
		Assert.True(onRealized < onTotal / 2,
			$"虚拟化开首屏只应物化视口附近条目：{onRealized}/{onTotal}");
		Assert.True(offRealized == offTotal,
			$"虚拟化关必须整篇物化：{offRealized}/{offTotal}");

		System.IO.Directory.CreateDirectory(ArtifactsDir);
		System.IO.File.WriteAllText(
			System.IO.Path.Combine(ArtifactsDir, "virtualization-profile-report.txt"), report.ToString());
		Console.WriteLine(report.ToString());
	}

	private (double ReadyMs, int Realized, int Total) Measure(string text, bool virtualized, System.Text.StringBuilder report)
	{
		return platform.Run(() =>
		{
			var viewer = new MarkdownViewer { EnableVirtualization = virtualized, Markdown = text };
			var presenter = new ScrollContentPresenter { Content = viewer };
			var scroll = new ScrollViewer
			{
				Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
				{
					scope.Register("PART_ScrollContentPresenter", presenter);
					return presenter;
				})
			};
			var window = new Window { Width = 1100, Height = 800, Content = scroll };
			window.Show();
			var watch = Stopwatch.StartNew();
			var deadline = watch.ElapsedMilliseconds + 90_000;
			try
			{
				// 就绪 = 与视口相交的每一块都已物化且内容就绪（非空延迟块）。Background 优先级的
				// 分帧续建会被 RunJobs 一并泵出，因此「关」的首屏墙钟≈整篇建块的同步成本。
				while (true)
				{
					Dispatcher.UIThread.RunJobs();
					window.UpdateLayout();
					if (ViewportReady(viewer, scroll)) break;
					if (watch.ElapsedMilliseconds > deadline)
						throw new Xunit.Sdk.XunitException($"virtualization={virtualized}: viewport not ready in 90s");
					Thread.Sleep(5);
				}

				var readyMs = watch.ElapsedMilliseconds;
				var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
				report.AppendLine($"    [{(virtualized ? "虚拟化开" : "虚拟化关")}] 首屏就绪={readyMs}ms "
					+ $"物化={panel.RealizedItemCount}/{panel.ItemCount} extent={scroll.Extent.Height:F0}");
				return (readyMs, panel.RealizedItemCount, panel.ItemCount);
			}
			finally
			{
				window.Close();
				Dispatcher.UIThread.RunJobs();
			}
		});
	}

	private static bool ViewportReady(MarkdownViewer viewer, ScrollViewer scroll)
	{
		if (scroll.Viewport.Height <= 0 || scroll.Extent.Height <= scroll.Viewport.Height) return false;
		var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().SingleOrDefault();
		if (panel is null) return false;
		var viewportTop = scroll.Offset.Y;
		var viewportBottom = viewportTop + scroll.Viewport.Height;
		for (var index = 0; index < panel.ItemCount; index++)
		{
			if (!panel.TryGetItemTop(index, out var top)) return false;
			if (top + panel.GetItemHeight(index) <= viewportTop || top >= viewportBottom) continue;
			// 相交块必须已物化；虚拟化路径的延迟块还必须已创建内容（全量路径块不是 Decorator）。
			if (panel.GetRealizedItem(index) is not Decorator { Child: not null }
				&& panel.GetRealizedItem(index) is Decorator) return false;
		}

		return true;
	}
}
