using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class ShellDroppedPathCommand : Command
{
    public ShellDroppedPathCommand(string path)
    {
        Path = path;
    }

    public string Path { get; }
}
