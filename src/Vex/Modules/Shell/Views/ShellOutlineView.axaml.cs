using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Vex.Modules.Shell.Views;

/// <summary>大纲侧栏薄壳：面板头 + 库 MarkdownOutlinePanel 的宿主接线（导航/折叠持久化/当前章）。
/// 关闭走 VM 的 ToggleOutlineCommand（MVVM：切停靠属于状态），这里只留导轨焦点的视图行为。</summary>
public partial class ShellOutlineView : UserControl
{
    public ShellOutlineView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        var viewModel = App.Services.GetRequiredService<ShellOutlineViewModel>();
        DataContext = viewModel;
        AttachedToVisualTree += (_, _) => viewModel.AttachPanel(OutlinePanel);
        DetachedFromVisualTree += (_, _) => viewModel.DetachPanel(OutlinePanel);
    }

    private void RefocusRailToggle(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow window) return;
        window.Workspace.Navigation.FindControl<Avalonia.Controls.Primitives.ToggleButton>("RailOutlineToggle")?.Focus();
    }
}
