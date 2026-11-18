namespace CodeWF.Avalonia.MindView;

public interface IMindMapFileService
{
    Task<MindMapFileOpenResult?> OpenAsync(CancellationToken cancellationToken = default);

    Task<MindMapFileOpenResult?> ImportAsync(CancellationToken cancellationToken = default);

    Task<string?> OpenTextAsync(MindMapFileFormat format, CancellationToken cancellationToken = default);

    Task<byte[]?> OpenBinaryAsync(MindMapFileFormat format, CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);

    Task<MindMapFileSaveTarget?> PickSaveTargetAsync(
        MindMapFileFormat suggestedFormat,
        string suggestedFileName,
        CancellationToken cancellationToken = default);

    Task SaveTextAsync(MindMapFileFormat format, string content, CancellationToken cancellationToken = default);

    Task SaveBinaryAsync(MindMapFileFormat format, byte[] content, CancellationToken cancellationToken = default);

    Task<MindMapSaveChangesDecision> ConfirmSaveChangesAsync(
        string documentName,
        CancellationToken cancellationToken = default);
}
