using Vex.Core.Messaging;

namespace Vex.Modules.Shell.Services;

public sealed class ShellStartupArgumentPublisher : IShellStartupArgumentPublisher
{
    public void PublishStartupArguments(IEnumerable<string> arguments)
    {
        var normalizedArguments = arguments.ToArray();
        // 启动参数只作为 Shell 意图发布，空参数也用于触发上次工作目录恢复。
        CodeWF.EventBus.EventBus.Default.Publish(new ShellStartupArgumentsCommand(normalizedArguments));
    }
}
