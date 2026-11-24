using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;

using CodeWF.Avalonia.Markdown.Mermaid;
using CodeWF.Avalonia.Markdown.Core.Blocks;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Shell.Views;

public partial class ShellEditorOptionsView : UserControl
{
    private Popup? _formatOverflowPopup;

    public ShellEditorOptionsView()
    {
        InitializeComponent();
        // Ursa ToolBarPanel 的多按钮溢出恢复有错位缺陷：宽约束恢复后驻留弹层的按钮
        // 不全回主面板（单按钮可回，多按钮恢复序错位）。重置式自愈：清 IsOverflowItem
        // 标记并重灌面板测量，Ursa 在当前宽约束下重新判定。门控=编辑栏可用宽 ≥848
        //（22 键整行下限+边距）——真窄栏（980 侧栏挤压）溢出合法，门控拦截不循环。
        // HealIfParked 内含同步 Measure——绝不能在布局过程中重入（headless 实测
        // 布局中重入测量直接 native Segfault）。三驱动全部改为队列化：排队一次，
        // Background 优先级（布局完成后的空闲点）执行；空闲态零布局经过零动作。
        FormattingToolbar.LayoutUpdated += (_, _) => QueueOverflowHeal();
        SizeChanged += (_, _) => QueueOverflowHeal();
        var overflowHealTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        overflowHealTimer.Tick += (_, _) => QueueOverflowHeal();
        overflowHealTimer.Start();
        FormattingToolbar.KeyDown += OnOverflowKeyDown;
        FormattingToolbar.AddHandler(Button.ClickEvent, OnOverflowActionClick);
        foreach (var button in new[] { SpanFontSize, SpanForeground, SpanBackground })
            button.Flyout!.Closed += (_, _) => { if (_formatOverflowPopup is { } popup) popup.IsOpen = false; };
        FormattingToolbar.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Popup>("FormatOverflowPopup") is not { } popup) return;
            _formatOverflowPopup = popup;
            if (e.NameScope.Find<ToggleButton>("button") is { } toggle)
                toggle.PropertyChanged += (_, change) =>
                {
                    if (change.Property == IsVisibleProperty && !toggle.IsVisible)
                        popup.SetCurrentValue(Popup.IsOpenProperty, false);
                };
            popup.Opened += (_, _) =>
            {
                if (TopLevel.GetTopLevel(this) is not { } window
                    || popup.Child?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll) return;
                var bottom = this.TranslatePoint(new Avalonia.Point(0, Bounds.Height), window)?.Y ?? 0;
                scroll.MaxHeight = Math.Max(32, window.ClientSize.Height - bottom - 30);
                if (scroll.Content is FormattingOverflowPanel panel)
                    panel.Children.FirstOrDefault(c => c is Button or ComboBox && c.IsVisible && c.IsEnabled)?.Focus();
            };
            popup.KeyDown += OnOverflowKeyDown;
        };
        // 图鉴卡片 SVG（库端正式管线渲染）：Flyout 打开或 TabItem 切换时对可见卡片赋 SvgSource。
        Loaded += (_, _) =>
        {
            _galleryThemeHost?.PropertyChanged -= OnGalleryThemeChanged;
            _galleryThemeHost = Application.Current;
            if (_galleryThemeHost is not null) _galleryThemeHost.PropertyChanged += OnGalleryThemeChanged;
            _ = PreloadGallerySourcesAsync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_galleryThemeHost is not null) _galleryThemeHost.PropertyChanged -= OnGalleryThemeChanged;
            _galleryThemeHost = null;
        };
        FormatMermaidGallery.Flyout!.Opened += (_, _) => ApplyGallerySources();
        FormatMermaidGallery.Flyout!.Closed += (_, _) => { /* 关闭不清理，下次打开直接复用缓存 */ };
        // 图鉴卡片点击即插入（原型 closePicker 语义）：Click 先冒泡、插入命令随后执行，
        // 推到 Dispatcher 下一轮收起弹层，保证「落到文件后」才收。
        if (FormatMermaidGallery.Flyout is Flyout { Content: Control galleryRoot })
            galleryRoot.AddHandler(Button.ClickEvent, OnGalleryCardClick);
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


    /// <summary>图鉴缩略图源缓存：SvgSource 在后台线程解析一次，跨 Flyout 打开复用（弹出零延迟的关键）。</summary>
    private static readonly Dictionary<string, (string CacheKey, Avalonia.Svg.Skia.SvgSource Source)> GallerySources = new(StringComparer.Ordinal);
    private Application? _galleryThemeHost;

    private static readonly string[] GalleryKeys =
    [
        "flowchart", "sequence", "state", "journey", "gantt", "timeline", "git", "kanban",
        "class", "er", "requirement", WorkbenchLayoutKeys.ViewMindMap, "block", "c4", "architecture", "packet",
        "pie", "quadrant", "xy", "sankey", "radar", "treemap", "venn", "treeview",
    ];

    /// <summary>后台线程预解析 24 张自包含 SVG（AssetLoader 读流 → SvgSource），启动后即开始。</summary>
    private async System.Threading.Tasks.Task PreloadGallerySourcesAsync()
    {
        // 走库端 Mermaid 正式渲染管线（渲染 + CSS 变量内联 + 象限/桑基修正 + LRU 缓存），
        // 与文档内图表同一条代码路径，视觉一致；后台线程执行，弹出时只做引用赋值。
        foreach (var key in GalleryKeys)
        {
            var current = GetGallerySource(key);
            if (current is { } ready && GallerySources.TryGetValue(key, out var cached) && cached.CacheKey == ready.CacheKey) continue;
            try
            {
                var source = MarkdownTemplates.GetDiagramSource(key);
                if (source is null) continue;
                var options = MermaidBlockRenderer.GetRenderOptions(source);
                var cacheKey = MermaidBlockRenderer.CreateCacheKey(source, options);
                var svgSource = await MermaidBlockRenderer.ResolveSvgSourceAsync(cacheKey, source, options);
                if (svgSource is not null && GetGallerySource(key) is { } latest && latest.CacheKey == cacheKey)
                    GallerySources[key] = (cacheKey, svgSource);
            }
            catch
            {
                // 单张渲染失败不阻塞其余；卡片保留文字标签。
            }
        }
        // 预热完成时若 Flyout 已打开（首开点得快），补一次赋值——Opened 只在打开瞬间触发。
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (FormatMermaidGallery.Flyout?.IsOpen == true) ApplyGallerySources();
        });
    }

    private void OnGalleryThemeChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property.Name != "ActualThemeVariant") return;
        _ = PreloadGallerySourcesAsync();
        if (FormatMermaidGallery.Flyout?.IsOpen == true)
            Avalonia.Threading.Dispatcher.UIThread.Post(ApplyGallerySources);
    }

    private static (string Source, string CacheKey)? GetGallerySource(string key)
    {
        var source = MarkdownTemplates.GetDiagramSource(key);
        if (source is null) return null;
        var options = MermaidBlockRenderer.GetRenderOptions(source);
        return (source, MermaidBlockRenderer.CreateCacheKey(source, options));
    }

    /// <summary>把缓存的 SvgSource 赋给卡片 Image；尚未就绪的由 Preload 完成后下次打开生效。</summary>
    private void ApplyGallerySources()
    {
        if (FormatMermaidGallery.Flyout is not Flyout { Content: Visual root }) return;
        foreach (var card in root.GetVisualDescendants().OfType<Button>())
        {
            if (card.CommandParameter is not string key) continue;
            var image = card.GetVisualDescendants().OfType<Image>().FirstOrDefault();
            if (image is null) continue;
            var current = GetGallerySource(key);
            image.Source = current is { } latest
                && GallerySources.TryGetValue(key, out var cached)
                && cached.CacheKey == latest.CacheKey
                ? new Avalonia.Svg.Skia.SvgImage { Source = cached.Source }
                : null;
        }
    }

    /// <summary>TabItem 切换：TabControl 非选中组内容不驻留可视树，新材料化即补挂缓存源。
    /// SelectedContent 换树发生在 SelectionChanged 之后，推到下一轮再赋；XAML 装载期选择模型
    /// 初始化也会触发（彼时字段未就绪、弹层未开），按弹层开关过滤。</summary>
    private void OnGalleryTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FormatMermaidGallery?.Flyout?.IsOpen != true) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(ApplyGallerySources);
    }

    /// <summary>图鉴卡片点击：插入命令执行完毕后（Dispatcher 下一轮）收起弹层。</summary>
    private void OnGalleryCardClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button card && card.Classes.Contains("dg-card"))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => FormatMermaidGallery.Flyout?.Hide());
    }

    /// <summary>
    /// 溢出卡死自愈：溢出态且有格式按钮被驻留在关闭的弹层（Ursa 多按钮恢复序错位）时，
    /// 清全部 IsOverflowItem 标记让 Ursa 在当前宽约束下重新判定，再强灌面板测量。
    /// 门控：可用宽 ≥848（22 键整行下限+边距）——真窄栏溢出合法，强灌只会立刻回弹造成布局环。
    /// </summary>
    private bool _overflowHealQueued;

    private void QueueOverflowHeal()
    {
        if (_overflowHealQueued || !FormattingToolbar.Classes.Contains(":overflow")) return;
        _overflowHealQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _overflowHealQueued = false;
            HealIfParked();
        }, DispatcherPriority.Background);
    }

    private void HealIfParked()
    {
        if (!FormattingToolbar.Classes.Contains(":overflow")) return;
        if (Bounds.Width < 848) return;
        var parked = FormattingToolbar.GetVisualDescendants()
            .OfType<Button>()
            .Any(button => button.Classes.Contains("wb-format") && button.IsVisible && !button.IsAttachedToVisualTree());
        if (!parked) return;

        // Ursa 的 IsOverflowItem 标记是 internal：无法直接重置——强灌面板测量让 Ursa
        // 重算 fit；多按钮深驻留时单次救回一个，由 LayoutUpdated 驱动迭代收敛。
        foreach (var panel in FormattingToolbar.GetVisualDescendants()
                     .Where(control => control.GetType().Name == "ToolBarPanel")
                     .OfType<Layoutable>())
        {
            panel.InvalidateMeasure();
            panel.Measure(new Size(Bounds.Width, double.PositiveInfinity));
        }
        FormattingToolbar.InvalidateMeasure();
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
