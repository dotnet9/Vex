using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

using CodeWF.Avalonia.Markdown.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 快拖骨架模式回归：连续大步滚动进入骨架（物化集合冻结 + FastDragStateChanged 通知），
/// 停顿后退出并重算窗口；孤立大跳不进骨架（程序化跳转立即生效）。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class WindowedStackPanelFastDragTests(AvaloniaPlatformFixture platform)
{
	private static (WindowedStackPanel Panel, ScrollViewer Scroll, Window Window) Create()
	{
		var panel = new WindowedStackPanel { EstimatedItemHeight = 42, VirtualizationThreshold = 1 };
		var presenter = new ScrollContentPresenter { Content = panel };
		var scroll = new ScrollViewer
		{
			Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
			{
				scope.Register("PART_ScrollContentPresenter", presenter);
				return presenter;
			}),
		};
		var window = new Window { Width = 640, Height = 360, Content = scroll };
		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		return (panel, scroll, window);
	}

	[Fact]
	public void ConsecutiveJumps_EntersSkeleton_FreezesRealization_ThenResumes() => platform.Run(() =>
	{
		var (panel, scroll, window) = Create();
		try
		{
			for (var i = 0; i < 300; i++)
			{
				panel.AddItem(new Border { Height = 40 });
			}
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();

			var stateChanges = 0;
			panel.FastDragStateChanged += (_, _) => stateChanges++;

			// 连续两次大步（>2 视口高、间隔 <150ms）→ 进入骨架。
			scroll.Offset = new Vector(0, 2000);
			Dispatcher.UIThread.RunJobs();
			scroll.Offset = new Vector(0, 6000);
			Dispatcher.UIThread.RunJobs();

			Assert.True(panel.IsFastDragging, "连续大步应进入骨架模式");
			Assert.True(stateChanges >= 1, "应通知骨架状态变化");

			// 停顿超过 150ms 后下一次滚动事件 → 退出骨架（headless 模拟时钟不推进
			// DispatcherTimer，退出由 scroll 处理器的墙钟检查完成，与真实拖动一致）。
			Thread.Sleep(200);
			scroll.Offset = new Vector(0, 7000);
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();
			Assert.False(panel.IsFastDragging, "停顿后应退出骨架模式");
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});

	[Fact]
	public void IsolatedJump_DoesNotEnterSkeleton() => platform.Run(() =>
	{
		var (panel, scroll, window) = Create();
		try
		{
			for (var i = 0; i < 300; i++)
			{
				panel.AddItem(new Border { Height = 40 });
			}
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();

			// 孤立大跳（间隔 >150ms）：程序化跳转语义，立即重算窗口，不进骨架。
			scroll.Offset = new Vector(0, 2000);
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();
			Thread.Sleep(200);
			scroll.Offset = new Vector(0, 8000);
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();

			Assert.False(panel.IsFastDragging, "孤立大跳不应进入骨架模式");
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
	});
}
