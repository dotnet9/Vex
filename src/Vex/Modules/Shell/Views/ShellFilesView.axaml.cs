using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellFilesView : UserControl
{
    public ShellFilesView()
    {
        InitializeComponent();
    }

    private void SelectItemOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TreeView treeView)
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

        var item = source.FindAncestorOfType<TreeViewItem>();
        if (item?.DataContext is DocumentFileNode documentFileNode
            && DataContext is ShellFilesViewModel viewModel)
        {
            viewModel.SelectDocumentFileForContextMenu(documentFileNode);
        }
    }
}
