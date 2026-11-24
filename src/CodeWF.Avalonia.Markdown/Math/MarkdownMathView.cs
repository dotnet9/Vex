using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Media.Immutable;

using CodeWF.Avalonia.Markdown.Shared.Rendering;
using CSharpMath.Atom;
using CSharpMath.Rendering.FrontEnd;

namespace CodeWF.Avalonia.Markdown.MathRendering;

public sealed class MarkdownMathView : Control
{
	private readonly MarkdownMathPainter _painter = new();
	private string? _appliedLatex;
	private float? _appliedFontSize;
	private LineStyle? _appliedLineStyle;
	private bool? _appliedDisplayErrorInline;
	private Color? _appliedColor;

	public static readonly StyledProperty<string?> LaTeXProperty =
		AvaloniaProperty.Register<MarkdownMathView, string?>(nameof(LaTeX));

	public static readonly StyledProperty<IBrush?> ForegroundProperty =
		AvaloniaProperty.Register<MarkdownMathView, IBrush?>(nameof(Foreground), Brushes.Black);

	public static readonly StyledProperty<float> FontSizeProperty =
		AvaloniaProperty.Register<MarkdownMathView, float>(nameof(FontSize), 16);

	public static readonly StyledProperty<LineStyle> LineStyleProperty =
		AvaloniaProperty.Register<MarkdownMathView, LineStyle>(nameof(LineStyle), LineStyle.Text);

	public static readonly StyledProperty<bool> DisplayErrorInlineProperty =
		AvaloniaProperty.Register<MarkdownMathView, bool>(nameof(DisplayErrorInline), false);

	public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
		AvaloniaProperty.Register<MarkdownMathView, IBrush?>(nameof(SelectionBrush),
			new ImmutableSolidColorBrush(Color.FromArgb(0x66, 0x33, 0x99, 0xFF)));

	public string? LaTeX
	{
		get => GetValue(LaTeXProperty);
		set => SetValue(LaTeXProperty, value);
	}

	public IBrush? Foreground
	{
		get => GetValue(ForegroundProperty);
		set => SetValue(ForegroundProperty, value);
	}

	public float FontSize
	{
		get => GetValue(FontSizeProperty);
		set => SetValue(FontSizeProperty, value);
	}

	public LineStyle LineStyle
	{
		get => GetValue(LineStyleProperty);
		set => SetValue(LineStyleProperty, value);
	}

	public bool DisplayErrorInline
	{
		get => GetValue(DisplayErrorInlineProperty);
		set => SetValue(DisplayErrorInlineProperty, value);
	}

	public IBrush? SelectionBrush
	{
		get => GetValue(SelectionBrushProperty);
		set => SetValue(SelectionBrushProperty, value);
	}

	static MarkdownMathView()
	{
		LaTeXProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.OnPainterPropertyChanged());
		ForegroundProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.OnPainterPropertyChanged());
		FontSizeProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.OnPainterPropertyChanged());
		LineStyleProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.OnPainterPropertyChanged());
		DisplayErrorInlineProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.OnPainterPropertyChanged());
		SelectionBrushProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.InvalidateVisual());
		MarkdownFormulaState.SearchBrushProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.InvalidateVisual());
		MarkdownFormulaState.SelectionStartProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.InvalidateVisual());
		MarkdownFormulaState.SelectionEndProperty.Changed.AddClassHandler<MarkdownMathView>((view, _) => view.InvalidateVisual());
	}

	public MarkdownMathView()
	{
		ApplyPainter();
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		ApplyPainter();
		var bounds = _painter.Measure();
		return new Size(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);
		ApplyPainter();
		DrawFormulaState(context);
		var canvas = new AvaloniaMathCanvas(context, Bounds.Size);
		_painter.Draw(canvas, CSharpMath.Rendering.FrontEnd.TextAlignment.Center);
	}

	private void DrawFormulaState(DrawingContext context)
	{
		if (MarkdownFormulaState.GetSearchBrush(this) is { } searchBrush)
		{
			context.DrawRectangle(searchBrush, null, new Rect(Bounds.Size));
		}

		var textLength = MarkdownFormulaState.GetText(this)?.Length ?? 0;
		var start = MarkdownFormulaState.GetSelectionStart(this);
		var end = MarkdownFormulaState.GetSelectionEnd(this);
		if (textLength == 0 || start == end || SelectionBrush is not { } selectionBrush)
		{
			return;
		}

		var from = Math.Clamp(Math.Min(start, end), 0, textLength);
		var to = Math.Clamp(Math.Max(start, end), 0, textLength);
		var left = Bounds.Width * from / textLength;
		var right = Bounds.Width * to / textLength;
		context.DrawRectangle(selectionBrush, null, new Rect(left, 0, Math.Max(1, right - left), Bounds.Height));
	}

	private void OnPainterPropertyChanged()
	{
		ApplyPainter();
		InvalidateMeasure();
		InvalidateVisual();
	}

	private void ApplyPainter()
	{
		var latex = LaTeX ?? string.Empty;
		var fontSize = FontSize;
		var lineStyle = LineStyle;
		var displayErrorInline = DisplayErrorInline;
		var color = ToDrawingColor(Foreground);
		if (_appliedDisplayErrorInline != displayErrorInline)
		{
			_painter.DisplayErrorInline = displayErrorInline;
			_appliedDisplayErrorInline = displayErrorInline;
		}

		if (_appliedFontSize != fontSize)
		{
			_painter.FontSize = fontSize;
			_appliedFontSize = fontSize;
		}

		if (_appliedLineStyle != lineStyle)
		{
			_painter.LineStyle = lineStyle;
			_appliedLineStyle = lineStyle;
		}

		if (_appliedLatex != latex)
		{
			_painter.LaTeX = latex;
			_appliedLatex = latex;
		}

		if (_appliedColor != color)
		{
			_painter.TextColor = color;
			_painter.ErrorColor = color;
			_appliedColor = color;
		}
	}

	private static Color ToDrawingColor(IBrush? brush)
	{
		return brush is ISolidColorBrush solid
			? solid.Color
			: Colors.Black;
	}
}

