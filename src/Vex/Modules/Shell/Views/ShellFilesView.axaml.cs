using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellFilesView : UserControl
{
    public ShellFilesView()
    {
        InitializeComponent();
        // 隧道路由先于 TreeView 默认处理，文件夹行只响应一次左键操作。
        AddHandler(PointerPressedEvent, ToggleFolderOnPointerPressed, RoutingStrategies.Tunnel);
        FileTree.SelectionChanged += RevealSelectedItem;
    }

    private void ToggleFolderOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || e.Source is not Control source
            || source.FindAncestorOfType<ToggleButton>() is not null)
        {
            return;
        }

        var item = source as TreeViewItem ?? source.FindAncestorOfType<TreeViewItem>();
        if (item?.DataContext is not DocumentFileNode { IsFolder: true } folder
            || DataContext is not ShellFilesViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedDocumentFileNode = folder;
        item.SetCurrentValue(TreeViewItem.IsExpandedProperty, !item.IsExpanded);
        folder.IsExpanded = item.IsExpanded;
        item.Focus();
        e.Handled = true;
    }

    private void OnFileRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        // 两次单击已各切换一次，阻止 TreeViewItem 的双击默认行为再切换。
        if ((sender as Control)?.DataContext is DocumentFileNode { IsFolder: true }) e.Handled = true;
    }

    private void RevealSelectedItem(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TreeView tree || tree.SelectedItem is not DocumentFileNode selected) return;
        Dispatcher.UIThread.Post(() =>
        {
            var item = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, selected));
            if (item is null || !ReferenceEquals(tree.SelectedItem, selected)) return;
            var header = item.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PART_LayoutRoot");
            header?.BringIntoView();
            if (selected.IsFolder && selected.Path == (DataContext as ShellFilesViewModel)?.DocumentFileNodes.FirstOrDefault()?.Path)
            {
                item.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    private void SelectItemOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control treeView)
        {
            return;
        }

        var point = e.GetCurrentPoint(treeView);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (e.Source is not Control source)
        {
            return;
        }

        var item = (Control?)source.FindAncestorOfType<TreeViewItem>() ?? source.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is DocumentFileNode documentFileNode
            && DataContext is ShellFilesViewModel viewModel)
        {
            viewModel.SelectDocumentFileForContextMenu(documentFileNode);
        }
    }
}
