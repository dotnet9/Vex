using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;

namespace CodeWF.Avalonia.Markdown.Mermaid;

/// <summary>在阅读图表中启用原位源码编辑；宿主负责将提交写入文档及撤销栈。</summary>
public sealed class MermaidEditing : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<MermaidEditing, MarkdownViewer, bool>("IsEnabled");

    public static bool GetIsEnabled(MarkdownViewer viewer) => viewer.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(MarkdownViewer viewer, bool value) => viewer.SetValue(IsEnabledProperty, value);

    /// <summary>保存、关闭或切换文档前提交仍有焦点的图表源码（含图片源码，二者共用 EditCommitted 回写通道）。</summary>
    public static void CommitPendingEdits(MarkdownViewer viewer)
    {
        foreach (var diagram in viewer.GetVisualDescendants().OfType<MermaidDiagramView>().ToArray()) diagram.Commit();
        foreach (var image in viewer.GetVisualDescendants().OfType<MarkdownImage>().ToArray()) image.CommitPendingSourceEdit();
    }

    public static readonly RoutedEvent<MermaidEditEventArgs> EditCommittedEvent =
        RoutedEvent.Register<MermaidEditing, MermaidEditEventArgs>("EditCommitted", RoutingStrategies.Bubble);

    public static void AddEditCommittedHandler(Interactive target, EventHandler<MermaidEditEventArgs> handler) =>
        target.AddHandler(EditCommittedEvent, handler);
    public static void RemoveEditCommittedHandler(Interactive target, EventHandler<MermaidEditEventArgs> handler) =>
        target.RemoveHandler(EditCommittedEvent, handler);
}

/// <summary>图表源码的完整文档快照与替换起点；原文不匹配时宿主应拒绝过期回写。</summary>
public sealed class MermaidEditEventArgs(string originalMarkdown, string markdown, int start)
    : RoutedEventArgs(MermaidEditing.EditCommittedEvent)
{
    public string OriginalMarkdown { get; } = originalMarkdown;
    public string Markdown { get; } = markdown;
    public int Start { get; } = start;
}
