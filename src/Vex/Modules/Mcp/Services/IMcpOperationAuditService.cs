using Vex.Modules.Mcp.Models;

namespace Vex.Modules.Mcp.Services;

public interface IMcpOperationAuditService
{
    IReadOnlyList<McpOperationRecord> GetRecent();

    void Record(McpOperationRecord record);
}
