using System.Text;

namespace Vex.Core.Services;

/// <summary>
/// 全局崩溃日志：Dispatcher / AppDomain / TaskScheduler 三路未处理异常落盘到
/// %LocalAppData%\Vex\crash\（保留最近 10 份）。AOT 未捕获异常走 fail-fast 没有事件日志
/// 托管栈，没有这份落盘就无法定位发布版崩溃。
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();
    private static bool _installed;

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vex", "crash");

    public static void Install()
    {
        lock (Gate)
        {
            if (_installed) return;
            _installed = true;
        }

        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            args.Handled = true; // 挂日志后继续跑，不让 UI 线程异常拖垮进程
            Write("ui-thread", args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Write(args.IsTerminating ? "terminating" : "background", args.ExceptionObject as Exception
                ?? new Exception(args.ExceptionObject?.ToString() ?? "unknown non-exception failure"));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write("unobserved-task", args.Exception);
            args.SetObserved();
        };
    }

    private static void Write(string channel, Exception exception)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var file = Path.Combine(Directory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            var builder = new StringBuilder();
            builder.AppendLine("time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.AppendLine("channel: " + channel);
            builder.AppendLine("version: " + (typeof(CrashLog).Assembly.GetName().Version?.ToString() ?? "?"));
            var onUi = false;
            try { onUi = Avalonia.Threading.Dispatcher.UIThread.CheckAccess(); }
            catch { /* headless/AOT 裸线程可能无调度器 */ }
            builder.AppendLine("thread: " + Environment.CurrentManagedThreadId + (onUi ? " (ui)" : " (bg)"));
            for (var ex = (Exception?)exception; ex is not null; ex = ex.InnerException)
            {
                builder.AppendLine();
                builder.AppendLine("[" + ex.GetType().FullName + "] " + ex.Message);
                builder.AppendLine(ex.StackTrace);
                if (ex is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions) builder.AppendLine("(aggregate) " + inner.GetType().FullName + ": " + inner.Message);
                }
            }
            File.WriteAllText(file, builder.ToString(), Encoding.UTF8);
            PruneOldLogs();
        }
        catch
        {
            // 崩溃日志自身失败不能再抛（多半已在终止路径上）。
        }
    }

    /// <summary>只留最近 10 份：崩溃风暴不把数据目录塞满。</summary>
    private static void PruneOldLogs()
    {
        var files = new DirectoryInfo(Directory).GetFiles("crash-*.log")
            .OrderByDescending(f => f.Name).Skip(10);
        foreach (var file in files)
        {
            try { file.Delete(); }
            catch { /* 占用即跳过 */ }
        }
    }
}
