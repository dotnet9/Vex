using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellUnsavedDialogView : UserControl
{
    public ShellUnsavedDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyFlow();
    }

    /// <summary>退出流与切换流共用参考图文案，仅丢弃按钮标签不同（丢弃 / 放弃更改）；
    /// 两流在原型中分别实测为 36/32 高按钮、8/12 卡片圆角、13/12.5 正文，这里按 Flow 同步切换。</summary>
    private void ApplyFlow()
    {
        if (DataContext is not ShellUnsavedDialogViewModel model) return;
        var exit = model.Flow == ShellUnsavedFlow.Exit;
        ExitActions.IsVisible = exit; SwitchActions.IsVisible = !exit;
        DialogCard.CornerRadius = new CornerRadius(exit ? 8 : 12);
        TitleText.FontSize = exit ? 16 : 15.5;
        TitleText.LineHeight = exit ? 25.6 : 24.8;
        HintLine1.FontSize = HintLine2.FontSize = exit ? 13 : 12.5;
        HintLine1.LineHeight = HintLine2.LineHeight = exit ? 23.4 : 20;
        BodyPanel.Margin = new Thickness(0, 10, 0, exit ? 6 : 0);
        ActionsGrid.Margin = new Thickness(0, exit ? 14 : 18, 0, 0);
        var height = exit ? 36.0 : 32.0;
        CancelButton.Height = height;
        CancelButton.CornerRadius = new CornerRadius(height / 2);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Cancel());

    private void OnDiscardClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Discard());

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Complete(vm => vm.Save());

    private void Complete(Action<ShellUnsavedDialogViewModel> action)
    {
        if (DataContext is ShellUnsavedDialogViewModel viewModel)
        {
            action(viewModel);
        }
    }
}
