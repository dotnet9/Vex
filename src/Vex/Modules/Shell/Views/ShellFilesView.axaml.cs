using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using Vex.Controls;

namespace Vex.Modules.Shell.Views;

public partial class ShellFilesView : UserControl
{
    public ShellFilesView()
    {
        InitializeComponent();
        FilesModeIcon.Data = MenuIconCatalog.Get("list-start");
        FilesListModeIcon.Data = MenuIconCatalog.Get("layout-list");
        UpdateToolbarIcons();
        MenuIconCatalog.Apply((ContextMenu)FileTree.ContextMenu!);
        MenuIconCatalog.Apply((ContextMenu)FileList.ContextMenu!);
        // 隧道路由先于 TreeView 默认处理，文件夹行只响应一次左键操作。
        AddHandler(PointerPressedEvent, ToggleFolderOnPointerPressed, RoutingStrategies.Tunnel);
        FileTree.SelectionChanged += RevealSelectedItem;
        FileList.LayoutUpdated += (_, _) => RequestVisibleListPreviews();
        DataContextChanged += (_, _) => ObserveViewModel();
        AttachedToVisualTree += (_, _) => ObserveViewModel();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_observedViewModel is not null) _observedViewModel.PropertyChanged -= ViewModelPropertyChanged;
            _observedViewModel = null;
        };
    }

    private ShellFilesViewModel? _observedViewModel;

    public ShellFilesView(ShellFilesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void ObserveViewModel()
    {
        if (_observedViewModel is not null) _observedViewModel.PropertyChanged -= ViewModelPropertyChanged;
        _observedViewModel = DataContext as ShellFilesViewModel;
        if (_observedViewModel is not null) _observedViewModel.PropertyChanged += ViewModelPropertyChanged;
        UpdateToolbarIcons();
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        UpdateToolbarIcons();
        if (args.PropertyName != nameof(ShellFilesViewModel.PreviewRevision) || sender is not ShellFilesViewModel) return;
        RequestVisibleListPreviews();
    }

    private void UpdateToolbarIcons()
    {
        if (_observedViewModel is not { } viewModel) return;
        FilesSortIcon.Data = MenuIconCatalog.Get(viewModel.IsSortDescending ? "arrow-up-a-z" : "arrow-down-a-z");
        FilesFoldIcon.Data = MenuIconCatalog.Get(viewModel.CanCollapseAll ? "fold-vertical" : "unfold-vertical");
    }

    private async void RetryDirectoryClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is DocumentFileNode node && DataContext is ShellFilesViewModel viewModel)
            await viewModel.RetryDirectoryAsync(node);
        e.Handled = true;
    }

    private bool _previewCheckPending;

    private void RequestVisibleListPreviews()
    {
        if (_previewCheckPending || !FileList.IsEffectivelyVisible || DataContext is not ShellFilesViewModel { IsListView: true }) return;
        _previewCheckPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _previewCheckPending = false;
            if (!FileList.IsEffectivelyVisible || DataContext is not ShellFilesViewModel { IsListView: true } viewModel) return;
            foreach (var item in FileList.GetVisualDescendants().OfType<ListBoxItem>())
            {
                var point = item.TranslatePoint(default(Point), FileList);
                if (item.IsEffectivelyVisible && point is { } origin && origin.Y < FileList.Bounds.Height && origin.Y + item.Bounds.Height > 0
                    && item.DataContext is DocumentFileNode node)
                    _ = viewModel.RequestPreviewAsync(node);
            }
        }, DispatcherPriority.Background);
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
