using Avalonia.Automation.Peers;
using Avalonia.Controls;

using CodeWF.Avalonia.Markdown.Controls;

namespace CodeWF.Avalonia.Markdown.Automation;

/// <summary>
/// Viewer 根自动化 peer：向读屏器暴露"文档"角色与纯文本名称。
///
/// 能力边界（如实声明）：Avalonia 没有公开的 UIA TextPattern，读屏可以拿到
/// 文档名称与子控件的名称/操作（文本块名称、复制按钮等），但无法做文本级
/// 光标浏览与区间选择。
/// </summary>
public sealed class MarkdownViewerAutomationPeer(MarkdownViewer owner) : ControlAutomationPeer(owner)
{
	/// <summary>名称截断上限：完整纯文本可能极大，读屏器不需要全部。</summary>
	private const int MaxNameLength = 2000;

	protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

	protected override string GetClassNameCore() => nameof(MarkdownViewer);

	protected override string? GetNameCore()
	{
		var plainText = owner.CurrentModel.PlainText;
		if (string.IsNullOrEmpty(plainText))
		{
			plainText = owner.Markdown ?? string.Empty;
		}

		plainText = plainText.Trim();
		if (plainText.Length <= MaxNameLength)
		{
			return plainText;
		}

		return plainText[..MaxNameLength] + "…";
	}
}
