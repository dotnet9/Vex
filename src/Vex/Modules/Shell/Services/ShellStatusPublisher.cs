using System.Globalization;
using CodeWF.EventBus;
using Lang.Avalonia;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.Services;

public sealed class ShellStatusPublisher : IShellStatusPublisher
{
    private readonly IEventBus _eventBus;

    public ShellStatusPublisher(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Publish(string message)
    {
        _eventBus.Publish(new WorkspaceStatusChangedCommand(message));
    }

    public void PublishResource(string key)
    {
        Publish(ResolveResource(key));
    }

    public void PublishResourceFormat(string key, params object?[] args)
    {
        Publish(string.Format(CultureInfo.CurrentCulture, ResolveResource(key), args));
    }

    private static string ResolveResource(string key)
    {
        // 状态栏文案统一从这里解析，后续做节流、日志或语言兜底时只需要维护这一处。
        return I18nManager.Instance.GetResource(key);
    }
}
