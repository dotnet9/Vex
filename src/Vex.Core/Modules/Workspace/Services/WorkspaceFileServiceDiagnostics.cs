namespace Vex.Modules.Workspace.Services;

/// <summary>服务生命周期内累计的磁盘工作计数，用于验证读取范围及并发上限。</summary>
public sealed record WorkspaceFileServiceDiagnostics(
    long DirectoryEnumerationCount,
    long IndexScanCount,
    long PreviewReadCount,
    int ActiveOperations,
    int MaximumConcurrentOperations);
