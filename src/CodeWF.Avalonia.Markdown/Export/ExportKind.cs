namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Markdown document export targets.
/// </summary>
public enum ExportKind
{
    Png,
    Pdf,
    Word,

    /// <summary>自包含单文件 HTML（内联样式，本地图片内嵌为 data URI）。</summary>
    Html
}
