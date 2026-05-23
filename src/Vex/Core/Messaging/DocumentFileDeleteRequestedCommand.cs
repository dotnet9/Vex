using CodeWF.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFileDeleteRequestedCommand : Command
{
    public DocumentFileDeleteRequestedCommand(DocumentFile file)
    {
        File = file;
    }

    public DocumentFile File { get; }
}
