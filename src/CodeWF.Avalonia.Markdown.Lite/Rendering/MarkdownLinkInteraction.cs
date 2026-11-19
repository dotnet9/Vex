using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Helpers;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>渲染文本中的链接区间（纯文本偏移 → URL，附区间显示文字）。</summary>
internal sealed record MarkdownLinkSpan(int Start, int End, string Url, string? Text = null);

/// <summary>
/// 链接区间提取与指针交互：提取内联树中的链接纯文本区间，
/// 挂接手型光标与点击打开行为，并支持命中查询。
/// </summary>
internal static class MarkdownLinkInteraction
{
    public static IReadOnlyList<MarkdownLinkSpan> ExtractLinkSpans(ContainerInline? container, bool stripTaskPrefix = false)
    {
        var links = new List<MarkdownLinkSpan>();
        var plain = new System.Text.StringBuilder();
        CollectLinkSpans(container, 0, links, plain, ref stripTaskPrefix);
        return links;
    }

    private static int CollectLinkSpans(
        ContainerInline? container,
        int offset,
        ICollection<MarkdownLinkSpan> links,
        System.Text.StringBuilder plain,
        ref bool stripTaskPrefix)
    {
        var child = container?.FirstChild;
        while (child is not null)
        {
            switch (child)
            {
                case LiteralInline literal:
                    var literalText = literal.Content.ToString();
                    if (stripTaskPrefix && MarkdownTaskListHelper.TryStripTaskPrefix(literalText, out var stripped))
                    {
                        stripTaskPrefix = false;
                        if (!string.IsNullOrWhiteSpace(stripped))
                        {
                            var trimmed = stripped.TrimStart();
                            offset += trimmed.Length;
                            plain.Append(trimmed);
                        }
                    }
                    else
                    {
                        stripTaskPrefix = false;
                        offset += literalText.Length;
                        plain.Append(literalText);
                    }

                    break;
                case CodeInline code:
                    stripTaskPrefix = false;
                    offset += code.Content.Length;
                    plain.Append(code.Content);
                    break;
                case LineBreakInline:
                    stripTaskPrefix = false;
                    offset += Environment.NewLine.Length;
                    plain.Append(Environment.NewLine);
                    break;
                case TaskList:
                    stripTaskPrefix = true;
                    break;
                case LinkInline { IsImage: true } image:
                    stripTaskPrefix = false;
                    var imageText = MarkdownPlainTextExtractor.ExtractImageText(image);
                    offset += imageText.Length;
                    plain.Append(imageText);
                    break;
                case LinkInline { IsImage: false } link:
                    stripTaskPrefix = false;
                    var start = offset;
                    var startLength = plain.Length;
                    offset = CollectLinkSpans(link, offset, links, plain, ref stripTaskPrefix);
                    if (offset == start && !string.IsNullOrWhiteSpace(link.Url))
                    {
                        offset += link.Url!.Length;
                        plain.Append(link.Url);
                    }

                    if (offset > start && !string.IsNullOrWhiteSpace(link.Url))
                    {
                        links.Add(new MarkdownLinkSpan(start, offset, link.Url!, plain.ToString(startLength, offset - start)));
                    }

                    break;
                case ContainerInline nested:
                    stripTaskPrefix = false;
                    offset = CollectLinkSpans(nested, offset, links, plain, ref stripTaskPrefix);
                    break;
                default:
                    stripTaskPrefix = false;
                    var text = child.ToString() ?? string.Empty;
                    if (!MarkdownPlainTextExtractor.IsTypeNameFallback(text, child.GetType()))
                    {
                        offset += text.Length;
                        plain.Append(text);
                    }

                    break;
            }

            child = child.NextSibling;
        }

        return offset;
    }

    public static void AttachLinkInteraction(
        SelectableTextBlock textBlock,
        IReadOnlyList<MarkdownLinkSpan> links,
        IMarkdownRenderContext context)
    {
        if (links.Count == 0)
        {
            return;
        }

        textBlock.Tag = links;
        textBlock.PointerMoved += (_, e) =>
        {
            string? ignored;
            textBlock.Cursor = TryGetLinkAtPointer(textBlock, e, out ignored)
                ? new Cursor(StandardCursorType.Hand)
                : null;
        };
        textBlock.PointerExited += (_, _) => textBlock.Cursor = null;
        textBlock.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left
                || !string.IsNullOrEmpty(textBlock.SelectedText)
                || !TryGetLinkAtPointer(textBlock, e, out var url))
            {
                return;
            }

            // 防钓鱼：链接文字与实际目标不符时打上确认标记，宿主决定是否先弹确认框。
            var linkText = TryGetLinkSpan(textBlock, url)?.Text;
            var args = new MarkdownLinkClickEventArgs
            {
                Url = url!,
                LinkText = linkText,
                RequiresConfirmation = MarkdownUrlPolicy.IsMisleadingLinkText(linkText, url),
            };
            context.RaiseLinkClicked(args);
            if (!args.Cancel && !string.IsNullOrWhiteSpace(url))
            {
                UrlHelper.Open(url!);
            }

            e.Handled = true;
        };
    }

    /// <summary>按 URL 反查链接区间。</summary>
    private static MarkdownLinkSpan? TryGetLinkSpan(SelectableTextBlock textBlock, string? url)
    {
        if (string.IsNullOrEmpty(url) || textBlock.Tag is not IReadOnlyList<MarkdownLinkSpan> links)
        {
            return null;
        }

        foreach (var link in links)
        {
            if (link.Url == url)
            {
                return link;
            }
        }

        return null;
    }

    public static bool TryGetLinkAtPointer(SelectableTextBlock textBlock, PointerEventArgs e, out string? url)
    {
        url = null;
        if (textBlock.Tag is not IReadOnlyList<MarkdownLinkSpan> links || links.Count == 0)
        {
            return false;
        }

        var hit = textBlock.TextLayout.HitTestPoint(e.GetPosition(textBlock));
        if (!hit.IsInside)
        {
            return false;
        }

        var textPosition = hit.TextPosition;
        foreach (var link in links)
        {
            if (textPosition >= link.Start && textPosition < link.End)
            {
                url = link.Url;
                return true;
            }
        }

        return false;
    }
}
