using System.Collections.Generic;

using Avalonia;
using global::Avalonia.Media;

using CSharpMath.Rendering.FrontEnd;
using CSharpMathColor = System.Drawing.Color;
using MathPath = CSharpMath.Rendering.FrontEnd.Path;

namespace CodeWF.Avalonia.Markdown.MathRendering;

/// <summary>
/// CSharpMath 画布适配（Avalonia 12.1.3 兼容版）。
/// <para>
/// 官方 CSharpMath.Avalonia 12.0.0 的 <c>AvaloniaCanvas</c> / <c>AvaloniaPath</c> 依赖两条
/// 在 Avalonia 12.1.3 上已失效的行为，导致公式只剩分式横线、其余字形全部不可见：
/// </para>
/// <list type="number">
/// <item>先用 <c>StreamGeometryContext</c> 打开几何、再填充带填充色的图形时，图形与填充状态都写不进去；</item>
/// <item>经由 <c>StreamGeometry.Parse</c> 得到的几何在 Avalonia 12.1.3 的 Skia 后端上不渲染。</item>
/// </list>
/// <para>
/// 因此这里改为「累积路径指令 → 解析为几何 → 直接绘制」。数学字形是纯轮廓路径（不含文字），
/// 无需 <c>IGraphicsContext</c> 的字形接口，实现 <c>ICanvas</c> 即可。
/// </para>
/// </summary>
internal sealed class AvaloniaMathCanvas : ICanvas
{
	private readonly DrawingContext _drawingContext;
	private readonly Stack<List<DrawingContext.PushedState>> _states = new();
	private CSharpMathColor _defaultColor = CSharpMathColor.Black;
	private CSharpMathColor? _currentColor;
	private IBrush _currentBrush = Brushes.Black;

	public AvaloniaMathCanvas(DrawingContext drawingContext, Size size)
	{
		_drawingContext = drawingContext;
		Width = (float)size.Width;
		Height = (float)size.Height;
	}

	public float Width { get; }

	public float Height { get; }

	public CSharpMathColor DefaultColor
	{
		get => _defaultColor;
		set
		{
			_defaultColor = value;
			if (_currentColor is null)
			{
				_currentBrush = ToBrush(value);
			}
		}
	}

	public CSharpMathColor? CurrentColor
	{
		get => _currentColor;
		set
		{
			_currentColor = value;
			_currentBrush = ToBrush(value ?? _defaultColor);
		}
	}

	public PaintStyle CurrentStyle { get; set; }

	internal IBrush CurrentBrush => _currentBrush;

	public MathPath StartNewPath() => new MathFigurePath(this);

	public void DrawLine(float x1, float y1, float x2, float y2, float lineThickness) =>
		_drawingContext.DrawLine(new Pen(_currentBrush, lineThickness), new Point(x1, y1), new Point(x2, y2));

	public void StrokeRect(float left, float top, float width, float height) =>
		_drawingContext.DrawRectangle(new Pen(_currentBrush), new Rect(left, top, width, height));

	public void FillRect(float left, float top, float width, float height) =>
		_drawingContext.FillRectangle(_currentBrush, new Rect(left, top, width, height));

	public void Save() => _states.Push(new List<DrawingContext.PushedState>());

	public void Translate(float dx, float dy) =>
		PushState(_drawingContext.PushTransform(Matrix.CreateTranslation(dx, dy)));

	public void Scale(float sx, float sy) =>
		PushState(_drawingContext.PushTransform(Matrix.CreateScale(sx, sy)));

	public void Restore()
	{
		if (_states.Count == 0)
		{
			return;
		}

		var states = _states.Pop();
		for (var i = states.Count - 1; i >= 0; i--)
		{
			states[i].Dispose();
		}
	}

	internal void DrawPath(Geometry geometry, IBrush? fill, IPen? pen) =>
		_drawingContext.DrawGeometry(fill, pen, geometry);

	private void PushState(DrawingContext.PushedState state)
	{
		if (_states.Count == 0)
		{
			state.Dispose();
			return;
		}

		_states.Peek().Add(state);
	}

	private static IBrush ToBrush(CSharpMathColor color) =>
		new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
}
