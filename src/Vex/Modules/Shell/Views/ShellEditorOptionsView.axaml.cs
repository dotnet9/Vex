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
        // Mermaid 图鉴缩略图：飞出层打开时按卡片键异步渲染（原型 dg-card 带缩略图的语义）。
        // 渲染走 Mermaider 同步管线在后台线程执行，首开一次性填充 24 张（RenderSvg 无内置缓存，
        // _thumbsFilled 保证只渲染一轮；缩略图固定亮色底，不随明暗主题重渲）。
        FormatMermaidGallery.Flyout!.Opened += async (_, _) => await FillGalleryThumbnailsAsync();
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

    private bool _thumbsFilled;
    private bool _galleryTabsWired;

    /// <summary>
    /// 图鉴分组切换（Flyout 内 RadioButton tab）：Avalonia 的 RadioButton 没有 WPF 的 Checked 事件，
    /// 在首开填充时挂 IsChecked 变化；只显示选中分组的卡片（Tag=组号），缩略图首开全量渲染避免切换空白。
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

    private async System.Threading.Tasks.Task FillGalleryThumbnailsAsync()
    {
        if (_thumbsFilled) return;
        _thumbsFilled = true;
        // Flyout 内容挂在 Popup（OverlayLayer）而非按钮视觉树里，必须从 Flyout.Content 出发找卡片 Image。
        if (FormatMermaidGallery.Flyout is not Flyout flyout || flyout.Content is not Visual root) return;
        WireGalleryGroupTabs(root);
        ApplyGalleryGroupFilter(0);
        var thumbs = root.GetVisualDescendants()
            .OfType<Image>()
            .Where(image => Helpers.GalleryThumb.GetThumbKey(image) is { } key && key.Length > 0)
            .GroupBy(image => Helpers.GalleryThumb.GetThumbKey(image)!)
            .ToDictionary(group => group.Key, group => group.First());
        if (thumbs.Count == 0) return;
        await System.Threading.Tasks.Task.Run(async () =>
        {
            foreach (var (key, image) in thumbs)
            {
                var source = MarkdownTemplates.GetDiagramSource(key);
                if (source is null) continue;
                try
                {
                    var svg = Mermaider.MermaidRenderer.RenderSvg(source, new Mermaider.Models.RenderOptions
                    {
                        Bg = "#FFFFFF",
                        Fg = "#302C3C",
                        Accent = "#8B7CF6",
                        Transparent = true,
                        // 系列色必须显式给（象限/饼/桑基等图型缺省落黑块），与运行时自检同参。
                        DataPalette = ["#8B7CF6", "#D9AD53", "#70A98B", "#C583B5", "#62A9C2"],
                    });
                    // Mermaider 的 SVG 依赖宿主 CSS 变量（var(--fg) 等）与 color-mix，SvgImage 不解析会落黑块；
                    // 缩略图自包含化：变量与混合全部内联为字面量色值。
                    svg = Helpers.GallerySvgInliner.Inline(svg, "#302C3C", "#FFFFFF", "#8B7CF6");
                    using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
                    var svgSource = Avalonia.Svg.Skia.SvgSource.LoadFromStream(stream);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => image.Source = new Avalonia.Svg.Skia.SvgImage { Source = svgSource });
                }
                catch
                {
                    // 单张缩略图渲染失败不阻塞其余卡片；卡片保留文字标签。
                }
            }
        });
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
