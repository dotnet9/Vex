using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodeWF.Avalonia.MindView;

/// <summary>
/// 脑图节点模型。控件、Markdown 编解码和小图都围绕这棵树工作。
/// </summary>
public class MindMapNode : INotifyPropertyChanged
{
    private string _title;
    private string _note = string.Empty;
    private string _accentColor = string.Empty;
    private double _x;
    private double _y;

    public MindMapNode(string title, params MindMapNode[] children)
    {
        _title = title;
        Children = new ObservableCollection<MindMapNode>(children);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 节点标题；根节点为空时由编辑器显示中心主题占位。
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// 节点备注，会在大纲和脑图中同步显示。
    /// </summary>
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    /// <summary>
    /// 节点强调色。留空时编辑器会按默认色板补齐。
    /// </summary>
    public string AccentColor
    {
        get => _accentColor;
        set => SetProperty(ref _accentColor, value);
    }

    /// <summary>
    /// 节点在脑图画布中的 X 坐标。
    /// </summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>
    /// 节点在脑图画布中的 Y 坐标。
    /// </summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>
    /// 子节点集合，集合变化会触发脑图重新绘制。
    /// </summary>
    public ObservableCollection<MindMapNode> Children { get; }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
