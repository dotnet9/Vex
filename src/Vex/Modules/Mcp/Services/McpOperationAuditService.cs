using Vex.Modules.Mcp.Models;

namespace Vex.Modules.Mcp.Services;

public sealed class McpOperationAuditService : IMcpOperationAuditService
{
    private const int MaxRecords = 100;
    private readonly Lock _syncRoot = new();
    private readonly Queue<McpOperationRecord> _records = new(MaxRecords);

    public IReadOnlyList<McpOperationRecord> GetRecent()
    {
        lock (_syncRoot)
        {
            return _records.ToArray();
        }
    }

    public void Record(McpOperationRecord record)
    {
        lock (_syncRoot)
        {
            while (_records.Count >= MaxRecords)
            {
                _records.Dequeue();
            }

            _records.Enqueue(record);
        }
    }
}
