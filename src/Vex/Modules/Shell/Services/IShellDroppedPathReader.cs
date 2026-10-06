using Avalonia.Input;

namespace Vex.Modules.Shell.Services;

public interface IShellDroppedPathReader
{
    string? GetFirstLocalFile(DragEventArgs e);
}
