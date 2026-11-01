using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellEditorOptionsView : UserControl
{
    private Popup? _formatOverflowPopup;

    public ShellEditorOptionsView()
    {
        InitializeComponent();
        FormattingToolbar.KeyDown += OnOverflowKeyDown;
        FormattingToolbar.AddHandler(Button.ClickEvent, OnOverflowActionClick);
        foreach (var button in new[] { SpanFontSize, SpanForeground, SpanBackground })
            button.Flyout!.Closed += (_, _) => { if (_formatOverflowPopup is { } popup) popup.IsOpen = false; };
        FormattingToolbar.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Popup>("FormatOverflowPopup") is not { } popup) return;
            _formatOverflowPopup = popup;
            popup.Opened += (_, _) =>
            {
                if (TopLevel.GetTopLevel(this) is not { } window
                    || popup.Child?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll) return;
                var bottom = this.TranslatePoint(new Avalonia.Point(0, Bounds.Height), window)?.Y ?? 0;
                scroll.MaxHeight = Math.Max(32, window.ClientSize.Height - bottom - 30);
                if (scroll.Content is FormattingOverflowPanel panel)
                    panel.Children.OfType<Button>().FirstOrDefault(b => b.IsVisible)?.Focus();
            };
            popup.KeyDown += OnOverflowKeyDown;
        };
    }

    private void OnOverflowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _formatOverflowPopup?.IsOpen != true) return;
        _formatOverflowPopup.IsOpen = false;
        FormattingToolbar.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(b => b.Name == "button")?.Focus();
        e.Handled = true;
    }

    private void OnOverflowActionClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { Flyout: null } button && button.Classes.Contains("wb-format")
            && _formatOverflowPopup is { IsOpen: true } popup)
            popup.IsOpen = false;
    }

    /// <summary>字号面板：Tag 即像素值，套行内 font-size。</summary>
    public void OnSpanSizeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: { } tag } && DataContext is MainWindowViewModel { EditorActions: { } actions })
            actions.ApplySpanStyle("font-size", tag.ToString() ?? string.Empty);
    }

    /// <summary>字号面板打开时高亮当前选区的字号（无样式则不高亮）。</summary>
    public void OnSizeFlyoutOpened(object? sender, EventArgs e)
    {
        var value = CodeWF.Toolkit.EventBus.EventBus.Default
            .Query(new Vex.Core.Messaging.EditorSpanStyleQuery("font-size")).Value;
        if (SpanSizePanel is not null)
        {
            foreach (var child in SpanSizePanel.Children)
            {
                if (child is Button { Tag: { } tag } button)
                {
                    var selected = tag.ToString() == value?.Replace("px", string.Empty, StringComparison.Ordinal);
                    button.BorderThickness = selected ? new Avalonia.Thickness(2) : new Avalonia.Thickness(0);
                    button.BorderBrush = selected
                        ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.DodgerBlue)
                        : null;
                }
            }
        }
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
