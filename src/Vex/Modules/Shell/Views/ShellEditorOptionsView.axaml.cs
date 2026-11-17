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

    private async System.Threading.Tasks.Task FillGalleryThumbnailsAsync()
    {
        if (_thumbsFilled) return;
        _thumbsFilled = true;
        var thumbs = FormatMermaidGallery.GetVisualDescendants()
            .OfType<Image>()
            .Where(image => Helpers.GalleryThumb.GetThumbKey(image) is { } key && key.Length > 0)
            .ToDictionary(image => Helpers.GalleryThumb.GetThumbKey(image)!, image => image);
        if (thumbs.Count == 0) return;
        await System.Threading.Tasks.Task.Run(async () =>
        {
            foreach (var (key, image) in thumbs)
            {
                var source = MarkdownTemplates.Get(key);
                if (source is null) continue;
                try
                {
                    var svg = Mermaider.MermaidRenderer.RenderSvg(source, new Mermaider.Models.RenderOptions
                    {
                        Bg = "#FFFFFF",
                        Fg = "#27272A",
                        Accent = "#4338CA",
                        Transparent = true,
                        Style = Mermaider.Models.DiagramStyle.Quiet,
                    });
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
