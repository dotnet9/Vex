using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

using CodeWF.Avalonia.Markdown.Helpers;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 自绘可选中文本段：段落/标题等文本块的轻量渲染（一个 Control + 一个
/// <see cref="IncrementalTextLayout"/>，无控件子树）——块单位成本比
/// SelectableTextBlock 控件树低一个数量级。
/// 支持：行内样式（粗/斜/删除线/==高亮==/行内代码/链接）、跨块选区接管
/// （<see cref="IMarkdownTextSelectionVisual"/>）、链接命中与点击打开。
/// </summary>
public sealed class MarkdownTextSegmentView : Control, IMarkdownTextSelectionVisual
{
	public static readonly StyledProperty<MarkdownSegmentContent?> ContentProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, MarkdownSegmentContent?>(nameof(Content));

	public static readonly StyledProperty<IBrush?> ForegroundProperty =
		TextBlock.ForegroundProperty.AddOwner<MarkdownTextSegmentView>();

	public static readonly StyledProperty<FontFamily> FontFamilyProperty =
		TextBlock.FontFamilyProperty.AddOwner<MarkdownTextSegmentView>();

	public static readonly StyledProperty<double> FontSizeProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, double>(nameof(FontSize), 14);

	public static readonly StyledProperty<FontWeight> FontWeightProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, FontWeight>(nameof(FontWeight), FontWeight.Normal);

	public static readonly StyledProperty<FontStyle> FontStyleProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, FontStyle>(nameof(FontStyle), FontStyle.Normal);

	public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
		TextBlock.TextAlignmentProperty.AddOwner<MarkdownTextSegmentView>();

	public static readonly StyledProperty<double> LineHeightProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, double>(nameof(LineHeight), double.NaN);

	public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, IBrush?>(nameof(SelectionBrush));

	public static readonly StyledProperty<IBrush?> LinkForegroundProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, IBrush?>(nameof(LinkForeground));

	public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, FontFamily>(nameof(CodeFontFamily),
			new FontFamily("Consolas, JetBrains Mono, Courier New, monospace"));

