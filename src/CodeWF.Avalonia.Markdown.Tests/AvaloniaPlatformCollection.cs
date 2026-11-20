using Avalonia;
using global::Avalonia.Headless;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 需要真实 Avalonia 平台（Skia 离屏绘制、控件构造）的测试集合夹具。
/// <para>
/// xUnit 串行执行仍可能更换工作线程。所有控件操作通过官方 Headless 会话
/// 调度到同一个 UI 线程，避免主题资源与控件跨线程访问。
/// </para>
/// </summary>
public sealed class AvaloniaPlatformFixture
{
    private static readonly HeadlessUnitTestSession Session = StartSession();

    private static HeadlessUnitTestSession StartSession()
    {
        // 控件构造路径会打点日志（如 MarkdownLiveEditorView.SetText），宿主未初始化时 Logger 会抛异常。
        CodeWF.Toolkit.Logging.Logger.Initialize(new CodeWF.Toolkit.Logging.LoggerOptions
        {
            MinimumLevel = Microsoft.Extensions.Logging.LogLevel.Warning,
            EnableConsole = false,
        });
        return HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint), AvaloniaTestIsolationLevel.PerAssembly);
    }

    /// <summary>在专用 UI 线程上执行。</summary>
    public void Run(Action action) => Session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>在平台线程上执行并取回结果。</summary>
    public T Run<T>(Func<T> action) => Session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();

    public Task RunAsync(Func<Task> action) => Session.Dispatch(async () =>
    {
        await action();
        return true;
    }, CancellationToken.None)
        // Headless 内联完成任务；将 xunit 后续测试移回线程池，避免 UI 线程等待自身的下一次调度。
        .ContinueWith(task => task.GetAwaiter().GetResult(), CancellationToken.None,
            TaskContinuationOptions.None, TaskScheduler.Default);

    public sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

[CollectionDefinition("AvaloniaPlatform")]
public sealed class AvaloniaPlatformCollection : ICollectionFixture<AvaloniaPlatformFixture>
{
}
