namespace Vex.Modules.Mcp.Services;

public interface IMcpOperationConfirmationService
{
    Task<bool> ConfirmAsync(string toolName, string target, string summary);
}
