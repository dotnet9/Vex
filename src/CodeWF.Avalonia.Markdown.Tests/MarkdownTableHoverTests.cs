using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 表格块悬浮交互回归：悬浮出现工具条、移出收起；
/// 显隐全程表格与外框布局零位移——工具条曾占独立 Auto 行，悬浮出现把表格下推、
/// 指针落出网格又收起，形成布局振荡（真机表现为表格不停晃动），现工具条悬浮叠加、不占布局。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownTableHoverTests(AvaloniaPlatformFixture platform)
{
    private static PointerEventArgs RaisePointer(RoutedEvent routedEvent, Control source) =>
        new(routedEvent, source, new Pointer(1, PointerType.Mouse, true), source,
            new Point(source.Bounds.Width / 2, source.Bounds.Height / 2), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None);

    [Fact]
    public void Hover_ShowsTools_And_NeverShiftsLayout() => platform.Run(() =>
    {
        var doc = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        var viewer = new MarkdownViewer { Markdown = doc };
        viewer.TableToolsFactory = (_, _) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { new TextBlock { Text = "复制 Markdown" } },
        };
        var window = new Window { Width = 600, Height = 400, Content = viewer };
        window.Show();
        try
        {
            for (var pass = 0; pass < 30; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var block = viewer.GetVisualDescendants().OfType<MarkdownTableBlock>().FirstOrDefault();
            Assert.NotNull(block);
            var tools = block!.Children[0];
            var table = (Border)block.Children[1];
            Assert.False(tools.IsVisible, "非悬浮态工具条应收起");

            Point TablePos() => table.TranslatePoint(new Point(0, 0), viewer) ?? default;
            var posBefore = TablePos();
            var blockHeightBefore = block.Bounds.Height;

            // 悬浮：工具条出现，且表格位置与外框高度零位移（布局振荡回归守卫）。
            block.RaiseEvent(RaisePointer(InputElement.PointerEnteredEvent, block));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(tools.IsVisible, "悬浮后工具条应出现");
            Assert.Equal(posBefore, TablePos());
            Assert.Equal(blockHeightBefore, block.Bounds.Height);

            // 移出：工具条收起，布局依旧零位移。
            block.RaiseEvent(RaisePointer(InputElement.PointerExitedEvent, block));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.False(tools.IsVisible, "移出后工具条应收起");
            Assert.Equal(posBefore, TablePos());
            Assert.Equal(blockHeightBefore, block.Bounds.Height);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });
}
