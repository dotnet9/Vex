using Avalonia.Controls;

using Microsoft.Extensions.DependencyInjection;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellDocumentToolsView : UserControl
{
    private readonly IAppLocalizer _localizer = App.Services.GetRequiredService<IAppLocalizer>();

    public ShellDocumentToolsView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<ShellDocumentToolsViewModel>();
        // 视图徽标（原型 #sb-layout）：即时/源码/阅读/导图随状态实时刷新。
        Loaded += (_, _) => SyncLayoutChip();
        if (DataContext is ShellDocumentToolsViewModel tools && tools.Workbench is not null)
            tools.Workbench.PropertyChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncLayoutChip);
        _localizer.CultureChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncLayoutChip);
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

    private void SyncLayoutChip()
    {
        if (LayoutChipText is null || DataContext is not ShellDocumentToolsViewModel tools) return;
        var workbench = tools.Workbench;
        if (workbench is null) return;
        LayoutChipText.Text = workbench.IsMapVisible && workbench.IsMindMap ? _localizer.Get(VexL.StatusViewMap)
            : workbench.IsReading ? _localizer.Get(VexL.StatusViewRead)
            : workbench.IsSourceEditing ? _localizer.Get(VexL.StatusViewSource)
            : _localizer.Get(VexL.StatusViewInstant);
    }
}
