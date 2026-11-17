using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

/// <summary>给当前选区套行内样式（color / background-color / font-size），值保留作者写法（如 "#c92a2a"、"18px"）。</summary>
public sealed class EditorSpanStyleCommand : Command
{
    public EditorSpanStyleCommand(string property, string value)
    {
        Property = property;
        Value = value;
    }

    public string Property { get; }

    public string Value { get; }
}
