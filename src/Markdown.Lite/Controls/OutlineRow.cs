using System.ComponentModel;
using Avalonia;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 大纲面板的单行展示模型：由 <see cref="MarkdownOutlinePanel.SetItems"/> 依据
/// <see cref="MarkdownOutlineItem"/>（含序号）生成，供模板绑定。
/// 折叠/当前章状态由宿主或面板写入，面板据此过滤与高亮。
/// </summary>
public sealed class OutlineRow : INotifyPropertyChanged
{
	public OutlineRow(MarkdownOutlineItem item, string number)
	{
		Item = item;
		Number = number;
		Level = Math.Clamp(item.Level, 1, 6);
		Title = item.Title;
		Line = item.Line;
		// 原型 .structure-item 用 padding-left 逐级缩进（(level-1)*12）。
		Indent = new Thickness(Math.Max(0, Level - 1) * 12, 0, 0, 0);
	}

	public MarkdownOutlineItem Item { get; }

	public int Level { get; }

	public string Title { get; }

	public int Line { get; }

	/// <summary>层级章节序号（1、1.1、1.1.1；文档不以 H1 开头时前级以 0 占位）。</summary>
	public string Number { get; }

	public bool IsTopLevel => Level == 1;

	/// <summary>是否有下级标题；没有时左右键折叠为空操作。</summary>
	public bool HasChildren { get; internal set; }

	public Thickness Indent { get; }

	private bool _isExpanded = true;

	/// <summary>折叠状态（仅影响可见性，不改数据）。宿主可在 SetItems 后按自身持久化盖章。</summary>
	public bool IsExpanded
	{
		get => _isExpanded;
		set
		{
			if (_isExpanded == value) return;
			_isExpanded = value;
			PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
		}
	}

	private bool _isCurrent;

	/// <summary>光标/视口所在章节高亮。</summary>
	public bool IsCurrent
	{
		get => _isCurrent;
		set
		{
			if (_isCurrent == value) return;
			_isCurrent = value;
			PropertyChanged?.Invoke(this, new(nameof(IsCurrent)));
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}
