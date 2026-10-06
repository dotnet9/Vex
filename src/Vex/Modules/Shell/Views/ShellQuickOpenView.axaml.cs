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
        AttachedToVisualTree += (_, _) => QueryBox.Focus();
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
