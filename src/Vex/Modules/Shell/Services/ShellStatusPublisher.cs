using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class ShellStatusPublisher : IShellStatusPublisher
{
    private readonly IAppLocalizer _localizer;

    public ShellStatusPublisher(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public void Publish(string message)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new WorkspaceStatusChangedCommand(message));
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
