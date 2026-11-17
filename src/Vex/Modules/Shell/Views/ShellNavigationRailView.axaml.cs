using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Avalonia.Controls.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Vex;
using Vex.Modules.Mcp.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>Window navigation entries and their localized expanded/collapsed labels.</summary>
public partial class ShellNavigationRailView : UserControl
{
    private ShellWindowLayoutViewModel? _railLayout;
    public ShellNavigationRailView()
    {
        InitializeComponent();
        // 原型 rail-item 的 title/aria-label 随开合状态切换（收起文件导航/展开文件导航…），语言切换同步重译。
        DataContextChanged += (_, _) =>
        {
            if (_railLayout is not null) _railLayout.PropertyChanged -= RailLayoutChanged;
            _railLayout = (DataContext as MainWindowViewModel)?.Layout;
            if (_railLayout is not null) _railLayout.PropertyChanged += RailLayoutChanged;
            SyncRailTips();
        };
        AttachedToVisualTree += (_, _) => CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged += CultureChanged;
        DetachedFromVisualTree += (_, _) => CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged -= CultureChanged;
        // MCP 服务运行徽标：设置图标右上角绿点（原型 mcp-badge 语义），状态变化实时点亮/熄灭。
        if (App.Services.GetService(typeof(Vex.Modules.Mcp.Services.IMcpServerHost)) is Vex.Modules.Mcp.Services.IMcpServerHost host)
        {
            host.StatusChanged += SyncMcpBadge;
            SyncMcpBadge(host, EventArgs.Empty);
            DetachedFromVisualTree += (_, _) => host.StatusChanged -= SyncMcpBadge;
        }
    }
    private void SyncMcpBadge(object? sender, EventArgs e)
    {
        if (sender is Vex.Modules.Mcp.Services.IMcpServerHost host)
            McpRunningBadge.IsVisible = host.IsRunning;
    }
    private void CultureChanged(object? sender, EventArgs e) => SyncRailTips();
    private void RailLayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellWindowLayoutViewModel.ShowSidebar) or nameof(ShellWindowLayoutViewModel.ShowOutlineSidebar)) SyncRailTips();
    }

    private void SyncRailTips()
    {
        if (_railLayout is null) return;
        // 语言切换可能从工作线程触发（验证夹具即在后台线程改 Culture），控件访问必须回 UI 线程。
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(SyncRailTips); return; }
        // Git 原型稿：大纲折叠钮移到标题栏，rail 只剩文件/Git 双模式 + 设置，大纲提示同步随之退役。
        ToolTipHelper.SetText(RailFilesToggle, DesignSelectionControls.Text(_railLayout.ShowSidebar ? "NavFilesCollapse" : "NavFilesExpand"));
    }

}
