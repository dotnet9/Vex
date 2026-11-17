using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class EditorSnippetCommand(string key) : Command
{
    public string Key { get; } = key;
}
