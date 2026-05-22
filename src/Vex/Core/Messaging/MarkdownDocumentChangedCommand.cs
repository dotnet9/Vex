using CodeWF.EventBus;

namespace Vex.Core.Messaging;

public sealed class MarkdownDocumentChangedCommand : Command
{
    public MarkdownDocumentChangedCommand(string markdown)
    {
        Markdown = markdown;
    }

    public string Markdown { get; }
}
