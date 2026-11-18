using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;

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
        // 图鉴卡片是随包静态 PNG（XAML 直接绑定，弹出零延迟）；Opened 只做分组 tab 接线与默认组过滤。
        FormatMermaidGallery.Flyout!.Opened += (_, _) =>
        {
            if (FormatMermaidGallery.Flyout is Flyout { Content: Visual root })
            {
                WireGalleryGroupTabs(root);
                ApplyGalleryGroupFilter(0);
            }
        };
        // 段落结构档位：选择即插入并复位到「正文」（原型 band-heading select 语义）。
        // DataContext 在构造后才附着，接线挂到 Loaded。
        Loaded += (_, _) =>
        {
            if (FormatHeading is not ComboBox combo || DataContext is not MainWindowViewModel vm) return;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is Avalonia.Controls.ComboBoxItem { Tag: Vex.Core.Messaging.EditorActionKind kind })
                {
                    vm.EditorActions.InsertAction(kind);
                    combo.SelectedIndex = 0;
                }
            };
        };
    }

    private bool _galleryTabsWired;

    /// <summary>
    /// 图鉴分组切换（Flyout 内 RadioButton tab）：Avalonia 的 RadioButton 没有 WPF 的 Checked 事件，
    /// 在首开时挂 IsChecked 变化；只显示选中分组的卡片（Tag=组号）。
    /// </summary>
    private void WireGalleryGroupTabs(Visual root)
    {
        if (_galleryTabsWired) return;
        _galleryTabsWired = true;
        foreach (var tab in root.GetVisualDescendants()
                     .OfType<RadioButton>()
                     .Where(radio => radio.GroupName == "MermaidGallery"))
        {
            tab.PropertyChanged += (_, e) =>
            {
                if (e.Property == RadioButton.IsCheckedProperty && tab.IsChecked == true
                    && int.TryParse(tab.Tag?.ToString(), out var group))
                    ApplyGalleryGroupFilter(group);
            };
        }
    }

    private void ApplyGalleryGroupFilter(int group)
    {
        if (FormatMermaidGallery.Flyout is not Flyout { Content: Visual root }) return;
        foreach (var card in root.GetVisualDescendants()
                     .OfType<Button>()
                     .Where(button => int.TryParse(button.Tag?.ToString(), out var cardGroup) && cardGroup == group))
        {
            card.IsVisible = true;
        }
        foreach (var card in root.GetVisualDescendants()
                     .OfType<Button>()
                     .Where(button => int.TryParse(button.Tag?.ToString(), out var cardGroup) && cardGroup != group))
        {
            card.IsVisible = false;
        }
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

}
