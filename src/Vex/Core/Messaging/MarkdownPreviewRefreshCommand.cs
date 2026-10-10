using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class MarkdownPreviewRefreshCommand : Command
{
    public MarkdownPreviewRefreshCommand(string markdown, string? filePath, long refreshVersion)
    {
        Markdown = markdown;
        FilePath = filePath;
        RefreshVersion = refreshVersion;
    }

    public string Markdown { get; }

    public string? FilePath { get; }

    public long RefreshVersion { get; }
}
