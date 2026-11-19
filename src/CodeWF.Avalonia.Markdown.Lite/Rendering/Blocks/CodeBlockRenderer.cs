using global::Avalonia.Controls;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 代码块渲染器：左侧标题（语言 · 路径#行段）+ 右侧工具条（折叠/换行/复制/下载），
/// 受理 FencedCodeBlock 与缩进 CodeBlock。外观与流式原地更新见 <see cref="CodeBlockFrame" />。
/// </summary>
internal sealed class CodeBlockRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        // YAML 元数据继承 CodeBlock，交给专用元数据卡而非代码工具栏。
        if (block is not CodeBlock codeBlock || block is Markdig.Extensions.Yaml.YamlFrontMatterBlock)
        {
            return null;
        }

        // 块键 = 原始源码起始偏移：相对 Span 平移解析输入原点（分段/尾部解析时非 0），
        // 保证折叠状态与高亮会话的键跨快照稳定（规格：块键 = 原始源码起始偏移）。
        var blockKey = context.CurrentBlockOrigin + Math.Max(codeBlock.Span.Start, 0);
        return new CodeBlockFrame(blockKey, codeBlock, context);
    }
}
