namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>编辑器构造与运行所需的行为配置（宿主可覆盖；默认值可直接使用）。</summary>
public sealed class MarkdownEditorOptions
{
    /// <summary>链接模板占位地址。</summary>
    public string LinkUrlPlaceholder { get; set; } = "https://example.com";

    /// <summary>图片模板占位目标。</summary>
    public string ImageTargetPlaceholder { get; set; } = "image.png";

    /// <summary>代码围栏默认语言。</summary>
    public string CodeFenceLanguage { get; set; } = "csharp";

    /// <summary>自动配对开关（成对符号插入、选区包裹、空配对退格删除）。</summary>
    public bool EnableAutoPair { get; set; } = true;

    /// <summary>是否显示行号。</summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>编辑器字号。</summary>
    public double FontSize { get; set; } = 15d;
}
