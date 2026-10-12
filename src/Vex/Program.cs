using System.Text;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Vex;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // 长文档预览含大量图片、Mermaid SVG、代码高亮的长文本纹理，
            // 默认 GPU 资源上限（约 100MB）会触发反复丢弃与重建，滚动时表现为掉帧。
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 512L * 1024 * 1024 })
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
    }
}
