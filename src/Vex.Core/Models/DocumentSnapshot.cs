using System.Text;

namespace Vex.Core.Models;

public sealed record DocumentSnapshot(
    string? FilePath,
    string FileName,
    string Markdown,
    Encoding Encoding,
    bool IsNew);
