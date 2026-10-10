using CodeWF.Toolkit.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFileSelectionChangedCommand : Command
{
    public DocumentFileSelectionChangedCommand(DocumentFile? selectedFile)
    {
        SelectedFile = selectedFile;
    }

    public DocumentFile? SelectedFile { get; }
}
