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
        IndentMargin = new Thickness(Math.Max(0, level - 1) * 14, 0, 0, 0);
    }

    public int Level { get; }

    public string Title { get; }

    public int Line { get; }

    public Thickness IndentMargin { get; }

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

    // 大纲角标（对应原型 H1-H4 标签）：直接展示标题层级 H1-H6
    public string LevelText => "H" + Level;
}
