using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IDocumentService
{
    DocumentSnapshot CreateNew();

    Task<DocumentSnapshot?> OpenAsync();

    Task<DocumentSnapshot> OpenPathAsync(string path, string? encodingName = null);

    Task<IReadOnlyList<DocumentFile>> OpenFolderAsync();

    Task<IReadOnlyList<DocumentFile>> OpenFolderPathAsync(string folder);

    Task<DocumentSnapshot?> SaveAsync(DocumentSnapshot document);

    Task<DocumentSnapshot?> SaveAsAsync(DocumentSnapshot document);

    Task DeleteAsync(string path);

    Task OpenFileLocationAsync(string path);
}
