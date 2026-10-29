using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellQuickOpenView : UserControl
{
    public ShellQuickOpenView()
    {
        InitializeComponent();
        // 浮层宿主会在挂载后把焦点抢给自身 Border，直接 Focus 会被夺走；等它落位后再补一次（同重命名框）。
        AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => QueryBox.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private ShellQuickOpenViewModel? ViewModel => DataContext as ShellQuickOpenViewModel;

    private void QueryBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                ViewModel?.MoveSelection(1);
                ScrollSelectionIntoView();
                break;
            case Key.Up:
                e.Handled = true;
                ViewModel?.MoveSelection(-1);
                ScrollSelectionIntoView();
                break;
            case Key.Enter:
                e.Handled = true;
                ViewModel?.Confirm();
                break;
            case Key.Escape:
                e.Handled = true;
                ViewModel?.Cancel();
                break;
        }
    }

    private void ScrollSelectionIntoView()
    {
        if (ViewModel?.SelectedItem is { } item)
        {
            ResultsList.ScrollIntoView(item);
        }
    }

    private void Results_OnDoubleTapped(object? sender, RoutedEventArgs e) => ViewModel?.Confirm();

    private void Open_OnClick(object? sender, RoutedEventArgs e) => ViewModel?.Confirm();

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => ViewModel?.Cancel();
}
