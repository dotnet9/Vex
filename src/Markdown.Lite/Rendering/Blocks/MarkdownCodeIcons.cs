using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>代码块工具条的 16×16 线条图标（几何取自 StreamMark，MIT）。</summary>
internal static class MarkdownCodeIcons
{
    public static Geometry Copy { get; } = Geometry.Parse(
        "M5,5 L5,2.5 A1,1 0 0 1 6,1.5 L13,1.5 A1,1 0 0 1 14,2.5 L14,9.5 A1,1 0 0 1 13,10.5 L11,10.5 M2.5,5.5 L10,5.5 A1,1 0 0 1 11,6.5 L11,13.5 A1,1 0 0 1 10,14.5 L2.5,14.5 A1,1 0 0 1 1.5,13.5 L1.5,6.5 A1,1 0 0 1 2.5,5.5 Z");

    public static Geometry Check { get; } = Geometry.Parse("M2.5,8.5 L6,12 L13.5,4");

    public static Geometry Wrap { get; } = Geometry.Parse(
        "M1.5,3.5 L14.5,3.5 M1.5,8 L12,8 A2.25,2.25 0 0 1 12,12.5 L8,12.5 M9.5,10.5 L7.5,12.5 L9.5,14.5 M1.5,12.5 L5,12.5");

    public static Geometry Download { get; } = Geometry.Parse("M8,2 L8,10.5 M4.5,7 L8,10.5 L11.5,7 M2.5,13.5 L13.5,13.5");
}
