using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>标题栏「更新」药丸 + 日志气泡（原型 upd-pill/upd-pop）：随更新会话状态变形，
/// 下载中显百分比进度、就绪后脉冲提示；点击按状态分派——后台态恢复更新窗，否则弹日志气泡。</summary>
public partial class ShellUpdatePillView : UserControl
{
    public ShellUpdatePillView()
    {
        InitializeComponent();
        // 药丸点击不触发气泡的轻 dismissing（否则按下先关、抬起又开，气泡永远弹着）：
        // 让轻 dismissing 层把落在药丸上的按压穿透回主树，交给 Click 自己切换开关。
        ChangelogPop.OverlayInputPassThroughElement = PillButton;
    }

    private void Pill_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ShellUpdateViewModel model) return;
        // 下载中/就绪/安装中/失败/取消：药丸是「回到更新窗」的入口，恢复现场不重新检查（原型 pill click 同款分派）。
        if (model.IsDownloading || model.PillReady || model.State is UpdateState.Failed or UpdateState.Canceled)
        {
            ChangelogPop.IsOpen = false;
            model.RestoreWindow();
            return;
        }
        ChangelogPop.IsOpen = !ChangelogPop.IsOpen;
    }

    private void FullLog_OnClick(object? sender, RoutedEventArgs args)
    {
        ChangelogPop.IsOpen = false;
        (DataContext as ShellUpdateViewModel)?.OpenFullChangelog();
    }

    private void Go_OnClick(object? sender, RoutedEventArgs args)
    {
        ChangelogPop.IsOpen = false;
        _ = (DataContext as ShellUpdateViewModel)?.StartDownloadFromBubbleAsync();
    }
}
