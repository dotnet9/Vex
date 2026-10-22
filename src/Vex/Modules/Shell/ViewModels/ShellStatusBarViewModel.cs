using Avalonia.Threading;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellStatusBarViewModel : ReactiveObject
{
    private readonly IMcpServerHost _mcpServerHost;
    private readonly IAppLocalizer _localizer;
    private readonly Vex.Modules.Shell.Services.IShellOverlayService _overlays;
    private string _mcpStatusText = string.Empty;

    public ShellStatusBarViewModel(
        ShellStatusViewModel status,
        ShellDocumentInfoViewModel documentInfo,
        ShellWindowLayoutViewModel layout,
        ShellWorkbenchViewModel workbench,
        IMcpServerHost mcpServerHost,
        IAppLocalizer localizer, ShellAppearanceViewModel appearance, Vex.Modules.Shell.Services.IShellOverlayService overlays)
    {
        Appearance = appearance; _overlays = overlays;
        Status = status;
        DocumentInfo = documentInfo;
        Layout = layout;
        Workbench = workbench;
        _mcpServerHost = mcpServerHost;
        _localizer = localizer;
        _mcpServerHost.StatusChanged += (_, _) => RefreshMcpStatus();
        _localizer.CultureChanged += (_, _) => RefreshMcpStatus();
        RefreshMcpStatus();
    }

    public EditorPreferences Preferences => Workbench.Preferences;
    public ShellAppearanceViewModel Appearance { get; }
    /// <summary>状态栏字数入口：打开文章信息统计浮层（对应原型 sb-wordcount → dlg-statistics）。</summary>
    public Task ShowStatisticsAsync() => _overlays.ShowStatisticsAsync(DocumentInfo);

    public ShellStatusViewModel Status { get; }

    public ShellDocumentInfoViewModel DocumentInfo { get; }

    public ShellWindowLayoutViewModel Layout { get; }
    public ShellWorkbenchViewModel Workbench { get; }

    public string McpStatusText
    {
        get => _mcpStatusText;
        private set => this.RaiseAndSetIfChanged(ref _mcpStatusText, value);
    }

    private void RefreshMcpStatus()
    {
        // 服务状态变化可能来自后台监听线程，统一回调度器再改可绑定属性。
        Dispatcher.UIThread.Post(() =>
        {
            McpStatusText = _localizer.Get(
                _mcpServerHost.IsRunning ? VexL.StatusMcpRunning : VexL.StatusMcpStopped);
        });
    }
}
