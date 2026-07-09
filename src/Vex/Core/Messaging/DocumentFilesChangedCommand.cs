using CodeWF.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFilesChangedCommand : Command
{
    public DocumentFilesChangedCommand(
        IReadOnlyList<DocumentFile> files,
        DocumentFile? selectedFile = null,
        string? workspaceRootPath = null)
    {
        Files = files.ToArray();
        SelectedFile = selectedFile;
        WorkspaceRootPath = workspaceRootPath;
    }

    public IReadOnlyList<DocumentFile> Files { get; }

    public DocumentFile? SelectedFile { get; }

    public string? WorkspaceRootPath { get; }
}
