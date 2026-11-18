using global::Avalonia;
using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 图片 Markdown 源在整篇文档中的绝对区间（渲染时记录），供图片能力包做
/// Typora 式「图上方源码编辑」的精确回写替换。区间不含段落缩进等容器前缀。
/// </summary>
public sealed record MarkdownImageEditSpan(int Start, int Length);

/// <summary>
/// 图片控件与源码区间的附件：仅独立成段的单图（SpecialBlockRenderer 的单图段落）
/// 记录区间——行内图片、幻灯片不记录，图片能力包据此保持原有「点击放大」交互。
/// </summary>
public sealed class MarkdownImageEdit : AvaloniaObject
{
    public static readonly AttachedProperty<MarkdownImageEditSpan?> SourceSpanProperty =
        AvaloniaProperty.RegisterAttached<MarkdownImageEdit, Control, MarkdownImageEditSpan?>("SourceSpan");

    public static MarkdownImageEditSpan? GetSourceSpan(Control control) => control.GetValue(SourceSpanProperty);

    public static void SetSourceSpan(Control control, MarkdownImageEditSpan? value) =>
        control.SetValue(SourceSpanProperty, value);
}
