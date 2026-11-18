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
        // 图鉴卡片 SVG（原型导出的自包含资源）：Loaded 即后台线程预解析进缓存，
        // 弹出时只做引用赋值（零解析零光栅化零延迟）；Opened 做分组 tab 接线与默认组过滤。
        Loaded += (_, _) => _ = PreloadGallerySourcesAsync();
        FormatMermaidGallery.Flyout!.Opened += (_, _) =>
        {
            if (FormatMermaidGallery.Flyout is Flyout { Content: Visual root })
            {
                WireGalleryGroupTabs(root);
                ApplyGalleryGroupFilter(0);
                ApplyGallerySources();
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

    /// <summary>图鉴缩略图源缓存：SvgSource 在后台线程解析一次，跨 Flyout 打开复用（弹出零延迟的关键）。</summary>
    private static readonly Dictionary<string, Avalonia.Svg.Skia.SvgSource> GallerySources = new(StringComparer.Ordinal);

    private static readonly string[] GalleryKeys =
    [
        "flowchart", "sequence", "state", "journey", "gantt", "timeline", "git", "kanban",
        "class", "er", "requirement", "mindmap", "block", "c4", "architecture", "packet",
        "pie", "quadrant", "xy", "sankey", "radar", "treemap", "venn", "treeview",
    ];

    /// <summary>后台线程预解析 24 张自包含 SVG（AssetLoader 读流 → SvgSource），启动后即开始。</summary>
    private async System.Threading.Tasks.Task PreloadGallerySourcesAsync()
    {
        foreach (var key in GalleryKeys)
        {
            if (GallerySources.ContainsKey(key)) continue;
            try
            {
                var uri = new Uri($"avares://Vex/Assets/MermaidGallery/{key}.svg");
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                    GallerySources[key] = Avalonia.Svg.Skia.SvgSource.LoadFromStream(stream);
                });
            }
            catch
            {
                // 单张资源缺失不阻塞其余；卡片保留文字标签。
            }
        }
        // 预热完成时若 Flyout 已打开（首开点得快），补一次赋值——Opened 只在打开瞬间触发。
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (FormatMermaidGallery.Flyout?.IsOpen == true) ApplyGallerySources();
        });
    }

    /// <summary>把缓存的 SvgSource 赋给卡片 Image；尚未就绪的由 Preload 完成后下次打开生效。</summary>
    private void ApplyGallerySources()
    {
        if (FormatMermaidGallery.Flyout is not Flyout { Content: Visual root }) return;
        foreach (var card in root.GetVisualDescendants()
                     .OfType<Button>()
                     .Where(button => button.CommandParameter is string && GallerySources.TryGetValue((string)button.CommandParameter, out _)))
        {
            var image = card.GetVisualDescendants().OfType<Image>().FirstOrDefault();
            if (image is not null && GallerySources.TryGetValue((string)card.CommandParameter!, out var source))
                image.Source = new Avalonia.Svg.Skia.SvgImage { Source = source };
        }
    }

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
