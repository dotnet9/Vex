using System.ComponentModel;
using Avalonia;

namespace Vex.Core.Models;

public sealed class OutlineItem : INotifyPropertyChanged
{
    public OutlineItem(int level, string title, int line)
    {
        Level = level;
        Title = title;
        Line = line;
        // 原型 .structure-item 用 padding-left 逐级缩进（workbench.js 内联 (level-1)*12），
        // 其余三边固定 6（左 0，让位给缩进本身）。
        RowPadding = new Thickness(Math.Max(0, level - 1) * 12, 6, 6, 6);
    }

    public int Level { get; }

    public string Title { get; }

    public int Line { get; }

    public Thickness RowPadding { get; }

    /// <summary>原型只对 H1 换档位：角标立起来、字重加粗、间距 7→8。</summary>
    public bool IsTopLevel => Level == 1;
    public string ExpansionKey { get; internal set; } = string.Empty;
    public bool HasChildren { get; internal set; }
    private bool _isExpanded = true;
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

    // 当前光标所在章节（由大纲侧栏联动更新）
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value)
            {
                return;
            }

            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Number { get; internal set; } = string.Empty;
}
