using CodeWF.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFileOpenRequestedCommand : Command
{
    public DocumentFileOpenRequestedCommand(DocumentFile file, DocumentFile? previousSelection)
    {
        File = file;
        PreviousSelection = previousSelection;
    }

    public DocumentFile File { get; }

    public DocumentFile? PreviousSelection { get; }
}
