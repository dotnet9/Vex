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

    /// <summary>颜色面板打开时预填当前选区的样式值（无样式则保持默认色）。</summary>
    public void OnSpanFlyoutOpened(object? sender, EventArgs e)
    {
        if (sender is Flyout { Content: ColorView { Tag: { } tag } view })
        {
            var value = CodeWF.Toolkit.EventBus.EventBus.Default
                .Query(new Vex.Core.Messaging.EditorSpanStyleQuery(tag.ToString() ?? string.Empty)).Value;
            if (Avalonia.Media.Color.TryParse(value, out var color))
                view.Color = color;
        }
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
