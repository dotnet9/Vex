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
        if (sender is not ListBox listBox)
        {
            return;
        }

        var point = e.GetCurrentPoint(listBox);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (e.Source is not Control source)
        {
            return;
        }

        var item = source.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is DocumentFile documentFile
            && DataContext is ShellFilesViewModel viewModel)
        {
            viewModel.SelectDocumentFileForContextMenu(documentFile);
        }
    }
}
