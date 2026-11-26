using Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 自绘文本段的搜索高亮接管：查找画刷由调用方传入（普通命中/当前命中两种），
/// 段控件按区间绘制在文字下方。重复调用整体替换；空列表即清除。
/// 区间口径：段纯文本（与查找偏移同源）。
/// </summary>
public interface IMarkdownSearchHighlightVisual
{
	/// <param name="highlights">(段内起点, 长度, 画刷) 列表；当前命中项传当前画刷。</param>
	void ApplySearchHighlights(IReadOnlyList<(int Start, int Length, IBrush Brush)> highlights);
}
