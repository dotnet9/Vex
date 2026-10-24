using CodeWF.Toolkit.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFileSelectionChangedCommand : Command
{
    public DocumentFileSelectionChangedCommand(DocumentFile? selectedFile, bool revealPath = false)
    {
        SelectedFile = selectedFile;
        RevealPath = revealPath;
    }

    public DocumentFile? SelectedFile { get; }

    public bool RevealPath { get; }
}
