using Avalonia.Media;

namespace Vex.Core.Services;

public static class VexFonts
{
    public static FontManagerOptions Options => new()
    {
        FontFallbacks = [new FontFallback
        {
            UnicodeRange = UnicodeRange.Parse("U+2E80-9FFF,U+F900-FAFF,U+FF00-FFEF,U+20000-3134F"),
            FontFamily = new FontFamily(OperatingSystem.IsWindows() ? "Microsoft YaHei UI" : OperatingSystem.IsMacOS() ? "PingFang SC" : "Noto Sans CJK SC, Noto Sans SC")
        }]
    };
    // 使用系统已有字体，不随安装包分发或向系统安装字体。
    public static FontFamily Code { get; } = new("Cascadia Code, Cascadia Mono, Consolas, JetBrains Mono, Microsoft YaHei UI, monospace");
}
