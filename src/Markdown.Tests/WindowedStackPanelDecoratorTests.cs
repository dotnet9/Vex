using CodeWF.Avalonia.Markdown.Tests;
using CodeWF.Avalonia.Markdown.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Headless;
using global::Avalonia.Threading;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 宿主绕过登记 API、直接往 <c>Children</c> 里塞子项（表格上方的工具条就是这么挂的）时，
/// 面板必须把这块装饰当作所属条目的附加高度来排。
/// 此前它会让排位游标与条目下标错位一位：每个块拿到邻居的高度，正文最后一块干脆没人排，
/// 被前一块直接盖住——阅读视图里看到的就是「段落被截断」。
/// 两个互为副本的 WindowedStackPanel 用同一组断言跑一遍，顺带钉住两侧行为一致。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class WindowedStackPanelDecoratorTests(AvaloniaPlatformFixture platform)
{
    public static TheoryData<string> HostNames { get; } = new(["markdown", "controls"]);

    [Theory]
    [MemberData(nameof(HostNames))]
    public void ShortContentKeepsItsNaturalWidthInsideAnAutoColumn(string hostName) => platform.Run(() =>
    {
        var panel = CreatePanel(hostName);
        panel.AddItem(new TextBlock { Text = "Natural width for table-cell content", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(panel.View);
        var window = new Window { Width = 500, Height = 300, Content = grid };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.InRange(panel.View.Bounds.Width, 100, 500);
            Assert.True(panel.View.DesiredSize.Height < 100);
        }
        finally { window.Close(); }
    });

    [Theory]
    [MemberData(nameof(HostNames))]
    public void UnregisteredChildSharesItsBandInsteadOfShiftingEveryLaterBlock(string hostName) => platform.Run(() =>
    {
        var panel = CreatePanel(hostName);
        var blocks = new List<Control>();
        for (var i = 1; i <= 4; i++)
        {
            var block = new Border { Height = i * 20 };
            blocks.Add(block);
            panel.AddItem(block);
        }

        var toolbar = new Border { Height = 12 };
        panel.View.Children.Insert(1, toolbar);

        var presenter = new ScrollContentPresenter { Content = panel.View };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };

        var window = new Window { Width = 300, Height = 100, Content = scroll };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        try
        {
            // 装饰不占条目：宿主的「块号 ↔ 条目号」对齐不能因为多挂了一个控件就整体后移。
            Assert.Equal(4, panel.ItemCount);
            Assert.Equal(0, blocks[0].Bounds.Y);
            Assert.Equal(20, toolbar.Bounds.Y);
            Assert.Equal(32, blocks[1].Bounds.Y);
            Assert.Equal(72, blocks[2].Bounds.Y);
            Assert.Equal(132, blocks[3].Bounds.Y);
            Assert.Equal(212, panel.View.DesiredSize.Height);
            Assert.Equal(52, panel.GetItemHeight(1));
            Assert.Equal(72, panel.GetItemTop(2));
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Theory]
    [MemberData(nameof(HostNames))]
    public void TrailingUnregisteredChildLandsBelowTheLastBlock(string hostName) => platform.Run(() =>
    {
        var panel = CreatePanel(hostName);
        var first = new Border { Height = 30 };
        var second = new Border { Height = 40 };
        panel.AddItem(first);
        panel.AddItem(second);
        var footnote = new Border { Height = 15 };
        panel.View.Children.Add(footnote);

        var presenter = new ScrollContentPresenter { Content = panel.View };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };

        var window = new Window { Width = 300, Height = 100, Content = scroll };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal(0, first.Bounds.Y);
            Assert.Equal(30, second.Bounds.Y);
            // 尾巴上没有别的条目可依附，就排在最后一块之下，并计入总高。
            Assert.Equal(70, footnote.Bounds.Y);
            Assert.Equal(85, panel.View.DesiredSize.Height);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    private static IPanelAdapter CreatePanel(string hostName) => hostName switch
    {
        "markdown" => new MarkdownAdapter(new global::CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel()),
        _ => new ControlsAdapter(new global::CodeWF.Avalonia.Controls.Controls.WindowedStackPanel()),
    };

    private interface IPanelAdapter
    {
        Panel View { get; }

        int ItemCount { get; }

        void AddItem(Control control);

        double GetItemHeight(int index);

        double GetItemTop(int index);
    }

    private sealed class MarkdownAdapter(global::CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel panel) : IPanelAdapter
    {
        public Panel View => panel;

        public int ItemCount => panel.ItemCount;

        public void AddItem(Control control) => panel.AddItem(control);

        public double GetItemHeight(int index) => panel.GetItemHeight(index);

        public double GetItemTop(int index) => panel.TryGetItemTop(index, out var top) ? top : double.NaN;
    }

    private sealed class ControlsAdapter(global::CodeWF.Avalonia.Controls.Controls.WindowedStackPanel panel) : IPanelAdapter
    {
        public Panel View => panel;

        public int ItemCount => panel.ItemCount;

        public void AddItem(Control control) => panel.AddItem(control);

        public double GetItemHeight(int index) => panel.GetItemHeight(index);

        public double GetItemTop(int index) => panel.TryGetItemTop(index, out var top) ? top : double.NaN;
    }
}
