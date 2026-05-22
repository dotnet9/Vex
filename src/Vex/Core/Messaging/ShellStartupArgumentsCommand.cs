using CodeWF.EventBus;

namespace Vex.Core.Messaging;

public sealed class ShellStartupArgumentsCommand : Command
{
    public ShellStartupArgumentsCommand(IEnumerable<string> arguments)
    {
        Arguments = arguments.ToArray();
    }

    public IReadOnlyList<string> Arguments { get; }
}
