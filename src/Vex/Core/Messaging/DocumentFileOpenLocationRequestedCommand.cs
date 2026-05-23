using CodeWF.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFileOpenLocationRequestedCommand : Command
{
    public DocumentFileOpenLocationRequestedCommand(DocumentFile file)
    {
        File = file;
    }

    public DocumentFile File { get; }
}
