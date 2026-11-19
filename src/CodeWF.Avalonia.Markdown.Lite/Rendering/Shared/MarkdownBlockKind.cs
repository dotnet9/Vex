using Markdig.Syntax;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

public enum MarkdownBlockKind
{
	Unknown,
	Paragraph,
	Heading,
	Code,
	List,
	Quote,
	ThematicBreak,
	Table,
	Footnote,
	FootnoteGroup,
	Html,
	LinkReference,
	Toc,
	Math,
	Image,
	Slide
}

[Flags]
public enum MarkdownDependencyFlags
{
	None = 0,
	Toc = 1,
	Footnote = 1 << 1,
	LinkReference = 1 << 2,
	OrderedList = 1 << 3,
	TableShape = 1 << 4,
	Global = Toc | Footnote | LinkReference
}

internal static class MarkdownBlockClassifier
{
	public static MarkdownBlockKind GetKind(Block block, string sourceText)
	{
		if (IsToc(block, sourceText))
		{
			return MarkdownBlockKind.Toc;
		}

		return block switch
		{
			HeadingBlock => MarkdownBlockKind.Heading,
			_ when IsMath(block, sourceText) => MarkdownBlockKind.Math,
			ParagraphBlock paragraph when IsImageParagraph(paragraph) => MarkdownBlockKind.Image,
			ParagraphBlock => MarkdownBlockKind.Paragraph,
			FencedCodeBlock => MarkdownBlockKind.Code,
			CodeBlock when block is not Markdig.Extensions.Yaml.YamlFrontMatterBlock => MarkdownBlockKind.Code,
			ListBlock => MarkdownBlockKind.List,
			QuoteBlock => MarkdownBlockKind.Quote,
			ThematicBreakBlock => MarkdownBlockKind.ThematicBreak,
			Table => MarkdownBlockKind.Table,
			FootnoteGroup => MarkdownBlockKind.FootnoteGroup,
			Footnote => MarkdownBlockKind.Footnote,
			HtmlBlock => MarkdownBlockKind.Html,
			LinkReferenceDefinitionGroup => MarkdownBlockKind.LinkReference,
			LinkReferenceDefinition => MarkdownBlockKind.LinkReference,
			_ => MarkdownBlockKind.Unknown
		};
	}

	/// <summary>
	/// 是否为「重块」：渲染成本高（代码高亮、图片解码、图表布局+SVG 光栅化、公式排版）
	/// 或高度远大于一个段落。宿主据此在滚动离屏后保留控件不解体，
	/// 避免反复付出重建成本（见 WindowedStackPanel.KeepAliveBudget）。
	/// <para>
	/// 代码块按围栏信息判定：<c>```mermaid</c> 由能力包渲染成图表（列布局 + SVG 光栅化），
	/// 成本远高于普通代码块。
	/// </para>
	/// </summary>
	public static bool IsHeavy(Block block, MarkdownBlockKind kind)
	{
		if (kind is MarkdownBlockKind.Image or MarkdownBlockKind.Table or MarkdownBlockKind.Math)
		{
			return true;
		}

		return kind switch
		{
			MarkdownBlockKind.Code => block is not FencedCodeBlock { Info: not null } fenced
									|| !string.Equals(fenced.Info.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase),
			_ => false
		};
	}

	public static MarkdownDependencyFlags GetDependencyFlags(Block block, MarkdownBlockKind kind)
	{		var flags = MarkdownDependencyFlags.None;
		if (kind == MarkdownBlockKind.Toc)
		{
			flags |= MarkdownDependencyFlags.Toc;
		}

		if (kind is MarkdownBlockKind.Footnote or MarkdownBlockKind.FootnoteGroup)
		{
			flags |= MarkdownDependencyFlags.Footnote;
		}

		if (kind == MarkdownBlockKind.LinkReference)
		{
			flags |= MarkdownDependencyFlags.LinkReference;
		}

		if (block is ListBlock { IsOrdered: true })
		{
			flags |= MarkdownDependencyFlags.OrderedList;
		}

		if (block is Table)
		{
			flags |= MarkdownDependencyFlags.TableShape;
		}

		return flags;
	}

	private static bool IsToc(Block block, string sourceText)
	{
		var typeName = block.GetType().Name;
		if (typeName.Contains("TableOfContents", StringComparison.OrdinalIgnoreCase)
			|| typeName.Equals("TocBlock", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return string.Equals(sourceText.Trim(), "[TOC]", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsImageParagraph(ParagraphBlock paragraph)
	{
		var first = paragraph.Inline?.FirstChild;
		if (first is not Markdig.Syntax.Inlines.LinkInline { IsImage: true })
		{
			return false;
		}

		for (var sibling = first.NextSibling; sibling is not null; sibling = sibling.NextSibling)
		{
			if (sibling is Markdig.Syntax.Inlines.LiteralInline literal
				&& string.IsNullOrWhiteSpace(literal.Content.ToString()))
			{
				continue;
			}

			return false;
		}

		return true;
	}

	private static bool IsMath(Block block, string sourceText)
	{
		if (block.GetType().Name.Contains("Math", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var trimmed = sourceText.Trim();
		return (trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal))
			   || (trimmed.StartsWith(@"\[", StringComparison.Ordinal) && trimmed.EndsWith(@"\]", StringComparison.Ordinal));
	}
}
