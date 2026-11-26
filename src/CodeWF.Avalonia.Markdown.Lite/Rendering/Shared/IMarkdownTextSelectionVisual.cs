using Avalonia;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 自绘文本段的选区接管接口：块控件子树里的控件实现本接口后，选区控制器把该块
/// 的命中测试与选区可视化委托给它，不再走「投影到块内 SelectableTextBlock /
/// 公式控件」的默认路径。
/// 坐标口径：<paramref name="documentPoint"/> 与选区控制器的坐标根一致（滚动宿主
/// 可视树坐标系）；实现内部自行 TranslatePoint 到本地。
/// 偏移口径：块纯文本（<see cref="MarkdownSelectionBlock.PlainText"/>）偏移。
/// </summary>
public interface IMarkdownTextSelectionVisual
{
	/// <summary>块内文档坐标点 → 纯文本偏移（含边界夹取）。</summary>
	int HitTestOffset(Point documentPoint);

	/// <summary>绘制选区可视化：块内纯文本区间 [from, to)。重复调用按新区间刷新。</summary>
	void ApplySelection(int from, int to);

	/// <summary>清除选区可视化。</summary>
	void ClearSelection();
}
