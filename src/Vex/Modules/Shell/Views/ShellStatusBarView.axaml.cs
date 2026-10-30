using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;

using Microsoft.Extensions.DependencyInjection;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellStatusBarView : UserControl
{
    public ShellStatusBarView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<ShellStatusBarViewModel>();
        // 原型把缩放坞收进状态栏（.statusbar-zoom）：阅读坞绑预览视图模型（容器单例），
        // 导图坞的还原=适应画布，需要主窗上的导图宿主，挂到可视树后接线。
        if (App.Services.GetRequiredService<MarkdownPreviewViewModel>() is { } preview)
        {
            ReadingZoomDock.DataContext = preview;
            ReadingZoomDock.ResetRequested += (_, _) => preview.ResetReadingZoom();
        }
        MapZoomDock.ResetRequested += (_, _) =>
            (TopLevel.GetTopLevel(this) as MainWindow)?.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")?.FitMap();
    }
}
