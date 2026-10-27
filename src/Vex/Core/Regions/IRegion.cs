using Avalonia.Controls;
using System.Collections.Generic;
using System.ComponentModel;

namespace Vex.Core.Regions;

/// <summary>区域接口</summary>
public interface IRegion : INotifyPropertyChanged
{
    /// <summary>区域名称</summary>
    string Name { get; }

    /// <summary>当前激活的视图</summary>
    Control? ActiveView { get; set; }

    /// <summary>区域中的所有视图</summary>
    IList<Control> Views { get; }

    /// <summary>添加视图到区域</summary>
    void AddView(Control view);

    /// <summary>从区域移除视图</summary>
    void RemoveView(Control view);
}
