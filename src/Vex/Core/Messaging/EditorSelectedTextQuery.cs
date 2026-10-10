using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class EditorSelectedTextQuery : Query<string>
{
    public override string Result { get; set; } = string.Empty;
}
