using Avalonia.Media;

namespace Vex.Core.Services;

public static class VexFonts
{
    // 使用系统已有字体，不随安装包分发或向系统安装字体。
    public static FontFamily Code { get; } = new("Cascadia Code, Cascadia Mono, Consolas, JetBrains Mono, Microsoft YaHei UI, monospace");
}
