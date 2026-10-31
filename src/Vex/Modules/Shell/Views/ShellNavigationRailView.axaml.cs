using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Avalonia.Controls.Helpers;
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
        ToolTipHelper.SetText(RailFilesToggle, DesignSelectionControls.Text(_railLayout.ShowSidebar ? "NavFilesCollapse" : "NavFilesExpand"));
        ToolTipHelper.SetText(RailOutlineToggle, DesignSelectionControls.Text(_railLayout.ShowOutlineSidebar ? "NavOutlineCollapse" : "NavOutlineExpand"));
    }

}
