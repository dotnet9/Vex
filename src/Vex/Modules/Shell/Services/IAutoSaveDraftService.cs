using Vex.Core.Models;

namespace Vex.Modules.Shell.Services;

public interface IAutoSaveDraftService
{
    string? LastError { get; }
    event EventHandler? StateChanged;
    DocumentSnapshot? TryRestore(DocumentSnapshot document);

    void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown);

    void Clear(DocumentSnapshot document);

    bool Flush();
    void ClearRestored(DocumentSnapshot document);
}
