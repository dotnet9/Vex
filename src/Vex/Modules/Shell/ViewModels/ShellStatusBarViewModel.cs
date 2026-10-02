using Avalonia.Threading;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellStatusBarViewModel : ReactiveObject
{
    private readonly IMcpServerHost _mcpServerHost;
    private readonly IAppLocalizer _localizer;
    private string _mcpStatusText = string.Empty;

    public ShellStatusBarViewModel(
        ShellStatusViewModel status,
        ShellDocumentInfoViewModel documentInfo,
        ShellWindowLayoutViewModel layout,
        IMcpServerHost mcpServerHost,
        IAppLocalizer localizer)
    {
        Status = status;
        DocumentInfo = documentInfo;
        Layout = layout;
        _mcpServerHost = mcpServerHost;
        _localizer = localizer;
        _mcpServerHost.StatusChanged += (_, _) => RefreshMcpStatus();
        _localizer.CultureChanged += (_, _) => RefreshMcpStatus();
        RefreshMcpStatus();
    }

    public ShellStatusViewModel Status { get; }

    public ShellDocumentInfoViewModel DocumentInfo { get; }

    public ShellWindowLayoutViewModel Layout { get; }

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
