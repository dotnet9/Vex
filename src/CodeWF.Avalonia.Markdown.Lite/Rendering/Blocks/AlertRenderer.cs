using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using Markdig.Extensions.Alerts;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// GitHub Alerts（GFM，FR-07）：[!NOTE]/[!TIP]/[!IMPORTANT]/[!WARNING]/[!CAUTION]
/// 渲染为「左边框 + 软底 + 类别标签 + 内容」的提示卡。颜色经资源键
/// CodeWFMarkdownCallout{Kind}BorderBrush / BackgroundBrush 随主题与配色解析，
/// 缺省回退到状态色全局默认（规则附录 A 状态色）。
/// </summary>
internal sealed class AlertRenderer : IMarkdownBlockRenderer
{
    private static readonly (string Kind, string Label, string Border, string Fill)[] Kinds =
    [
        ("note", "备注", "#4338CA", "#4338CA"),
        ("tip", "提示", "#15803D", "#15803D"),
        ("important", "重要", "#6745D6", "#6745D6"),
        ("warning", "警告", "#B45309", "#B45309"),
        ("caution", "注意", "#DC2626", "#DC2626"),
    ];

    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not AlertBlock alert)
        {
            return null;
        }

        var kind = alert.Kind.ToString().ToLowerInvariant();
        var meta = Kinds.FirstOrDefault(item => item.Kind == kind);
        if (string.IsNullOrEmpty(meta.Kind))
        {
            meta = Kinds[0];
        }

        var capital = char.ToUpperInvariant(meta.Kind[0]) + meta.Kind[1..];

        var border = new Border();
        context.AddMarkdownClass(border, MarkdownStyleKeys.Quote);
        border.Classes.Add("MdCallout");
        border.Classes.Add("MdCallout" + capital);
        border.BorderThickness = new Thickness(0, 0, 0, 0);
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable(
            "CodeWFMarkdownCallout" + capital + "BorderBrush", v => v ?? new SolidColorBrush(Color.Parse(meta.Border))));
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable(
            "CodeWFMarkdownCallout" + capital + "BackgroundBrush", v => v ?? new SolidColorBrush(Color.Parse(meta.Fill)) { Opacity = 0.10 }));

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        var label = new TextBlock
        {
            Text = meta.Label,
            FontWeight = FontWeight.SemiBold,
            FontSize = context.ParagraphFontSize * 0.9,
            Margin = new Thickness(0, 0, 0, 4),
        };
        label.Bind(TextBlock.ForegroundProperty, border.GetResourceObservable(
            "CodeWFMarkdownCallout" + capital + "BorderBrush", v => v ?? new SolidColorBrush(Color.Parse(meta.Border))));
        stack.Children.Add(label);

        foreach (var child in (QuoteBlock)alert)
        {
            var control = context.ConvertBlock(child, null);
            if (control is not null)
            {
                stack.Children.Add(control);
            }
        }

        border.Child = stack;
        return border;
    }
}
