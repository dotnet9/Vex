using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Vex.Core.Services;

/// <summary>
/// 单实例：首个进程持有命名互斥体并监听命名管道；后续进程把命令行参数转发后退出。
/// 仅 Windows/Linux/macOS 桌面运行时使用；验证器路径不经此类。
/// </summary>
public static class SingleInstance
{
    private const string MutexName = "Vex.SingleInstance.Mutex";
    private const string PipeName = "Vex.SingleInstance.Pipe";

    /// <summary>尝试成为主实例；作为从实例时把参数转发给主实例并返回 false。</summary>
    public static bool TryAcquire(string[] args)
    {
        if (TryForwardToPrimary(args))
        {
            return false;
        }
        _ = StartListener();
        return true;
    }

    private static bool TryForwardToPrimary(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 400);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(string.Join('\n', args));
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task StartListener()
    {
        while (true)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var payload = await reader.ReadToEndAsync();
                var paths = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(File.Exists).ToArray();
                // 复用启动参数通道：主实例按既有逻辑打开文件。
                // 管道线程不得直接触碰 Avalonia 对象：命令必须投递到 UI 线程处理。
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
                        new Vex.Core.Messaging.ShellStartupArgumentsCommand(paths)));
            }
            catch
            {
                // 管道异常不终止主实例；下一轮重建。
            }
        }
    }
}
