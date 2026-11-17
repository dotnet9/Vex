using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>
/// 命令面板视图（F1）：VM 由宿主注入；本控件只承担键盘循环与开关。
/// 面板常驻可视树（IsVisible 切换），不走 Popup——命中测试遮罩点击即关。
/// </summary>
public partial class ShellCommandPaletteView : UserControl
{
    public ShellCommandPaletteView()
    {
        InitializeComponent();
        IsVisible = false;
        QueryInput.AddHandler(KeyDownEvent, OnQueryKeyDown, RoutingStrategies.Tunnel);
        // 单击条目即执行（原型 cp-item click 同款）：Tapped 在容器层捕获，点击即关面板跑命令。
        ResultsList.AddHandler(TappedEvent, OnResultsTapped, RoutingStrategies.Bubble);
    }

    private void OnResultsTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source
            && source.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault() is { } item
            && item.DataContext is ShellCommandItem command)
        {
            IsOpen = false;
            command.Execute();
            e.Handled = true;
        }
    }

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ShellCommandPaletteView, bool>(nameof(IsOpen), false);

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set
        {
            SetValue(IsOpenProperty, value);
            IsVisible = value;
            if (value && DataContext is ShellCommandPaletteViewModel model) model.Reset();
            if (value) QueryInput.Focus();
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            (DataContext as ShellCommandPaletteViewModel)?.Move(e.Key == Key.Down ? 1 : -1);
            return;
        }
        if (e.Key is Key.Home or Key.End)
        {
            e.Handled = true;
            if (DataContext is ShellCommandPaletteViewModel model && model.Results.Count > 0)
                model.Selected = model.Results[e.Key == Key.Home ? 0 : model.Results.Count - 1];
            return;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            CloseWithExecute();
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private void Backdrop_OnTapped(object? sender, TappedEventArgs e) => IsOpen = false;

    private void CloseWithExecute()
    {
        if (DataContext is ShellCommandPaletteViewModel model)
        {
            var executed = model.ExecuteSelected();
            IsOpen = false;
            _ = executed;
        }
        else IsOpen = false;
    }
}
