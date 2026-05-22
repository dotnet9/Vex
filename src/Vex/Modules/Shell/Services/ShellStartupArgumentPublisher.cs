using CodeWF.EventBus;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.Services;

public sealed class ShellStartupArgumentPublisher : IShellStartupArgumentPublisher
{
    private readonly IEventBus _eventBus;

    public ShellStartupArgumentPublisher(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void PublishStartupArguments(IEnumerable<string> arguments)
    {
        var normalizedArguments = arguments.ToArray();
        if (normalizedArguments.Length == 0)
        {
            return;
        }

        // 启动参数只作为 Shell 意图发布，具体打开流程仍由主文档工作流处理。
        _eventBus.Publish(new ShellStartupArgumentsCommand(normalizedArguments));
    }
}
