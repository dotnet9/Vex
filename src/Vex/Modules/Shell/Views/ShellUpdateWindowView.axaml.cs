using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>非模态更新窗的窗体内容：与更新会话（ShellUpdateViewModel）绑定，
/// 供 ShellUpdateWindow 承载——下载中关窗/最小化由窗口层接管，这里只负责展示与操作分派。</summary>
public partial class ShellUpdateWindowView : UserControl
{
    public ShellUpdateWindowView() => InitializeComponent();

    private void Close_OnClick(object? sender, RoutedEventArgs args) => (DataContext as ShellUpdateViewModel)?.Close();
}
