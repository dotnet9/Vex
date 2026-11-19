using CodeWF.Avalonia.Markdown.Controls;

namespace CodeWF.Avalonia.Markdown.Mermaid;

/// <summary>
/// Mermaid 能力包接入扩展：注册后所有新建的 <see cref="MarkdownViewer"/>
/// 都会把 ```mermaid 围栏代码块渲染为图表。
/// </summary>
public static class CodeWFMarkdownMermaidExtensions
{
    private static readonly object RegistrationLock = new();
    private static bool _registered;

    public static MarkdownViewer UseMermaid(this MarkdownViewer viewer)
    {
        EnsureRegistered();
        return viewer;
    }

    public static void EnsureRegistered()
    {
        lock (RegistrationLock)
        {
            if (_registered)
            {
                return;
            }

            MarkdownViewer.RegisterBlockRenderer(new MermaidBlockRenderer());
            _registered = true;
        }
    }
}
