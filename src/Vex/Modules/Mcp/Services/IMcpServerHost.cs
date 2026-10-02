namespace Vex.Modules.Mcp.Services;

public interface IMcpServerHost
{
    bool IsRunning { get; }

    string StatusText { get; }

    Task ApplySettingsAsync();

    Task StopAsync();
}
