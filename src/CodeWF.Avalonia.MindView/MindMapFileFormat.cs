namespace CodeWF.Avalonia.MindView;

/// <summary>
/// 枝见支持读写的脑图文档格式。
/// </summary>
public enum MindMapFileFormat
{
    /// <summary>
    /// 可读、可直接提交版本库的 Markdown 标题层级。
    /// </summary>
    Markdown,

    /// <summary>
    /// 便于和大纲类工具交换的 OPML。
    /// </summary>
    Opml,

    /// <summary>
    /// 常见脑图工具使用的 XMind 文件包。
    /// </summary>
    XMind,

    Xml,

    FreeMind,

    MindManager,

    MindNode,

    MindMaster,

    BaiduMindMap,

    MindNow,

    Image,

    Svg,

    WebP,

    Pdf,

    Word,

    Excel,

    PowerPoint,

    PlainText,

    TextBundle,

    Html,

    Json,

    Yaml,

    Csv,

    DrawIo,

    Visio,

    Gliffy,

    Lucid
}
