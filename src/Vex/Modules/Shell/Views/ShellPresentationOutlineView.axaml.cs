using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellPresentationOutlineView : UserControl
{
    public ShellPresentationOutlineView() => InitializeComponent();
    private void ChapterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index } && DataContext is MainWindowViewModel vm)
            vm.Workbench.NavigatePresentation(index);
    }
}
