using AnimatedImage.Avalonia;
using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Metadata;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using CodeWF.Toolkit.Lang;
using SkiaSharp;

using CodeWF.Avalonia.Markdown.Mermaid;
using CodeWF.Avalonia.Markdown.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>正文图片的尺寸策略；默认保留原有自然尺寸与最大尺寸约束。</summary>
public enum MarkdownImageSizing
{
    Natural,
    FillAvailableWidth
}

[TemplatePart(ContentHostPartName, typeof(ContentControl), IsRequired = true)]
[TemplatePart(SourceInputPartName, typeof(TextBox))]
public class MarkdownImage : TemplatedControl, CodeWF.Avalonia.Markdown.Rendering.IMarkdownInteractiveVisual
{
    private const string ContentHostPartName = "PART_ContentHost";
    private const string SourceInputPartName = "PART_SourceInput";
    private const double DefaultMaxImageWidth = 900;
    private const double DefaultMaxImageHeight = 520;
    private const long MaxImageByteCacheBytes = 128L * 1024 * 1024;
    private const long MaxImagePreviewCacheBytes = 64L * 1024 * 1024;

    private static readonly Dictionary<string, MarkdownImageSource> ImageByteCache = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> ImageByteCacheOrder = new();
    private static readonly object ImageByteCacheGate = new();
    private static long _imageByteCacheBytes;

    private static readonly Dictionary<string, byte[]> ImagePreviewCache = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> ImagePreviewCacheOrder = new();
    private static readonly object ImagePreviewCacheGate = new();
    private static long _imagePreviewCacheBytes;

    private ContentControl? _contentHost;
    private TextBox? _sourceInput;
    private Bitmap? _bitmap;
    private MemoryStream? _animatedStream;
    private byte[]? _imageBytes;
    private byte[]? _previewBytes;
    private string? _fileName;
    private bool _isSvg;
    private bool _isGif;
    private bool _isAnimated;
    private long _loadVersion;
    private CancellationTokenSource? _loadCts;
    private Point? _pressedPoint;
    private bool _isPointerDragging;
    private Size _imagePixelSize;

    // ---- 图上方源码编辑（Typora 式，仅编辑态 MarkdownViewer 的独立成段图片） ----
    private readonly DispatcherTimer _sourcePreviewTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly List<IDisposable> _sourceBindings = [];
    private bool _sourceEditing;
    private bool _sourcePreviewPending;
    private MarkdownViewer? _sourceViewer;
    private TopLevel? _sourceTopLevel;
    private MarkdownImageEditSpan? _sourceSpan;
    private string? _editMarkdown;
    private string? _editSyntax;
    private string? _editSource;
    private string? _editAlt;

    public static readonly StyledProperty<MarkdownImageSizing> SizingProperty =
        AvaloniaProperty.Register<MarkdownImage, MarkdownImageSizing>(nameof(Sizing));

    public MarkdownImageSizing Sizing
    {
        get => GetValue(SizingProperty);
        set => SetValue(SizingProperty, value);
    }

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MarkdownImage, string?>(nameof(Source));

    public static readonly StyledProperty<string?> AltTextProperty =
        AvaloniaProperty.Register<MarkdownImage, string?>(nameof(AltText));

    public static readonly StyledProperty<string?> ImageBasePathProperty =
        AvaloniaProperty.Register<MarkdownImage, string?>(nameof(ImageBasePath));

    /// <summary>
    /// 图片源解析回调：对 http(s) 源在远程获取前调用，返回本地文件路径即离线加载
    /// （仍走白名单与大小限制），返回 null 按原样远程获取。典型用途：CDN URL
    /// 反向映射本地文件离线显示。
    /// </summary>
    public static readonly StyledProperty<Func<string, string?>?> ImageSourceResolverProperty =
        AvaloniaProperty.Register<MarkdownImage, Func<string, string?>?>(nameof(ImageSourceResolver));

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? AltText
    {
        get => GetValue(AltTextProperty);
        set => SetValue(AltTextProperty, value);
    }

