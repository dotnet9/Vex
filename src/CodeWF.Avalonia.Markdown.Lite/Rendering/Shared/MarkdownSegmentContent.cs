using System.Text;
using System.Text.RegularExpressions;

using Markdig.Extensions.TaskLists;
using CodeWF.Avalonia.Markdown.Rendering;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>自绘文本段的行内样式（可叠加；渲染端按组合求字体/装饰/底色）。</summary>
public readonly record struct MarkdownSegmentStyle(
	bool Bold,
	bool Italic,
	bool Strike,
	bool Highlight,
	bool Code,
	bool Link);

public readonly record struct MarkdownSegmentStyleSpan(int Start, int Length, MarkdownSegmentStyle Style);

/// <summary>
/// 自绘文本段的内容：纯文本 + 样式区间 + 链接区间（偏移一致，命中/复制共用）。
/// 由 <see cref="MarkdownSegmentContentBuilder"/> 从 Markdig 内联树构建。
/// </summary>
public sealed record MarkdownSegmentContent(
	string Text,
	IReadOnlyList<MarkdownSegmentStyleSpan> Styles,
	IReadOnlyList<MarkdownLinkSpan> Links);

/// <summary>
/// Markdig 内联树 → 自绘文本段内容。
/// 纯文本构建规则与 <see cref="MarkdownLinkInteraction.ExtractLinkSpans"/> 同源（链接偏移一致的前提）。
/// v1 支持内联：字面量、强调（粗/斜/删除线/==高亮==）、行内代码、链接、硬换行、
/// 任务列表前缀剥离、普通容器；v1 不支持 → TryBuild 返回 false，所在块回退既有
/// SelectableTextBlock 路径：内联图片、行内公式、脚注引用、上下标、非 br 内联 HTML。
/// </summary>
internal static class MarkdownSegmentContentBuilder
{
	private static readonly Regex LineBreakTagPattern =
		new(@"^<br\s*/?>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	public static bool IsSupported(ContainerInline? container, bool softBreakAsNewline) =>
		TryBuild(container, softBreakAsNewline, out _, out _);

	public static bool TryBuild(
		ContainerInline? container,
		bool softBreakAsNewline,
		out MarkdownSegmentContent content,
		out IReadOnlyList<MarkdownLinkSpan> links)
	{
		var plain = new StringBuilder();
		var styles = new List<MarkdownSegmentStyleSpan>();
		var linkSpans = new List<MarkdownLinkSpan>();
		var unsupported = false;
		var stripTaskPrefix = false;

		Walk(container, 0, ref stripTaskPrefix, softBreakAsNewline, plain, styles, linkSpans,
			new MarkdownSegmentStyle(), ref unsupported);

		if (unsupported)
		{
			content = null!;
			links = [];
			return false;
		}

		content = new MarkdownSegmentContent(plain.ToString(), styles, linkSpans);
		links = linkSpans;
		return true;
	}

	/// <summary>遍历内联树；文本经 Emit 记录样式区间并推进偏移，返回推进后的偏移。</summary>
	private static int Walk(
		ContainerInline? container,
		int offset,
		ref bool stripTaskPrefix,
		bool softBreakAsNewline,
		StringBuilder plain,
		List<MarkdownSegmentStyleSpan> styles,
		List<MarkdownLinkSpan> links,
		MarkdownSegmentStyle style,
		ref bool unsupported)
	{
		var child = container?.FirstChild;
		while (child is not null)
		{
			switch (child)
			{
				case LiteralInline literal:
					stripTaskPrefix = false;
					var literalText = literal.Content.ToString();
					if (stripTaskPrefix && MarkdownTaskListHelper.TryStripTaskPrefix(literalText, out var stripped))
					{
						if (!string.IsNullOrWhiteSpace(stripped))
						{
							var trimmed = stripped.TrimStart();
							offset += trimmed.Length;
							Emit(plain, styles, trimmed, offset, style);
						}
					}
					else
					{
						offset += literalText.Length;
						Emit(plain, styles, literalText, offset, style);
					}
					break;

				case CodeInline code:
					stripTaskPrefix = false;
					var codeText = code.Content ?? string.Empty;
					offset += codeText.Length;
					Emit(plain, styles, codeText, offset, style with { Code = true });
					break;

				case LineBreakInline lineBreak:
					stripTaskPrefix = false;
					var br = lineBreak.IsHard || softBreakAsNewline ? "\n" : " ";
					offset += br.Length;
					Emit(plain, styles, br, offset, style);
					break;

				case TaskList:
					stripTaskPrefix = true;
					break;

				case LinkInline { IsImage: true }:
					unsupported = true;
					break;

				case LinkInline { IsImage: false } link:
					stripTaskPrefix = false;
					var start = offset;
					var startLength = plain.Length;
					offset = Walk(link, offset, ref stripTaskPrefix, softBreakAsNewline, plain, styles, links,
						style with { Link = true }, ref unsupported);
					if (offset == start && !string.IsNullOrWhiteSpace(link.Url))
					{
						var urlText = link.Url!;
						offset += urlText.Length;
						Emit(plain, styles, urlText, offset, style with { Link = true });
					}

					if (offset > start && !string.IsNullOrWhiteSpace(link.Url))
					{
						links.Add(new MarkdownLinkSpan(start, offset, link.Url!, plain.ToString(startLength, offset - start)));
					}
					break;

				case EmphasisInline emphasis:
					stripTaskPrefix = false;
					if (emphasis.DelimiterChar is '^' or '~' && emphasis.DelimiterCount == 1)
					{
						// 上下标：自绘段 v1 不做基线偏移，回退旧路径。
						unsupported = true;
						break;
					}
					var inner = style with
					{
						Bold = style.Bold || (emphasis.DelimiterCount >= 2 && emphasis.DelimiterChar is '*' or '_'),
						Italic = style.Italic || (emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount == 1),
						Strike = style.Strike || (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount >= 2),
						Highlight = style.Highlight || (emphasis.DelimiterChar == '=' && emphasis.DelimiterCount == 2),
					};
					offset = Walk(emphasis, offset, ref stripTaskPrefix, softBreakAsNewline, plain, styles, links, inner, ref unsupported);
					break;

				case Markdig.Extensions.Footnotes.FootnoteLink:
				case Markdig.Extensions.Mathematics.MathInline:
					unsupported = true;
					break;

				case HtmlInline html:
					stripTaskPrefix = false;
					if (LineBreakTagPattern.IsMatch(html.Tag))
					{
						offset += 1;
						Emit(plain, styles, "\n", offset, style);
					}
					else
					{
						unsupported = true;
					}
					break;

				case ContainerInline nested:
					stripTaskPrefix = false;
					offset = Walk(nested, offset, ref stripTaskPrefix, softBreakAsNewline, plain, styles, links, style, ref unsupported);
					break;

				default:
					stripTaskPrefix = false;
					var fallbackText = child.ToString() ?? string.Empty;
					if (!MarkdownPlainTextExtractor.IsTypeNameFallback(fallbackText, child.GetType()))
					{
						offset += fallbackText.Length;
						Emit(plain, styles, fallbackText, offset, style);
					}
					break;
			}

			child = child.NextSibling;
		}

		return offset;
	}

	/// <summary>记录样式区间并追加纯文本。空文本跳过（不产生零长样式）。</summary>
	private static void Emit(
		StringBuilder plain,
		List<MarkdownSegmentStyleSpan> styles,
		string text,
		int endOffset,
		MarkdownSegmentStyle style)
	{
		if (text.Length == 0)
		{
			return;
		}

		var start = endOffset - text.Length;
		plain.Append(text);
		if (style is { Bold: false, Italic: false, Strike: false, Highlight: false, Code: false, Link: false })
		{
			return;
		}

		// 相邻同款式区间合并，控制 run 数量。
		if (styles.Count > 0 && styles[^1] is { } last
			&& last.Start + last.Length == start
			&& last.Style.Equals(style))
		{
			styles[^1] = new MarkdownSegmentStyleSpan(last.Start, last.Length + text.Length, style);
			return;
		}

		styles.Add(new MarkdownSegmentStyleSpan(start, text.Length, style));
	}
}
