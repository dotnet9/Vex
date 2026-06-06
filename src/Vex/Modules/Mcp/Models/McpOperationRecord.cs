namespace Vex.Modules.Mcp.Models;

public sealed record McpOperationRecord(
    DateTimeOffset Timestamp,
    string ToolName,
    string Target,
    string OperationType,
    bool RequiresConfirmation,
    bool Confirmed,
    bool Succeeded,
    string? Error);