    public string? ImageBasePath
    {
        get => GetValue(ImageBasePathProperty);
        set => SetValue(ImageBasePathProperty, value);
    }

    public Func<string, string?>? ImageSourceResolver
    {
        get => GetValue(ImageSourceResolverProperty);
        set => SetValue(ImageSourceResolverProperty, value);
    }

    static MarkdownImage()
    {
        AffectsMeasure<MarkdownImage>(SizingProperty);
        SourceProperty.Changed.AddClassHandler<MarkdownImage>((image, _) => image.QueueLoad());
        ImageBasePathProperty.Changed.AddClassHandler<MarkdownImage>((image, _) => image.QueueLoad());
        ImageSourceResolverProperty.Changed.AddClassHandler<MarkdownImage>((image, _) => image.QueueLoad());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _contentHost = e.NameScope.Find<ContentControl>(ContentHostPartName);
        _sourceInput = e.NameScope.Find<TextBox>(SourceInputPartName);
        if (_sourceInput is { } sourceInput)
        {
            sourceInput.KeyDown += OnSourceInputKeyDown;
            sourceInput.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (_sourceEditing && sourceInput.IsKeyboardFocusWithin != true) CommitSourceEdit();
            });
            sourceInput.TextChanged += (_, _) =>
            {
                if (!_sourceEditing) return;
                _sourcePreviewTimer.Stop();
                _sourcePreviewPending = true;
                _sourcePreviewTimer.Start();
            };
            _sourcePreviewTimer.Tick += (_, _) =>
            {
                _sourcePreviewTimer.Stop();
                if (_sourceEditing && _sourcePreviewPending)
                {
                    _sourcePreviewPending = false;
                    ApplySourcePreview();
                }
            };
        }

        QueueLoad();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 块被重渲染/虚拟化回收前先提交未落盘的源码，避免用户输入丢失（与 MermaidDiagramView 一致）。
        CommitSourceEdit();
        Interlocked.Increment(ref _loadVersion);
        CancelCurrentLoad();
        ClearImageState();
        base.OnDetachedFromVisualTree(e);
    }

    private void QueueLoad()
    {
        var source = Source?.Trim();
        var version = Interlocked.Increment(ref _loadVersion);
        CancelCurrentLoad();

        if (string.IsNullOrWhiteSpace(source))
        {
            TrimImageState();
            SetContent(null);
            return;
        }

        var loadCts = new CancellationTokenSource();
        _loadCts = loadCts;
        _ = LoadAsync(source, version, loadCts);
    }

    private void CancelCurrentLoad()
    {
        var loadCts = _loadCts;
        _loadCts = null;
        loadCts?.Cancel();
    }

    private void ClearImageState()
    {
        var oldBitmap = _bitmap;
        var oldAnimatedStream = _animatedStream;
        _bitmap = null;
        _animatedStream = null;
        _imageBytes = null;
        _previewBytes = null;
        _fileName = null;
        _isSvg = false;
        _isGif = false;
        _isAnimated = false;
        _pressedPoint = null;
        _isPointerDragging = false;

        SetContent(null);
        oldBitmap?.Dispose();
        oldAnimatedStream?.Dispose();
    }

    /// <summary>
    /// 解码结果只保留在静态缓存里；控件被回收/重建时 <see cref="ClearImageState"/> 不应丢弃它，
    /// 否则块重新物化又要从字节重新解码（SVG/GIF 还要重新光栅化），滚动就卡。
    /// </summary>
    private void TrimImageState()
    {
        _bitmap = null;
        _imageBytes = null;
        _fileName = null;
        _pressedPoint = null;
        _isPointerDragging = false;
    }

    private async Task LoadAsync(string source, long version, CancellationTokenSource loadCts)
    {
        var token = loadCts.Token;
        var cacheKey = MarkdownImageSourceLoader.CreateCacheKey(source, ImageBasePath);

        try
        {
            token.ThrowIfCancellationRequested();
            var loadResult = await LoadBytesAsync(source, token);
            token.ThrowIfCancellationRequested();
            var previewBytes = ResolvePreviewBytes(cacheKey, loadResult);
            token.ThrowIfCancellationRequested();
            using var stream = new MemoryStream(previewBytes);
            var bitmap = new Bitmap(stream);
            var fileName = loadResult.FileName;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != _loadVersion || token.IsCancellationRequested)
                {
                    bitmap.Dispose();
                    return;
                }

                MemoryStream? animatedStream = null;
                var isAnimated = loadResult.IsGif && CountGifFrames(loadResult.Bytes) > 1;
                var content = loadResult.IsSvg
                    ? CreateSvgContent(bitmap)
                    : isAnimated
                        ? CreateAnimatedGifContent(loadResult.Bytes, bitmap, out animatedStream)
                        : CreateBitmapContent(bitmap);

                var oldBitmap = _bitmap;
                var oldAnimatedStream = _animatedStream;
                _bitmap = bitmap;
                _animatedStream = animatedStream;
                _imageBytes = loadResult.Bytes;
                _previewBytes = previewBytes;
                _fileName = fileName;
                _isSvg = loadResult.IsSvg;
                _isGif = loadResult.IsGif;
                _isAnimated = isAnimated;

                SetContent(content);
                oldBitmap?.Dispose();
                oldAnimatedStream?.Dispose();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (FileNotFoundException ex)
        {
            ;
            await ShowFallbackAsync(
                version,
                string.Format(MarkdownLocalization.GetResource(CodeWF.Avalonia.Markdown.MarkdownL.ImageFileNotFound), ex.FileName ?? source));
        }
        catch
        {
            await ShowFallbackAsync(
                version,
                string.Format(MarkdownLocalization.GetResource(CodeWF.Avalonia.Markdown.MarkdownL.ImageLoadFailed), AltText ?? source));
        }
        finally
        {
            if (ReferenceEquals(_loadCts, loadCts))
            {
                _loadCts = null;
            }

            loadCts.Dispose();
        }
    }

    /// <summary>
    /// 取静态预览字节：SVG/GIF 需要光栅化，结果按缓存 Key 复用，
    /// 让被回收后重新物化的图片块跳过解析与光栅化。
    /// </summary>
    private static byte[] ResolvePreviewBytes(string cacheKey, MarkdownImageSource loadResult)
    {
        if (!loadResult.IsSvg && !loadResult.IsGif)
        {
            return loadResult.Bytes;
        }

        if (TryGetCachedPreviewBytes(cacheKey, out var cached))
        {
            return cached;
        }

        var previewBytes = MarkdownImageRasterizer.RenderToPngBytes(loadResult);
        AddPreviewBytesToCache(cacheKey, previewBytes);
        return previewBytes;
    }

    /// <summary>GIF 只有一帧时按静态图渲染，避免为一张图启动动画时钟。</summary>
    private static int CountGifFrames(byte[] bytes)
    {
        try
        {
            using var sourceData = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(sourceData);
            return codec?.FrameCount ?? 1;
        }
        catch
        {
            return 1;
        }
    }

    private async Task<MarkdownImageSource> LoadBytesAsync(string source, CancellationToken token)
    {
        // 解析回调只对 http(s) 源有意义；解析结果参与缓存键，避免同 URL 在
        // 「已映射/未映射」两种取用方式间互相命中。
        var resolvedPath = TryResolveLocalSource(source);
        var cacheKey = MarkdownImageSourceLoader.CreateCacheKey(source, ImageBasePath)
                       + (resolvedPath is null ? string.Empty : $"|resolved:{resolvedPath}");
        if (TryGetCachedImageBytes(cacheKey, out var cached))
        {
            return cached;
        }

        var result = await MarkdownImageSourceLoader.LoadAsync(
            source, ImageBasePath, token, resolveLocalSource: resolvedPath is null ? null : _ => resolvedPath);
        AddImageBytesToCache(cacheKey, result);
        return result;
    }

    private string? TryResolveLocalSource(string source)
    {
        var resolver = ImageSourceResolver;
        if (resolver is null
            || !Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return resolver(source);
    }

    private static bool TryGetCachedImageBytes(string source, out MarkdownImageSource result)
    {
        lock (ImageByteCacheGate)
        {
            if (!ImageByteCache.TryGetValue(source, out result!))
            {
                return false;
            }

            ImageByteCacheOrder.Remove(source);
            ImageByteCacheOrder.AddLast(source);
            return true;
        }
    }

    private static void AddImageBytesToCache(string source, MarkdownImageSource result)
    {
        lock (ImageByteCacheGate)
        {
            if (result.Bytes.LongLength > MaxImageByteCacheBytes)
            {
                return;
            }

            if (ImageByteCache.TryGetValue(source, out var existing))
            {
                _imageByteCacheBytes -= existing.Bytes.LongLength;
                ImageByteCacheOrder.Remove(source);
                ImageByteCache[source] = result;
            }
            else
            {
                ImageByteCache[source] = result;
            }

            ImageByteCacheOrder.AddLast(source);
            _imageByteCacheBytes += result.Bytes.LongLength;
            while (_imageByteCacheBytes > MaxImageByteCacheBytes && ImageByteCacheOrder.First is { } oldest)
            {
                ImageByteCacheOrder.RemoveFirst();
                if (ImageByteCache.Remove(oldest.Value, out var removed))
                {
                    _imageByteCacheBytes -= removed.Bytes.LongLength;
                }
            }
        }
    }

    private static bool TryGetCachedPreviewBytes(string cacheKey, out byte[] previewBytes)
    {
        lock (ImagePreviewCacheGate)
        {
            if (!ImagePreviewCache.TryGetValue(cacheKey, out previewBytes!))
            {
                return false;
            }

            ImagePreviewCacheOrder.Remove(cacheKey);
            ImagePreviewCacheOrder.AddLast(cacheKey);
            return true;
        }
    }

    private static void AddPreviewBytesToCache(string cacheKey, byte[] previewBytes)
    {
        lock (ImagePreviewCacheGate)
        {
            if (previewBytes.LongLength > MaxImagePreviewCacheBytes)
            {
                return;
            }

            if (ImagePreviewCache.TryGetValue(cacheKey, out var existing))
            {
                _imagePreviewCacheBytes -= existing.LongLength;
                ImagePreviewCacheOrder.Remove(cacheKey);
            }

            ImagePreviewCache[cacheKey] = previewBytes;
            ImagePreviewCacheOrder.AddLast(cacheKey);
            _imagePreviewCacheBytes += previewBytes.LongLength;
            while (_imagePreviewCacheBytes > MaxImagePreviewCacheBytes && ImagePreviewCacheOrder.First is { } oldest)
            {
                ImagePreviewCacheOrder.RemoveFirst();
                if (ImagePreviewCache.Remove(oldest.Value, out var removed))
                {
                    _imagePreviewCacheBytes -= removed.LongLength;
                }
            }
        }
    }

    private void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        _pressedPoint = e.GetPosition(this);
        _isPointerDragging = false;
    }

    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedPoint is not { } pressedPoint)
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        if (Math.Abs(currentPoint.X - pressedPoint.X) > 4 || Math.Abs(currentPoint.Y - pressedPoint.Y) > 4)
        {
            _isPointerDragging = true;
        }
    }

    private void OnImagePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var shouldOpen = e.InitialPressMouseButton == MouseButton.Left && !_isPointerDragging;
        _pressedPoint = null;
        _isPointerDragging = false;

        if (!shouldOpen)
        {
            return;
        }

        e.Handled = true;
        switch (ResolveClickAction())
        {
            case ImageClickAction.SourceEdit:
                BeginSourceEdit();
                break;
            case ImageClickAction.PreviewWindow:
                OpenPreviewWindow();
                break;
            case ImageClickAction.ReadingZoom:
                OpenReadingZoomDialog();
                break;
        }
    }

    private enum ImageClickAction
    {
        SourceEdit,
        PreviewWindow,
        ReadingZoom,
        None
    }

    /// <summary>
    /// 图片点击行为：编辑态 MarkdownViewer（IsDiagramEditingEnabled=true）中独立成段的图片
    /// 在图上方展开源码编辑（Typora 式，回写走 MermaidEditing.EditCommitted 同一通道）；
    /// 纯阅读（=false）独立成段的图片弹出放大查看对话框（纯查看，与 Mermaid 阅读态一致，
    /// 即时编辑块内预览经 IsMediaZoomEnabled=false 保持无交互，点击交给块编辑流程）；
    /// 行内图片、幻灯片等嵌入场景（无源码区间）以及 Viewer 之外的独立使用保持原有
    /// 点击放大窗（MarkdownImagePreviewWindow，带缩放/旋转/另存）。
    /// </summary>
    private ImageClickAction ResolveClickAction()
    {
        var viewer = this.GetVisualAncestors().OfType<MarkdownViewer>().FirstOrDefault();
        if (viewer is null || MarkdownImageEdit.GetSourceSpan(this) is null || _sourceInput is null)
        {
            return ImageClickAction.PreviewWindow;
        }

        if (viewer.IsDiagramEditingEnabled)
        {
            return ImageClickAction.SourceEdit;
        }

        return viewer.IsMediaZoomEnabled ? ImageClickAction.ReadingZoom : ImageClickAction.None;
    }

    /// <summary>展开图上方源码框；已在编辑中时仅重新聚焦输入框。</summary>
    private void BeginSourceEdit()
    {
        if (_sourceEditing)
        {
            _sourceInput?.Focus();
            return;
        }

        var viewer = this.GetVisualAncestors().OfType<MarkdownViewer>().FirstOrDefault();
        if (_sourceInput is null
            || viewer?.Markdown is not { } markdown
            || MarkdownImageEdit.GetSourceSpan(this) is not { } span
            || span.Start < 0
            || span.Start + span.Length > markdown.Length)
        {
            return;
        }

        _sourceEditing = true;
        _sourceViewer = viewer;
        _sourceSpan = span;
        _editMarkdown = markdown;
        _editSyntax = markdown.Substring(span.Start, span.Length);
        _editSource = Source;
        _editAlt = AltText;
        _sourcePreviewPending = false;
        _sourceInput.Text = _editSyntax;

        // 源码框配色跟随 Viewer 主题属性（与 MermaidDiagramView 对源码框的 BindTheme 同一套来源）。
        foreach (var binding in _sourceBindings)
        {
            binding.Dispose();
        }

        _sourceBindings.Clear();
        _sourceBindings.Add(_sourceInput.Bind(TextBox.ForegroundProperty, viewer.GetObservable(MarkdownViewer.ForegroundProperty)));
        _sourceBindings.Add(_sourceInput.Bind(TextBox.BackgroundProperty, viewer.GetObservable(MarkdownViewer.CodeBackgroundBrushProperty)));
        _sourceBindings.Add(_sourceInput.Bind(TextBox.BorderBrushProperty, viewer.GetObservable(MarkdownViewer.BorderLineBrushProperty)));
        _sourceBindings.Add(_sourceInput.Bind(TextBox.FontFamilyProperty, viewer.GetObservable(MarkdownViewer.CodeFontFamilyProperty)));

        _sourceInput.IsVisible = true;
        _sourceViewer.PropertyChanged += OnSourceViewerChanged;
        _sourceTopLevel = TopLevel.GetTopLevel(this);
        _sourceTopLevel?.RemoveHandler(PointerPressedEvent, OnSourceOutsidePointer);
        _sourceTopLevel?.AddHandler(PointerPressedEvent, OnSourceOutsidePointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        _sourceInput.Focus();
        _sourceInput.CaretIndex = _sourceInput.Text?.Length ?? 0;
    }

    /// <summary>提交源码：通过 MermaidEditing.EditCommitted 同一事件通道回写宿主（原文不匹配即拒绝过期回写）。</summary>
    internal void CommitSourceEdit()
    {
        if (!_sourceEditing)
        {
            return;
        }

        _sourceEditing = false;
        _sourcePreviewTimer.Stop();
        _sourcePreviewPending = false;
        TeardownSourceEdit();
        var viewer = _sourceViewer;
        var span = _sourceSpan;
        var original = _editMarkdown;
        var syntax = _editSyntax;
        _sourceViewer = null;
        _sourceSpan = null;
        _editMarkdown = null;
        _editSyntax = null;
        _editSource = null;
        _editAlt = null;
        if (viewer is null || span is null || original is null || syntax is null || viewer.Markdown != original)
        {
            return;
        }

        var replacement = _sourceInput?.Text ?? string.Empty;
        if (string.Equals(replacement, syntax, StringComparison.Ordinal))
        {
            return;
        }

        var start = Math.Clamp(span.Start, 0, original.Length);
        var end = Math.Clamp(start + span.Length, start, original.Length);
        viewer.RaiseEvent(new MermaidEditEventArgs(original, original[..start] + replacement + original[end..], start));
    }

    /// <summary>Esc 取消：恢复点击前的图片显示，不产生文档回写。</summary>
    private void CancelSourceEdit()
    {
        if (!_sourceEditing)
        {
            return;
        }

        _sourceEditing = false;
        _sourcePreviewTimer.Stop();
        _sourcePreviewPending = false;
        TeardownSourceEdit();
        _sourceViewer = null;
        _sourceSpan = null;
        _editMarkdown = null;
        _editSyntax = null;
        Source = _editSource;
        AltText = _editAlt;
        _editSource = null;
        _editAlt = null;
    }

    private void TeardownSourceEdit()
    {
        _sourceTopLevel?.RemoveHandler(PointerPressedEvent, OnSourceOutsidePointer);
        _sourceTopLevel = null;
        if (_sourceViewer is { } viewer)
        {
            viewer.PropertyChanged -= OnSourceViewerChanged;
        }

        foreach (var binding in _sourceBindings)
        {
            binding.Dispose();
        }

        _sourceBindings.Clear();
        if (_sourceInput is { } input)
        {
            input.IsVisible = false;
        }
    }

    /// <summary>文档在编辑期间被外部改动：立即收起并拒绝过期回写（与 MermaidDiagramView 同策略）。</summary>
    private void OnSourceViewerChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == MarkdownViewer.MarkdownProperty || args.Property == MarkdownViewer.ImageBasePathProperty)
        {
            CommitSourceEdit();
        }
    }

    private void OnSourceOutsidePointer(object? sender, PointerPressedEventArgs args)
    {
        // 从顶层监听，工具条/侧栏以及已处理过指针的控件也能结束图片源码编辑。
        if (args.Source is Visual visual && (ReferenceEquals(visual, this) || visual.GetVisualAncestors().Contains(this)))
        {
            return;
        }

        CommitSourceEdit();
    }

    private void OnSourceInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_sourceEditing)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelSourceEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Control)
        {
            // 图片源码一行为主：回车（含 Ctrl+Enter，与 Mermaid 源码提交一致）即提交。
            CommitSourceEdit();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 编辑中按语法可用性做实时预览（300ms 防抖，与 MermaidDiagramView 的预览节奏一致）：
    /// 语法不完整时保持当前画面，避免半截路径把图片打成加载失败。
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex SourceSyntax =
        new(@"^\s*!\[(?<alt>[^\]]*)\]\(\s*<?(?<url>[^)\s>]*)>?(?:\s+""(?<title>[^""]*)"")?\s*\)\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private void ApplySourcePreview()
    {
        if (_sourceInput?.Text is not { } text
            || SourceSyntax.Match(text) is not { Success: true } match)
        {
            return;
        }

        var source = match.Groups["url"].Value;
        var alt = match.Groups["alt"].Value;
        if (!string.Equals(Source, source, StringComparison.Ordinal))
        {
            Source = source;
            AltText = alt;
        }
    }

    /// <summary>保存、关闭或切换文档前提交仍有焦点的图片源码（宿主经 MermaidEditing.CommitPendingEdits 调用）。</summary>
    internal void CommitPendingSourceEdit() => CommitSourceEdit();

    private void OpenPreviewWindow()
    {
        if (_bitmap is null || _previewBytes is null)
        {
            return;
        }

        Bitmap previewBitmap;
        try
        {
            using var previewStream = new MemoryStream(_previewBytes);
            previewBitmap = new Bitmap(previewStream);
        }
        catch
        {
            return;
        }

        var title = !string.IsNullOrWhiteSpace(AltText) ? AltText : Source;
        var window = new MarkdownImagePreviewWindow(
            previewBitmap,
            _imageBytes ?? _previewBytes,
            _fileName ?? (_isSvg ? "markdown-image.svg" : _isGif ? "markdown-image.gif" : "markdown-image.png"),
            title,
            _isSvg,
            _isAnimated);
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>
    /// 纯阅读的放大查看对话框（纯查看）：同一图片源在 ~80% 可用区域的遮罩层里展示，
    /// SVG 走矢量渲染、动图继续播帧，点遮罩或 Esc 关闭。与 <see cref="OpenPreviewWindow"/>
    /// 的区别：阅读放大挂在宿主窗口 OverlayLayer 内、无工具条；预览窗是独立窗口。
    /// </summary>
    private void OpenReadingZoomDialog()
    {
        if (_bitmap is null || _previewBytes is null)
        {
            return;
        }

        Bitmap previewBitmap;
        try
        {
            using var previewStream = new MemoryStream(_previewBytes);
            previewBitmap = new Bitmap(previewStream);
        }
        catch
        {
            return;
        }

        var bytes = _imageBytes ?? _previewBytes;
        Control content;
        IDisposable? cleanup = null;
        if (_isSvg)
        {
            // 矢量渲染：放大后文字与线条不发糊（失败时工厂内部回落预览位图）。
            content = MarkdownImagePreviewWindow.CreateSvgContent(bytes, previewBitmap);
        }
        else if (_isAnimated)
        {
            content = MarkdownImagePreviewWindow.CreateAnimatedGifContent(bytes, previewBitmap, out var stream);
            cleanup = stream;
        }
        else
        {
            content = MarkdownImagePreviewWindow.CreateBitmapContent(previewBitmap);
        }

        // 遮罩内大图按卡片适配（Uniform），避免 Fill 在非等比卡片里拉伸变形。
        switch (content)
        {
            case Image image:
                image.Stretch = Stretch.Uniform;
                break;
            case global::Avalonia.Svg.Skia.Svg svg:
                svg.Stretch = Stretch.Uniform;
                break;
        }

        var viewer = this.GetVisualAncestors().OfType<MarkdownViewer>().FirstOrDefault();
        MarkdownMediaZoomDialog.Show(TopLevel.GetTopLevel(this), content, viewer, cleanup, detachWatch: this);
    }

    private Control CreateBitmapContent(Bitmap bitmap)
    {
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ApplyImageChrome(image, bitmap);
        AttachImageClick(image);
        return image;
    }

    private Control CreateSvgContent(Bitmap previewBitmap)
    {
        return CreateBitmapContent(previewBitmap);
    }

    private Control CreateAnimatedGifContent(byte[] gifBytes, Bitmap previewBitmap, out MemoryStream animatedStream)
    {
        animatedStream = new MemoryStream(gifBytes, writable: false);
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ImageBehavior.SetAnimatedSource(image, new AnimatedImageSourceStream(animatedStream));
        ImageBehavior.SetRepeatBehavior(image, RepeatBehavior.Forever);
        ApplyImageChrome(image, previewBitmap);
        AttachImageClick(image);
        return image;
    }

    private void ApplyImageChrome(Control control, Bitmap bitmap)
    {
        _imagePixelSize = new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        control.Classes.Add(MarkdownStyleKeys.ImageContent);
        control.MaxWidth = DefaultMaxImageWidth;
        control.MaxHeight = DefaultMaxImageHeight;
        control.Margin = new Thickness(0, 6, 0, 10);

        var (width, height) = CalculateDisplaySize(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        control.Width = width;
        control.Height = height;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_contentHost?.Content is Image image && _imagePixelSize.Width > 0)
        {
            var fill = Sizing == MarkdownImageSizing.FillAvailableWidth && double.IsFinite(availableSize.Width);
            var natural = CalculateDisplaySize(_imagePixelSize.Width, _imagePixelSize.Height);
            var width = fill ? Math.Max(1, availableSize.Width - image.Margin.Left - image.Margin.Right) : natural.Width;
            image.MaxWidth = fill ? double.PositiveInfinity : DefaultMaxImageWidth;
            image.MaxHeight = fill ? double.PositiveInfinity : DefaultMaxImageHeight;
            image.Width = width;
            image.Height = fill ? width * _imagePixelSize.Height / _imagePixelSize.Width : natural.Height;
        }
        return base.MeasureOverride(availableSize);
    }

    private (double Width, double Height) CalculateDisplaySize(double imageWidth, double imageHeight)
    {
        imageWidth = Math.Max(1, imageWidth);
        imageHeight = Math.Max(1, imageHeight);

        if (!double.IsNaN(Width) && Width > 0 && !double.IsNaN(Height) && Height > 0)
        {
            var scale = Math.Min(Width / imageWidth, Height / imageHeight);
            return (imageWidth * scale, imageHeight * scale);
        }

        if (!double.IsNaN(Width) && Width > 0)
        {
            var scale = Width / imageWidth;
            return (Width, imageHeight * scale);
        }

        if (!double.IsNaN(Height) && Height > 0)
        {
            var scale = Height / imageHeight;
            return (imageWidth * scale, Height);
        }

        var maxWidth = double.IsNaN(MaxWidth) || double.IsInfinity(MaxWidth) || MaxWidth <= 0
            ? DefaultMaxImageWidth
            : MaxWidth;
        var maxHeight = double.IsNaN(MaxHeight) || double.IsInfinity(MaxHeight) || MaxHeight <= 0
            ? DefaultMaxImageHeight
            : MaxHeight;
        var maxScale = Math.Min(1, Math.Min(maxWidth / imageWidth, maxHeight / imageHeight));
        return (imageWidth * maxScale, imageHeight * maxScale);
    }

    private void AttachImageClick(Control control)
    {
        control.AddHandler(
            InputElement.PointerPressedEvent,
            OnImagePointerPressed,
            RoutingStrategies.Bubble);
        control.AddHandler(
            InputElement.PointerMovedEvent,
            OnImagePointerMoved,
            RoutingStrategies.Bubble);
        control.AddHandler(
            InputElement.PointerReleasedEvent,
            OnImagePointerReleased,
            RoutingStrategies.Bubble);
    }

    private async Task ShowFallbackAsync(long version, string text)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != _loadVersion)
            {
                return;
            }

            var oldBitmap = _bitmap;
            var oldAnimatedStream = _animatedStream;
            _bitmap = null;
            _animatedStream = null;
            _imageBytes = null;
            _previewBytes = null;
            _fileName = null;
            _isSvg = false;
            _isGif = false;
            _isAnimated = false;

            var fallback = new Border
            {
                Child = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            fallback.Classes.Add(MarkdownStyleKeys.ImageFallback);
            if (fallback.Child is TextBlock textBlock)
            {
                textBlock.Classes.Add(MarkdownStyleKeys.ImageFallbackText);
            }

            SetContent(fallback);
            oldBitmap?.Dispose();
            oldAnimatedStream?.Dispose();
        });
    }

    private void SetContent(Control? content)
    {
        if (_contentHost is not null)
        {
            _contentHost.Content = content;
            _contentHost.InvalidateMeasure();
        }

        InvalidateMeasure();
        InvalidateArrange();
    }
}
