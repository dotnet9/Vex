using CodeWF.Toolkit.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class DocumentFileRenameRequestedCommand : Command
{
    public DocumentFileRenameRequestedCommand(DocumentFile file)
    {
        File = file;
    }

    public DocumentFile File { get; }
}
