using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IDocumentService
{
    string? LastOpenedFolderPath { get; }

    DocumentSnapshot CreateNew();

    Task<DocumentSnapshot?> OpenAsync();

    Task<DocumentSnapshot> OpenPathAsync(string path, string? encodingName = null);

    Task<DocumentSnapshot> ReloadAsync(DocumentSnapshot document);

    Task<IReadOnlyList<DocumentFile>> OpenFolderAsync();

    Task<IReadOnlyList<DocumentFile>> OpenFolderPathAsync(string folder);

    bool IsSupportedDocumentPath(string path);

    Task<DocumentSnapshot?> SaveAsync(DocumentSnapshot document);

    Task<DocumentSnapshot?> SaveAsAsync(DocumentSnapshot document);

    Task DeleteAsync(string path);

    Task<string> RenameAsync(string path, string newName);

    Task OpenFileLocationAsync(string path);
}
