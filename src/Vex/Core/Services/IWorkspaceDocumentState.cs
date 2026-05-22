namespace Vex.Core.Services;

public interface IWorkspaceDocumentState
{
    string Markdown { get; }

    void UpdateMarkdown(string markdown);
}
