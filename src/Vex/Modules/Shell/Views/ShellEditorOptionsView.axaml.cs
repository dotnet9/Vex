using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellEditorOptionsView : UserControl
{
    public ShellEditorOptionsView()
    {
        InitializeComponent();
    }

    /// <summary>字号面板：Tag 即像素值，套行内 font-size。</summary>
    public void OnSpanSizeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: { } tag } && DataContext is MainWindowViewModel { EditorActions: { } actions })
            actions.ApplySpanStyle("font-size", tag.ToString() ?? string.Empty);
    }

    /// <summary>颜色面板关闭时应用最终颜色（拖动频谱过程不逐帧改写选区）。</summary>
    public void OnSpanFlyoutClosed(object? sender, EventArgs e)
    {
        if (sender is Flyout { Content: ColorView { Tag: { } tag } view } && DataContext is MainWindowViewModel { EditorActions: { } actions })
        {
            var color = view.Color;
            actions.ApplySpanStyle(tag.ToString() ?? string.Empty, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
        }
    }
}
