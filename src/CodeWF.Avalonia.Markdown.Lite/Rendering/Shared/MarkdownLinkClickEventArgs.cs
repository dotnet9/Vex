namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 链接点击事件参数：宿主可在确认对话框后置 <see cref="Cancel"/> 阻止打开。
/// </summary>
public sealed class MarkdownLinkClickEventArgs : EventArgs
{
	/// <summary>链接目标地址。</summary>
	public required string Url { get; init; }

	/// <summary>链接显示文字（提取自渲染文本，可能为空）。</summary>
	public string? LinkText { get; init; }

	/// <summary>
	/// 链接文字疑似伪装（文字像 URL 但与实际目标的主机不一致，如
	/// <c>[https://github.com](https://evil.example)</c>），宿主应先弹确认框。
	/// </summary>
	public bool RequiresConfirmation { get; init; }

	/// <summary>宿主置 true 阻止打开（确认对话框被拒绝时）。</summary>
	public bool Cancel { get; set; }
}
