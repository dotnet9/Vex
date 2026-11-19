using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Styling;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 阅读视图的媒体放大对话框（纯查看）：纯阅读（MarkdownViewer.IsDiagramEditingEnabled=false）
/// 下点击块级图片或 Mermaid 图表弹出，挂在宿主 TopLevel 的 OverlayLayer 上（不弹独立窗口）。
/// 布局 = 全屏半透明遮罩 + 居中内容卡（约 80% 可用区域）；点遮罩或按 Esc 关闭。
/// 与行内图片沿用的 <see cref="MarkdownImagePreviewWindow"/>（带缩放/旋转/另存工具条）分开：
/// 阅读放大是轻量只读操作，不提供编辑与导出入口。
/// 全部控件代码构造（无反射创建），AOT 友好；配色经 BindTheme 跟随 Viewer 主题属性。
/// </summary>
internal sealed class MarkdownMediaZoomDialog : Panel
{
    /// <summary>内容卡占 TopLevel 可用区域的比例（约定 ~80%）。</summary>
    private const double CardAreaRatio = 0.8;

    /// <summary>遮罩底色：统一半透明黑，明暗主题下都能压暗背景衬托内容卡。</summary>
    private static readonly IBrush BackdropBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x00, 0x00));

    private readonly Border _backdrop;
    private readonly Border _card;
    private readonly IDisposable? _contentCleanup;
    private readonly List<IDisposable> _themeBindings = [];
    private TopLevel? _topLevel;
    private IDisposable? _overlayBoundsSubscription;
    private bool _closed;

    /// <summary>对话框被关闭（含宿主窗口关闭导致的脱离）后触发；调用方据此清空入口守卫。</summary>
    internal event System.Action? Dismissed;

    private MarkdownMediaZoomDialog(Control content, IDisposable? contentCleanup)
    {
        _contentCleanup = contentCleanup;
        Focusable = true;
        Classes.Add(MarkdownStyleKeys.MediaZoomDialog);

        // 遮罩：点击即关闭（内容卡是兄弟节点，点在卡上不会落到遮罩）。
        _backdrop = new Border { Background = BackdropBrush };
        _backdrop.Classes.Add(MarkdownStyleKeys.MediaZoomDialogBackdrop);
        _backdrop.PointerReleased += OnBackdropPointerReleased;

        // 内容卡：居中 + 主题化描边/底色，尺寸由 UpdateCardSize 按 TopLevel 可用区域给上限。
        _card = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            ClipToBounds = true,
            Child = content
        };
        _card.Classes.Add(MarkdownStyleKeys.MediaZoomDialogContent);

        Children.Add(_backdrop);
        Children.Add(_card);
        KeyDown += OnDialogKeyDown;
        DetachedFromVisualTree += (_, _) => Close();
    }

    /// <summary>
    /// 弹出放大对话框：topLevel/OverlayLayer 缺失（如控件尚未挂树）时静默不弹；
    /// contentCleanup 为内容自带的非托管资源（动画帧流等），随对话框关闭释放。
    /// <paramref name="detachWatch"/> 为来源控件（图片/图表视图）：它脱离视觉树
    /// （切文档、切布局导致的重渲染）时对话框自动关闭，避免遮罩滞留挡住新内容。
    /// </summary>
    public static MarkdownMediaZoomDialog? Show(TopLevel? topLevel, Control content,
        MarkdownViewer? themeSource = null, IDisposable? contentCleanup = null,
        Control? detachWatch = null)
    {
        var overlay = topLevel is null ? null : OverlayLayer.GetOverlayLayer(topLevel);
        if (overlay is null)
        {
            contentCleanup?.Dispose();
            return null;
        }

        var dialog = new MarkdownMediaZoomDialog(content, contentCleanup);
        dialog.BindThemeBrushes(themeSource);
        overlay.Children.Add(dialog);
        dialog.WatchTopLevel(topLevel, overlay);
        if (detachWatch is not null)
        {
            void OnSourceDetached(object? sender, VisualTreeAttachmentEventArgs args) => dialog.Close();
            detachWatch.DetachedFromVisualTree += OnSourceDetached;
            dialog.Dismissed += () => detachWatch.DetachedFromVisualTree -= OnSourceDetached;
        }

        // 焦点给对话框：Esc 的第一通道；焦点被内容抢走时由 TopLevel 隧道兜底。
        dialog.Focus();
        return dialog;
    }

    /// <summary>把卡片描边/底色绑定到宿主 Viewer 的主题属性，明暗切换实时跟随。</summary>
    private void BindThemeBrushes(MarkdownViewer? themeSource)
    {
        if (themeSource is not { } viewer || viewer is not IMarkdownRenderContext context)
        {
            // Viewer 缺位（理论上不会发生）：回落到主题令牌一次性取值。
            _card.Background = ResolveThemeBrush(themeSource, MarkdownStyleKeys.QuoteBackgroundBrushResource, lightFallback: 0xFFFFFFFF, darkFallback: 0xFF1E1E26);
            _card.BorderBrush = ResolveThemeBrush(themeSource, MarkdownStyleKeys.BorderBrushResource, lightFallback: 0xFFDDDDDD, darkFallback: 0xFF3A3A42);
            return;
        }

        // Viewer.Background 常为空（宿主的正文卡片底色在页面层，不在 Viewer 上）——
        // 直接绑定会把卡片绑成透明，放大图表背后就是遮罩。有值才绑定；无值走令牌兜底链。
        if (viewer.Background is not null)
        {
            _themeBindings.Add(context.BindTheme(_card, Border.BackgroundProperty, MarkdownViewer.BackgroundProperty));
        }
        else
        {
            _card.Background = ResolveThemeBrush(themeSource, MarkdownStyleKeys.QuoteBackgroundBrushResource, lightFallback: 0xFFFFFFFF, darkFallback: 0xFF1E1E26);
        }

        if (viewer.BorderLineBrush is not null)
        {
            _themeBindings.Add(context.BindTheme(_card, Border.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty));
        }
        else
        {
            _card.BorderBrush = ResolveThemeBrush(themeSource, MarkdownStyleKeys.BorderBrushResource, lightFallback: 0xFFDDDDDD, darkFallback: 0xFF3A3A42);
        }
    }

    /// <summary>
    /// 兜底取刷：优先 Viewer 范围的主题资源（随宿主主题字典走），退到应用级令牌，
    /// 仍没有则按明暗变体给保守常量——保证放大卡片永不透明。
    /// </summary>
    private IBrush ResolveThemeBrush(MarkdownViewer? themeSource, string resourceKey, uint lightFallback, uint darkFallback)
    {
        if (themeSource is not null && themeSource.TryFindResource(resourceKey, out var scoped) && scoped is IBrush scopedBrush)
        {
            return scopedBrush;
        }

        if (Application.Current?.TryGetResource(resourceKey, ActualThemeVariant, out var appLevel) == true && appLevel is IBrush appBrush)
        {
            return appBrush;
        }

        var argb = ActualThemeVariant == ThemeVariant.Light ? lightFallback : darkFallback;
        return new SolidColorBrush(argb);
    }

    private void WatchTopLevel(TopLevel? topLevel, OverlayLayer overlay)
    {
        _topLevel = topLevel;
        // OverlayLayer 是 Canvas 系：子项不会被自动拉伸，手动对齐其 Bounds 铺满整层；
        // 内容卡上限也按整层（即 TopLevel 可用区域）尺寸折算。
        _overlayBoundsSubscription = overlay.GetObservable(BoundsProperty).Subscribe(new OverlayBoundsObserver(this));
        // Esc 兜底通道：焦点在内容里或窗口标题栏时也能关闭。
        topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnBackdropPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (args.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        args.Handled = true;
        Close();
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            args.Handled = true;
            Close();
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            args.Handled = true;
            Close();
        }
    }

    /// <summary>内容卡上限 = TopLevel 可用区域的 ~80%（ClientSize 为 DIP，含窗口装饰扣除）。</summary>
    private void UpdateCardSize(Size clientSize)
    {
        _card.MaxWidth = Math.Max(240, clientSize.Width * CardAreaRatio);
        _card.MaxHeight = Math.Max(180, clientSize.Height * CardAreaRatio);
    }

    /// <summary>关闭：摘掉监听与主题绑定、释放内容资源并脱离 OverlayLayer；可重入。</summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        _overlayBoundsSubscription?.Dispose();
        _overlayBoundsSubscription = null;
        foreach (var binding in _themeBindings)
        {
            binding.Dispose();
        }

        _themeBindings.Clear();
        _contentCleanup?.Dispose();
        ((Panel?)Parent)?.Children.Remove(this);
        Dismissed?.Invoke();
    }

    /// <summary>把 OverlayLayer 尺寸变化折算成对话框铺满与内容卡上限更新。</summary>
    private sealed class OverlayBoundsObserver(MarkdownMediaZoomDialog owner) : IObserver<Rect>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(Rect value)
        {
            owner.Width = value.Width;
            owner.Height = value.Height;
            owner.UpdateCardSize(value.Size);
            owner.InvalidateMeasure();
        }
    }
}
