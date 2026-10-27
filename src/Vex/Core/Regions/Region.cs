using Avalonia.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Vex.Core.Regions;

/// <summary>区域实现</summary>
public class Region : IRegion
{
    private Control? _activeView;

    public string Name { get; }

    public Control? ActiveView
    {
        get => _activeView;
        set
        {
            if (_activeView != value)
            {
                _activeView = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveView)));
            }
        }
    }

    public IList<Control> Views { get; } = new List<Control>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public Region(string name)
    {
        Name = name;
    }

    public void AddView(Control view)
    {
        if (!Views.Contains(view))
        {
            Views.Add(view);

            // 第一个视图自动激活
            if (ActiveView == null)
            {
                ActiveView = view;
            }
        }
    }

    public void RemoveView(Control view)
    {
        Views.Remove(view);

        // 如果移除的是激活视图，激活第一个可用视图
        if (ActiveView == view)
        {
            ActiveView = Views.FirstOrDefault();
        }
    }
}
