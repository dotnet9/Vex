using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;

namespace CodeWF.Avalonia.Markdown.Highlighting;

/// <summary>
/// 代码高亮能力接入扩展：注册后代码块使用 TextMate 语法高亮；
/// 不注册时由 Core 降级为单色等宽渲染。
/// </summary>
public static class CodeWFMarkdownHighlightingExtensions
{
    /// <summary>全局注册高亮能力，应用启动时调用一次；可传入常用语言预热语法装载（如 "csharp"、"bash"）。</summary>
    public static void UseHighlighting(params string[] prewarmLanguages)
    {
        MarkdownViewer.RegisterCodeHighlighter(CodeHighlighter.Render);
        if (prewarmLanguages is { Length: > 0 })
        {
            CodeHighlighter.Prewarm(prewarmLanguages);
        }
    }

    /// <summary>按实例注册高亮能力（覆盖全局注册）。</summary>
    public static MarkdownViewer UseHighlighting(this MarkdownViewer viewer)
    {
        viewer.CodeHighlighter = CodeHighlighter.Render;
        return viewer;
    }
}
