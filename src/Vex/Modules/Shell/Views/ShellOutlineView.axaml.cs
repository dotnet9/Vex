using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Vex.Core.Models;

using Microsoft.Extensions.DependencyInjection;
using Vex.Modules.Shell.ViewModels;
namespace Vex.Modules.Shell.Views;

public partial class ShellOutlineView : UserControl
{
    private void ClosePanelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow window || window.DataContext is not MainWindowViewModel shell) return;
        shell.Layout.ToggleOutline();
        window.Workspace.Navigation.FindControl<Avalonia.Controls.Primitives.ToggleButton>("RailOutlineToggle")?.Focus();
    }

    public ShellOutlineView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<ShellOutlineViewModel>();
        OutlineList.AddHandler(PointerReleasedEvent, OutlinePointerReleased, RoutingStrategies.Bubble, true);
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is ShellOutlineViewModel vm) vm.PropertyChanged += OutlineStateChanged;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (DataContext is ShellOutlineViewModel vm) vm.PropertyChanged -= OutlineStateChanged;
        };
    }

    private void OutlineStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellOutlineViewModel.CurrentOutlineItem)
            || DataContext is not ShellOutlineViewModel { CurrentOutlineItem: { } item } vm) return;
        // 用户正在浏览大纲时不挪动鼠标下的行。
        if (OutlineList.IsPointerOver || OutlineList.IsKeyboardFocusWithin) return;
        var visible = vm.FilteredOutlineItems.LastOrDefault(candidate => candidate.Line <= item.Line);
        if (visible is not null) OutlineList.ScrollIntoView(visible);
    }

    private void OutlinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || DataContext is not ShellOutlineViewModel vm) return;
        var row = (e.Source as Avalonia.Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (row?.DataContext is OutlineItem item) vm.NavigateToOutlineItem(item);
    }

    private void OutlineKeyDown(object? sender, KeyEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is not OutlineItem item) return;
        if (e.Key is Key.Enter or Key.Space && DataContext is ShellOutlineViewModel vm)
        {
            vm.NavigateToOutlineItem(item);
            e.Handled = true;
            return;
        }
        if (!item.HasChildren) return;
        if (e.Key is not (Key.Left or Key.Right)) return;
        item.IsExpanded = e.Key == Key.Right;
        e.Handled = true;
    }
}
