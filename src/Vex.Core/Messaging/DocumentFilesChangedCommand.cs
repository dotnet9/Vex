using CodeWF.Toolkit.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFilesChangedCommand : Command
{
    public DocumentFilesChangedCommand(
        IReadOnlyList<DocumentFile> files,
        DocumentFile? selectedFile = null,
        string? workspaceRootPath = null,
        bool focusWorkspaceRoot = false,
        string? renamedFrom = null)
    {
        Files = files.ToArray();
        SelectedFile = selectedFile;
        WorkspaceRootPath = workspaceRootPath;
        FocusWorkspaceRoot = focusWorkspaceRoot;
        RenamedFrom = renamedFrom;
    }

    public IReadOnlyList<DocumentFile> Files { get; }

    /// <summary>本次变更是重命名时的旧完整路径（非空时文件树做节点就地改名，不做全量重建）。</summary>
    public string? RenamedFrom { get; }

    public DocumentFile? SelectedFile { get; }

    public string? WorkspaceRootPath { get; }

    public bool FocusWorkspaceRoot { get; }
}
