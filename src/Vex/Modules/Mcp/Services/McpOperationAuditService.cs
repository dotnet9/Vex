using System.IO;
using System.Text.Json;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Serialization;

namespace Vex.Modules.Mcp.Services;

public sealed class McpOperationAuditService : IMcpOperationAuditService
{
    private const int MaxRecords = 100;
    private const long MaxLogFileBytes = 1024 * 1024;
    private readonly Lock _syncRoot = new();
    private readonly Queue<McpOperationRecord> _records = new(MaxRecords);
    private readonly string _logFilePath;

    public McpOperationAuditService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vex");
        _logFilePath = Path.Combine(directory, "mcp-audit.jsonl");
    }

    public string LogFilePath => _logFilePath;

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

        AppendToLogFile(record);
    }

    private void AppendToLogFile(McpOperationRecord record)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > MaxLogFileBytes)
            {
                var rotatedPath = Path.ChangeExtension(_logFilePath, ".1.jsonl");
                File.Move(_logFilePath, rotatedPath, overwrite: true);
            }

            var line = JsonSerializer.Serialize(record, McpJsonContext.Default.McpOperationRecord);
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 审计落盘失败不影响主流程，内存记录仍然可用。
        }
    }
}
