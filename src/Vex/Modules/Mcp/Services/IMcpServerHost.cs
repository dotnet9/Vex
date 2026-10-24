namespace Vex.Modules.Mcp.Services;

/// <summary>服务运行状态：偏好页状态行的指示灯与文字色据此区分（对应原型 data-state）。</summary>
public enum McpServerState
{
    Stopped,
    Running,
    Faulted
}

public interface IMcpServerHost
{
    bool IsRunning { get; }

    McpServerState State { get; }

    string StatusText { get; }

    event EventHandler? StatusChanged;

    Task ApplySettingsAsync();

    Task StopAsync();
}
