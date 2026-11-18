using global::Avalonia.Media;

using CSharpMath.Rendering.FrontEnd;
using CSharpMathColor = System.Drawing.Color;
using MathPath = CSharpMath.Rendering.FrontEnd.Path;

namespace CodeWF.Avalonia.Markdown.MathRendering;

/// <summary>
/// CSharpMath 路径适配：把「移动 / 直线 / 二次曲线 / 三次曲线 / 闭合」指令累积为
/// <see cref="PathFigure"/> 与对应的 <see cref="PathSegment"/>，在 <see cref="Dispose"/> 时整体绘制。
/// <para>
/// 官方实现边写边用 <c>StreamGeometryContext</c>（先 <c>Open</c> 再 <c>BeginFigure</c>），
/// 在 Avalonia 12.1.3 上填充图形与填充状态都写不进去，导致数学字形只剩分数线可见；
/// 本实现改为直接组装图形与线段对象，绕开该缺陷。
/// </para>
/// </summary>
internal sealed class MathFigurePath : MathPath
{
	private const double StrokeThickness = 1d;

	private readonly AvaloniaMathCanvas _canvas;
	private readonly PathFigure _figure = new();
	private readonly PathSegments _segments = new();
	private bool _hasFigure;
	private bool _disposed;

	public MathFigurePath(AvaloniaMathCanvas canvas)
	{
		_canvas = canvas;
	}

	public override CSharpMathColor? Foreground { get; set; }

	public override void MoveTo(float x0, float y0)
	{
		_hasFigure = true;
		_figure.StartPoint = new global::Avalonia.Point(x0, y0);
	}

	public override void LineTo(float x1, float y1)
	{
		EnsureFigure(x1, y1);
		_segments.Add(new LineSegment { Point = new global::Avalonia.Point(x1, y1) });
	}

	public override void Curve3(float x1, float y1, float x2, float y2)
	{
		EnsureFigure(x1, y1);
		_segments.Add(new QuadraticBezierSegment
		{
			Point1 = new global::Avalonia.Point(x1, y1),
			Point2 = new global::Avalonia.Point(x2, y2)
		});
	}

	public override void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
	{
		EnsureFigure(x1, y1);
		_segments.Add(new BezierSegment
		{
			Point1 = new global::Avalonia.Point(x1, y1),
			Point2 = new global::Avalonia.Point(x2, y2),
			Point3 = new global::Avalonia.Point(x3, y3)
		});
	}

	public override void CloseContour() => _figure.IsClosed = true;

	public override void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		if (!_hasFigure)
		{
			return;
		}

		_figure.Segments = _segments;
		_figure.IsFilled = _canvas.CurrentStyle == PaintStyle.Fill;
		var geometry = new PathGeometry { Figures = new PathFigures { _figure } };

		var brush = Foreground is { } foreground
			? new SolidColorBrush(Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B))
			: _canvas.CurrentBrush;
		if (_canvas.CurrentStyle == PaintStyle.Fill)
		{
			_canvas.DrawPath(geometry, brush, null);
		}
		else
		{
			_canvas.DrawPath(geometry, null, new Pen(brush, StrokeThickness));
		}
	}

	private void EnsureFigure(float x, float y)
	{
		if (!_hasFigure)
		{
			MoveTo(x, y);
		}
	}
}
