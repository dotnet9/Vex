using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Vex.Modules.Git.ViewModels;

namespace Vex.Modules.Git.Views;

/// <summary>Git 管理面板（原型 git-panel.js 的正式实现）。交互全部走绑定，这里只留接线。</summary>
public partial class ShellGitView : UserControl
{
    private ListBox? _historyList;

    /// <summary>顶栏两个下拉的 Flyout（Flyout 非 Control，FindControl 取不到；经按钮的 Flyout 属性取）。</summary>
    private Flyout? RepoFlyout => RepoButton?.Flyout as Flyout;
    private Flyout? BranchFlyout => BranchButton?.Flyout as Flyout;

    public ShellGitView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ShellGitViewModel vm) vm.TagFlyoutRequested -= OnTagFlyoutRequested;
            if (DataContext is ShellGitViewModel vm2) vm2.TagFlyoutRequested += OnTagFlyoutRequested;
        };
        AttachedToVisualTree += (_, _) => WireHistoryScrollLoad();
    }

    /// <summary>Git 菜单/历史右键请求：在分支芯片上展开 tag 表单。</summary>
    private void OnTagFlyoutRequested(object? sender, EventArgs args)
    {
        ShowTagFlyoutAtBranchButton();
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

    /// <summary>分支芯片 tag 表单（历史右键「打 tag…/推送 tag…」与 Git 菜单入口共用；tag 打在选中提交上）。</summary>
    private void ShowTagFlyoutAtBranchButton()
    {
        FlyoutBase.GetAttachedFlyout(BranchButton)?.ShowAt(BranchButton);
    }

    /// <summary>历史提交右键：打 tag…（tag 打在选中提交上）。</summary>
    private void TagOnCommit_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ShellGitViewModel vm || vm.SelectedCommit is not { } commit) return;
        vm.PrepareTagFlyout(alsoPush: false, targetSha: commit.Sha);
        ShowTagFlyoutAtBranchButton();
    }

    /// <summary>历史提交右键：推送 tag…（预勾选同时推送；tag 打在选中提交上）。</summary>
    private void PushTagOnCommit_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ShellGitViewModel vm || vm.SelectedCommit is not { } commit) return;
        vm.PrepareTagFlyout(alsoPush: true, targetSha: commit.Sha);
        ShowTagFlyoutAtBranchButton();
    }

    /// <summary>仓库下拉：切换已打开仓库（把 Git 面板重绑到该仓库根并刷新）。</summary>
    private void RepoListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is not ShellGitViewModel vm) return;
        if (args.AddedItems.OfType<GitRepoRow>().FirstOrDefault() is { } repo && !repo.IsCurrent)
        {
            _ = vm.SwitchRepoAsync(repo.Path);
            (RepoButton.Flyout as Flyout)?.Hide();
        }
    }

    /// <summary>分支下拉筛选框：过滤词驱动 VM 重建分组行。</summary>
    private void BranchFilterBox_OnTextChanged(object? sender, TextChangedEventArgs args)
    {
        if (DataContext is ShellGitViewModel vm && sender is TextBox box) vm.BranchFilter = box.Text ?? string.Empty;
    }

    /// <summary>分支下拉：点击分支行切换检出（分组头行不可选中）。</summary>
    private void BranchListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is not ShellGitViewModel vm) return;
        if (args.AddedItems.OfType<GitBranchRow>().FirstOrDefault(row => !row.IsGroupHeader) is { } row)
        {
            vm.SelectedBranchRow = row;
            _ = vm.SwitchBranchAsync(row.Name);
            (BranchButton.Flyout as Flyout)?.Hide();
        }
    }

    /// <summary>分支下拉行右键：重命名…（弹层预填旧名）。</summary>
    private void RenameBranchRow_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is ShellGitViewModel vm && vm.SelectedBranchRow is { } row)
        {
            _ = vm.RenameBranchFromDialogAsync(row.Name);
        }
    }

    /// <summary>分支下拉行右键：复制分支名（剪贴板 + toast）。</summary>
    private async void CopyBranchName_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ShellGitViewModel vm || vm.SelectedBranchRow is not { } row) return;
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } && window.Clipboard is { } clip)
        {
            await clip.SetTextAsync(row.Name);
        }
        vm.CopyBranchNameToast(row.Name);
    }

    /// <summary>分支下拉行右键：删除…（确认弹层；当前分支服务端拒绝并 toast）。</summary>
    private void DeleteBranchRow_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is ShellGitViewModel vm && vm.SelectedBranchRow is { } row)
        {
            _ = vm.DeleteBranchFromDialogAsync(row.Name);
        }
    }
}
