namespace Vex.Core.Models;

public sealed class DocumentFileNode
{
    public DocumentFileNode(
        string name,
        string path,
        bool isFolder,
        IReadOnlyList<DocumentFileNode>? children = null,
        DocumentFile? documentFile = null)
    {
        Name = name;
        Path = path;
        IsFolder = isFolder;
        Children = children ?? [];
        DocumentFile = documentFile;
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsFolder { get; }

    public bool IsDocument => DocumentFile is not null;

    public DocumentFile? DocumentFile { get; }

    public IReadOnlyList<DocumentFileNode> Children { get; }

    public string ModifiedText => DocumentFile?.ModifiedText ?? string.Empty;

    public string Preview => DocumentFile?.Preview ?? string.Empty;
}
