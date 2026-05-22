using CodeWF.EventBus;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.Services;

public sealed class ShellStatusPublisher : IShellStatusPublisher
{
    private readonly IEventBus _eventBus;
    private readonly IShellLocalizer _localizer;

    public ShellStatusPublisher(IEventBus eventBus, IShellLocalizer localizer)
    {
        _eventBus = eventBus;
        _localizer = localizer;
    }

    public void Publish(string message)
    {
        _eventBus.Publish(new WorkspaceStatusChangedCommand(message));
    }

    public void PublishResource(string key)
    {
        Publish(_localizer.Get(key));
    }

    public void PublishResourceFormat(string key, params object?[] args)
    {
        Publish(_localizer.Format(key, args));
    }
}
