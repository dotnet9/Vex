namespace Vex.Modules.Mcp.Services;

public interface IMcpServerHost
{
    bool IsRunning { get; }

    string StatusText { get; }

    event EventHandler? StatusChanged;

    Task ApplySettingsAsync();

    Task StopAsync();
}
