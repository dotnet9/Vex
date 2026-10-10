using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class MarkdownDocumentChangedCommand : Command
{
    public MarkdownDocumentChangedCommand(string markdown, string? filePath)
    {
        Markdown = markdown;
        FilePath = filePath;
    }

    public string Markdown { get; }

    public string? FilePath { get; }
}
