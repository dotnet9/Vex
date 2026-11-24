namespace Vex.Modules.Workspace.Services;

public sealed record EditorPasteTarget(string? FilePath, string Markdown, int Start, int Length, bool IsInstantEditing)
{
    public bool Matches(string? filePath, string markdown, int start, int length, bool isInstantEditing) =>
        string.Equals(FilePath, filePath, StringComparison.Ordinal)
        && string.Equals(Markdown, markdown, StringComparison.Ordinal)
        && Start == start
        && Length == length
        && IsInstantEditing == isInstantEditing;

    public string? TryReplace(string? filePath, string markdown, int start, int length, bool isInstantEditing, string insertion)
    {
        if (!Matches(filePath, markdown, start, length, isInstantEditing)
            || start < 0 || length < 0 || start > markdown.Length || length > markdown.Length - start)
        {
            return null;
        }

        return markdown.Remove(start, length).Insert(start, insertion);
    }
}
