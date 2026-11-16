using System.Text;
using Avalonia;
using ReactiveUI.Avalonia;

namespace Vex;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Vex.Core.Services.CrashLog.Install();
        if (args is ["--verify-ui", var uiDirectory])
        {
            RuntimeVerification.RunUi(uiDirectory);
            return;
        }
        if (args is ["--verify-ui", var sampleDirectory, var sampleArticle])
        {
            RuntimeVerification.RunUi(sampleDirectory, sampleArticle);
            return;
        }
        if (args is ["--verify-runtime", var directory])
        {
            RuntimeVerification.Run(directory);
            return;
        }
        if (args is ["--verify-git", var gitRepository])
        {
            RuntimeVerification.RunGit(gitRepository);
            return;
        }
        if (args is ["--git-run", var runRepository, var runAction, var runArgument, var runPaths])
        {
            RuntimeVerification.RunGitExecute(runRepository, runAction, runArgument, runPaths);
            return;
        }
        if (args is ["--git-run", var runRepo3, var runAction3])
        {
            RuntimeVerification.RunGitExecute(runRepo3, runAction3, null);
            return;
        }
        if (Core.Services.SingleInstance.TryAcquire(args))
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(Core.Services.VexFonts.Options)
            // 长文档预览含大量图片、Mermaid SVG、代码高亮的长文本纹理，
            // 默认 GPU 资源上限（约 100MB）会触发反复丢弃与重建，滚动时表现为掉帧。
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 512L * 1024 * 1024 })
            .UseReactiveUI(_ => { })
            .LogToTrace();
    }
}
