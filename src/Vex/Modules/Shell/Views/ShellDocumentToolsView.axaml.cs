using Avalonia.Controls;

using Microsoft.Extensions.DependencyInjection;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellDocumentToolsView : UserControl
{
    public ShellDocumentToolsView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<ShellDocumentToolsViewModel>();
        // 阅读缩放还原字号，导图缩放还原到适应画布。
        if (App.Services.GetRequiredService<MarkdownPreviewViewModel>() is { } preview)
        {
            ReadingZoomDock.DataContext = preview;
            ReadingZoomDock.ResetRequested += (_, _) => preview.ResetReadingZoom();
        }
        MapZoomDock.ResetRequested += (_, _) =>
            (TopLevel.GetTopLevel(this) as MainWindow)?.Workspace.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")?.FitMap();
        // 评审修订（原型 C3）：缩放坞新增「回到中心」——保持缩放把根节点带回视口，平移后不再迷失。
        MapZoomDock.CenterRequested += (_, _) =>
            (TopLevel.GetTopLevel(this) as MainWindow)?.Workspace.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")?.CenterMap();
    }
}
