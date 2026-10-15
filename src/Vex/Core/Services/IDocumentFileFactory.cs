using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IDocumentFileFactory
{
    DocumentFile Create(string path, string? workspaceRoot = null);

    string GetModifiedText(string path);
}
