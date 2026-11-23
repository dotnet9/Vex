using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Vex.Modules.Git.ViewModels;

namespace Vex.Modules.Git.Views;

/// <summary>Git 管理面板（原型 git-panel.js 的正式实现）。交互全部走绑定，这里只留接线。</summary>
public partial class ShellGitView : UserControl
{
    private ListBox? _historyList;

    public ShellGitView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => WireHistoryScrollLoad();
    }

    /// <summary>历史列表滚动近底部（余 8 行内）自动续拉下一批；重连视觉树只挂一次。</summary>
    private void WireHistoryScrollLoad()
    {
        if (_historyList is not null) return;
        _historyList = this.FindControl<ListBox>("HistoryList");
        if (_historyList is null) return;
        _historyList.AddHandler(ScrollViewer.ScrollChangedEvent, async (_, args) =>
        {
            if (DataContext is not ShellGitViewModel vm || !vm.HasMoreHistory) return;
            if (_historyList.ItemsSource is not ObservableCollection<GitCommitItem> items || items.Count == 0) return;
            var scroller = _historyList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scroller is null || scroller.Extent.Height <= 0) return;
            var nearBottom = scroller.Offset.Y + scroller.Viewport.Height >= scroller.Extent.Height - 8 * 44;
            if (!nearBottom) return;
            await vm.LoadMoreHistoryAsync();
        }, RoutingStrategies.Bubble);
    }

    /// <summary>分支下拉：打 tag…（默认不推送）。</summary>
    private void TagMenu_OnClick(object? sender, RoutedEventArgs args)
    {
        (DataContext as ShellGitViewModel)?.PrepareTagFlyout(alsoPush: false);
        FlyoutBase.GetAttachedFlyout(BranchButton)?.ShowAt(BranchButton);
    }

    /// <summary>分支下拉：推送 tag…（预勾选同时推送）。</summary>
    private void PushTagMenu_OnClick(object? sender, RoutedEventArgs args)
    {
        (DataContext as ShellGitViewModel)?.PrepareTagFlyout(alsoPush: true);
        FlyoutBase.GetAttachedFlyout(BranchButton)?.ShowAt(BranchButton);
    }

    /// <summary>分支下拉：分支管理（桌面版后续提供）。</summary>
    private void BranchManage_OnClick(object? sender, RoutedEventArgs args)
    {
        (DataContext as ShellGitViewModel)?.BranchManage();
    }
}
