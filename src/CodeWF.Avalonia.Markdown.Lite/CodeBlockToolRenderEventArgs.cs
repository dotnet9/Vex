using Avalonia.Controls;

using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// 代码块工具栏渲染完成后的事件参数，用于让调用方追加自定义操作按钮。
/// </summary>
public sealed class CodeBlockToolRenderEventArgs : EventArgs
{
    public CodeBlockToolRenderEventArgs(StackPanel headerPanel, StackPanel contentPanel, CodeBlock codeBlock)
    {
        HeaderPanel = headerPanel;
        ContentPanel = contentPanel;
        CodeBlock = codeBlock;
    }

    /// <summary>
    /// 代码块头部工具栏容器。
    /// </summary>
    public StackPanel HeaderPanel { get; }

    /// <summary>
    /// 代码内容容器。
    /// </summary>
    public StackPanel ContentPanel { get; }

    /// <summary>
    /// Markdig 解析得到的原始代码块节点。
    /// </summary>
    public CodeBlock CodeBlock { get; }
}
