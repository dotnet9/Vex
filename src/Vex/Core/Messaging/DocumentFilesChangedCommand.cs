using CodeWF.Toolkit.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFilesChangedCommand : Command
{
    public DocumentFilesChangedCommand(
        IReadOnlyList<DocumentFile> files,
        DocumentFile? selectedFile = null,
        string? workspaceRootPath = null,
        bool focusWorkspaceRoot = false)
    {
        Files = files.ToArray();
        SelectedFile = selectedFile;
        WorkspaceRootPath = workspaceRootPath;
        FocusWorkspaceRoot = focusWorkspaceRoot;
    }

    public IReadOnlyList<DocumentFile> Files { get; }

    public DocumentFile? SelectedFile { get; }

    public string? WorkspaceRootPath { get; }

    public bool FocusWorkspaceRoot { get; }
}