	public static readonly StyledProperty<IBrush?> CodeBackgroundProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, IBrush?>(nameof(CodeBackground),
			new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)));

	public static readonly StyledProperty<IBrush?> HighlightBackgroundProperty =
		AvaloniaProperty.Register<MarkdownTextSegmentView, IBrush?>(nameof(HighlightBackground),
			new SolidColorBrush(Color.FromArgb(100, 255, 213, 79)));

	/// <summary>链接被点击（打开之前触发；宿主可置 Cancel 阻止）。</summary>
	public event EventHandler<MarkdownLinkClickEventArgs>? LinkActivated;

	private MarkdownSegmentContent? _content;
	private IReadOnlyList<MarkdownLinkSpan> _links = [];
	private readonly List<(string Text, GenericTextRunProperties Props)> _runs = [];
	private IncrementalTextLayout? _layout;
	private TextRunCache? _runCache;
	private int _textLength;
	private int _selectionFrom = -1;
	private int _selectionTo = -1;
	private bool _contentDirty;

	public MarkdownSegmentContent? Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	public IBrush? Foreground
	{
		get => GetValue(ForegroundProperty);
		set => SetValue(ForegroundProperty, value);
	}

	public FontFamily FontFamily
	{
		get => GetValue(FontFamilyProperty);
		set => SetValue(FontFamilyProperty, value);
	}

	public double FontSize
	{
		get => GetValue(FontSizeProperty);
		set => SetValue(FontSizeProperty, value);
	}

	public FontWeight FontWeight
	{
		get => GetValue(FontWeightProperty);
		set => SetValue(FontWeightProperty, value);
	}

	public FontStyle FontStyle
	{
		get => GetValue(FontStyleProperty);
		set => SetValue(FontStyleProperty, value);
	}

	public TextAlignment TextAlignment
	{
		get => GetValue(TextAlignmentProperty);
		set => SetValue(TextAlignmentProperty, value);
	}

	public double LineHeight
	{
		get => GetValue(LineHeightProperty);
		set => SetValue(LineHeightProperty, value);
	}

	public IBrush? SelectionBrush
	{
		get => GetValue(SelectionBrushProperty);
		set => SetValue(SelectionBrushProperty, value);
	}

	public IBrush? LinkForeground
	{
		get => GetValue(LinkForegroundProperty);
		set => SetValue(LinkForegroundProperty, value);
	}

	public FontFamily CodeFontFamily
	{
		get => GetValue(CodeFontFamilyProperty);
		set => SetValue(CodeFontFamilyProperty, value);
	}

	public IBrush? CodeBackground
	{
		get => GetValue(CodeBackgroundProperty);
		set => SetValue(CodeBackgroundProperty, value);
	}

	public IBrush? HighlightBackground
	{
		get => GetValue(HighlightBackgroundProperty);
		set => SetValue(HighlightBackgroundProperty, value);
	}

	public string PlainText => _content?.Text ?? string.Empty;

	/// <summary>命中处的链接（本地坐标）；不在链接上返回 null。</summary>
	public MarkdownLinkSpan? HitTestLink(Point localPoint)
	{
		if (_layout is null || _links.Count == 0)
		{
			return null;
		}

		var offset = Math.Clamp(_layout.HitTestPoint(localPoint).TextPosition, 0, _content?.Text.Length ?? 0);
		foreach (var link in _links)
		{
			if (offset >= link.Start && offset < link.End)
			{
				return link;
			}
		}

		return null;
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == ContentProperty)
		{
			_content = change.GetNewValue<MarkdownSegmentContent?>();
			_links = _content?.Links ?? [];
			_contentDirty = true;
		}

		if (_content is null)
		{
			return;
		}

		RebuildRuns();
		InvalidateMeasure();
		InvalidateVisual();
	}

	private void RebuildRuns()
	{
		_runs.Clear();
		_textLength = _content?.Text.Length ?? 0;
		if (_content is null)
		{
			return;
		}

		var text = _content.Text;
		var baseTypeface = new Typeface(FontFamily, FontStyle, FontWeight);
		var cursor = 0;
		foreach (var span in _content.Styles)
		{
			if (span.Start > cursor)
			{
				var normalLength = span.Start - cursor;
				_runs.Add((text.Substring(cursor, normalLength), PropsFor(baseTypeface, default)));
				cursor += normalLength;
			}

			var typeface = new Typeface(span.Style.Code ? CodeFontFamily : FontFamily, FontStyle,
				span.Style.Bold ? FontWeight.Bold : FontWeight);
			_runs.Add((text.Substring(span.Start, span.Length), PropsFor(typeface, span.Style)));
			cursor = span.Start + span.Length;
		}

		if (cursor < text.Length)
		{
			_runs.Add((text.Substring(cursor), PropsFor(baseTypeface, default)));
		}
	}

	private GenericTextRunProperties PropsFor(Typeface typeface, MarkdownSegmentStyle style)
	{
		TextDecorationCollection? decorations = null;
		if (style.Strike)
		{
			decorations = TextDecorations.Strikethrough;
		}
		else if (style.Link)
		{
			decorations = TextDecorations.Underline;
		}

		IBrush? foreground = style.Link ? LinkForeground ?? Foreground : Foreground;
		IBrush? background = style.Code ? CodeBackground : style.Highlight ? HighlightBackground : null;

#pragma warning disable CS0618
		return new GenericTextRunProperties(typeface, fontRenderingEmSize: FontSize, textDecorations: decorations,
			foregroundBrush: foreground, backgroundBrush: background);
#pragma warning restore CS0618
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		EnsureLayout(availableSize.Width);
		var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _layout?.Width ?? 0;
		return new Size(width, _layout?.Height ?? 0);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		EnsureLayout(finalSize.Width);
		return finalSize;
	}

	private void EnsureLayout(double width)
	{
		if (_content is null)
		{
			_layout?.Dispose();
			_layout = null;
			return;
		}

		if (_layout is not null && width > 0 && width.Equals(_layout.MaxWidth))
		{
			return;
		}

		if (_contentDirty || _layout is null)
		{
			_runCache?.Dispose();
#pragma warning disable CS0618
			_runCache = new TextRunCache();
#pragma warning restore CS0618
			_contentDirty = false;
		}

		_layout ??= new IncrementalTextLayout();
		var defaultProps = new GenericTextRunProperties(new Typeface(FontFamily, FontStyle, FontWeight),
			fontRenderingEmSize: FontSize, foregroundBrush: Foreground);
		var fixedHeight = new GenericTextParagraphProperties(defaultProps, TextAlignment, TextWrapping.Wrap, LineHeight);
		var natural = new GenericTextParagraphProperties(defaultProps, TextAlignment, TextWrapping.Wrap);
		_layout.Format(new SegmentRunSource(this), _textLength, width, fixedHeight, natural, 0, _runCache);
		InvalidateVisual();
	}

	private TextRun? GetTextRunAt(int textSourceIndex)
	{
		var offset = 0;
		foreach (var (text, props) in _runs)
		{
			if (textSourceIndex < offset + text.Length)
			{
				return new TextCharacters(text.AsMemory(textSourceIndex - offset), props);
			}

			offset += text.Length;
		}

		return null;
	}

	private sealed class SegmentRunSource(MarkdownTextSegmentView owner) : ITextSource
	{
		public TextRun? GetTextRun(int textSourceIndex) => owner.GetTextRunAt(textSourceIndex);
	}

	public override void Render(DrawingContext context)
	{
		if (_layout is null || _content is null)
		{
			return;
		}

		if (_selectionFrom >= 0 && _selectionTo > _selectionFrom)
		{
			var brush = ResolveSelectionBrush();
			foreach (var rect in _layout.HitTestTextRange(_selectionFrom, _selectionTo - _selectionFrom))
			{
				context.DrawRectangle(brush, null, rect, 2, 2);
			}
		}

		var top = 0d;
		for (var i = 0; i < _layout.TextLines.Count; i++)
		{
			_layout.TextLines[i].Draw(context, new Point(0, top));
			top += _layout.TextLines[i].Height;
		}
	}

	private IBrush ResolveSelectionBrush()
	{
		if (SelectionBrush is { } brush)
		{
			return brush;
		}

		if (Foreground is ISolidColorBrush solid)
		{
			var color = solid.Color;
			return new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B));
		}

		return new SolidColorBrush(Color.FromArgb(60, 100, 100, 100));
	}

	int IMarkdownTextSelectionVisual.HitTestOffset(Point pointInBlock)
	{
		if (_layout is null || _content is null)
		{
			return 0;
		}

		return Math.Clamp(_layout.HitTestPoint(pointInBlock).TextPosition, 0, _content.Text.Length);
	}

	void IMarkdownTextSelectionVisual.ApplySelection(int from, int to)
	{
		_selectionFrom = Math.Clamp(from, 0, _content?.Text.Length ?? 0);
		_selectionTo = Math.Clamp(to, _selectionFrom, _content?.Text.Length ?? 0);
		InvalidateVisual();
	}

	void IMarkdownTextSelectionVisual.ClearSelection()
	{
		_selectionFrom = -1;
		_selectionTo = -1;
		InvalidateVisual();
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		var link = HitTestLink(e.GetPosition(this));
		Cursor = link is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		if (e.InitialPressMouseButton != MouseButton.Left)
		{
			return;
		}

		var link = HitTestLink(e.GetPosition(this));
		if (link is null)
		{
			return;
		}

		var args = new MarkdownLinkClickEventArgs
		{
			Url = link.Url,
			LinkText = link.Text,
			RequiresConfirmation = MarkdownUrlPolicy.IsMisleadingLinkText(link.Text, link.Url),
		};
		LinkActivated?.Invoke(this, args);
		if (!args.Cancel && !string.IsNullOrWhiteSpace(link.Url))
		{
			UrlHelper.Open(link.Url);
		}

		e.Handled = true;
	}
}
