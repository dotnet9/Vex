using AnimatedImage.Avalonia;
using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using CodeWF.Toolkit.Lang;
using Svg.Skia;
using System.Text;

namespace CodeWF.Avalonia.Markdown.Controls;

internal sealed class MarkdownImagePreviewWindow : Window
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8.0;
    private const double ZoomStep = 1.2;

    private readonly byte[] _imageBytes;
    private readonly string _fileName;
    private readonly Bitmap _bitmap;
    private readonly MemoryStream? _animatedStream;
    private readonly Control _image;
    private readonly Border _imageHost;
    private readonly ScrollViewer _scrollViewer;
    private readonly TextBlock _zoomText;
    private readonly List<(Button Button, string ResourceKey)> _localizedButtons = [];
    private readonly double _originalWidth;
    private readonly double _originalHeight;
    private readonly bool _usesDefaultTitle;
    private double _zoom = 1.0;
    private int _rotation;
    private Point? _panStart;
    private Vector _panStartOffset;
    private bool _isPanning;

    public MarkdownImagePreviewWindow(Bitmap bitmap, byte[] imageBytes, string fileName, string? title, bool isSvg, bool isAnimatedGif)
    {
        _bitmap = bitmap;
        _imageBytes = imageBytes;
        _fileName = string.IsNullOrWhiteSpace(fileName) ? "markdown-image.png" : fileName;
        _originalWidth = Math.Max(1, bitmap.PixelSize.Width);
        _originalHeight = Math.Max(1, bitmap.PixelSize.Height);
        _usesDefaultTitle = string.IsNullOrWhiteSpace(title);

        Title = _usesDefaultTitle ? MarkdownLocalization.GetResource(MarkdownL.ImagePreviewTitle) : title;
        Classes.Add(MarkdownStyleKeys.ImagePreviewWindow);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _zoomText = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _zoomText.Classes.Add(MarkdownStyleKeys.ImagePreviewZoomText);

        _image = isSvg
            ? CreateSvgContent(imageBytes, bitmap)
            : isAnimatedGif
                ? CreateAnimatedGifContent(imageBytes, bitmap, out _animatedStream)
                : CreateBitmapContent(bitmap);

        _imageHost = new Border
        {
            Child = _image,
            ClipToBounds = false,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _imageHost.Classes.Add(MarkdownStyleKeys.ImagePreviewContent);
        _imageHost.PointerPressed += OnPanPointerPressed;
        _imageHost.PointerMoved += OnPanPointerMoved;
        _imageHost.PointerReleased += OnPanPointerReleased;
        _imageHost.PointerCaptureLost += OnPanPointerCaptureLost;

        _scrollViewer = new ScrollViewer
        {
            Content = _imageHost,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        Content = CreateLayout();
        PointerWheelChanged += OnPointerWheelChanged;
        I18nManager.Instance.CultureChanged += OnCultureChanged;
        I18nManager.Instance.ResourcesChanged += OnCultureChanged;
        Closed += OnClosed;
        UpdateImageSize();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        I18nManager.Instance.CultureChanged -= OnCultureChanged;
        I18nManager.Instance.ResourcesChanged -= OnCultureChanged;
        _bitmap.Dispose();
        _animatedStream?.Dispose();
    }

    /// <summary>位图内容：阅读放大对话框（MarkdownMediaZoomDialog）也复用这三类内容工厂，保持同一渲染口径。</summary>
    internal static Control CreateBitmapContent(Bitmap bitmap)
    {
        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center
        };
    }

    internal static Control CreateSvgContent(byte[] svgBytes, Bitmap fallbackBitmap)
    {
        try
        {
            return new global::Avalonia.Svg.Skia.Svg(new Uri("file:///", UriKind.Absolute))
            {
                Source = Encoding.UTF8.GetString(svgBytes),
                Stretch = Stretch.Fill,
                AnimationBackend = SvgAnimationHostBackend.DispatcherTimer,
                AnimationFrameInterval = TimeSpan.FromMilliseconds(33),
                AnimationPlaybackRate = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = RelativePoint.Center
            };
        }
        catch
        {
            return CreateBitmapContent(fallbackBitmap);
        }
    }

    internal static Control CreateAnimatedGifContent(byte[] gifBytes, Bitmap fallbackBitmap, out MemoryStream animatedStream)
    {
        animatedStream = new MemoryStream(gifBytes, writable: false);
        var image = new Image
        {
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center
        };
        ImageBehavior.SetAnimatedSource(image, new AnimatedImageSourceStream(animatedStream));
        ImageBehavior.SetRepeatBehavior(image, RepeatBehavior.Forever);
        return image;
    }

    private Control CreateLayout()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(1, GridUnitType.Star))
            }
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        toolbar.Classes.Add(MarkdownStyleKeys.ImagePreviewToolbar);

        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewZoomOut, () => SetZoom(_zoom / ZoomStep)));
        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewZoomIn, () => SetZoom(_zoom * ZoomStep)));
        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewActualSize, () => SetZoom(1.0)));
        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewFit, FitToWindow));
        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewRotateLeft, () => Rotate(-90)));
        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewRotateRight, () => Rotate(90)));
        toolbar.Children.Add(CreateButton(MarkdownL.ImagePreviewSaveAs, async () => await SaveAsAsync()));
        toolbar.Children.Add(_zoomText);

        Grid.SetRow(toolbar, 0);
        Grid.SetRow(_scrollViewer, 1);
        root.Children.Add(toolbar);
        root.Children.Add(_scrollViewer);
        return root;
    }

    private Button CreateButton(string resourceKey, Action action)
    {
        var button = new Button
        {
            Content = MarkdownLocalization.GetResource(resourceKey),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Classes.Add(MarkdownStyleKeys.ImagePreviewButton);
        button.Click += (_, _) => action();
        _localizedButtons.Add((button, resourceKey));
        return button;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        SetZoom(e.Delta.Y > 0 ? _zoom * ZoomStep : _zoom / ZoomStep);
        e.Handled = true;
    }

    private void OnPanPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_imageHost).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _panStart = e.GetPosition(this);
        _panStartOffset = _scrollViewer.Offset;
        _imageHost.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(_imageHost);
        e.Handled = true;
    }

    private void OnPanPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || _panStart is not { } panStart)
        {
            return;
        }

        var current = e.GetPosition(this);
        SetScrollOffset(_panStartOffset.X - (current.X - panStart.X), _panStartOffset.Y - (current.Y - panStart.Y));
        e.Handled = true;
    }

    private void OnPanPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        EndPan(e.Pointer);
        e.Handled = true;
    }

    private void OnPanPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndPan(null);
    }

    private void EndPan(IPointer? pointer)
    {
        _isPanning = false;
        _panStart = null;
        _imageHost.Cursor = new Cursor(StandardCursorType.Hand);
        pointer?.Capture(null);
    }

    private void SetScrollOffset(double x, double y)
    {
        var maxX = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        _scrollViewer.Offset = new Vector(Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
    }

    private void FitToWindow()
    {
        var padding = _imageHost.Padding;
        var availableWidth = Math.Max(1, _scrollViewer.Bounds.Width - padding.Left - padding.Right);
        var availableHeight = Math.Max(1, _scrollViewer.Bounds.Height - padding.Top - padding.Bottom);
        var rotated = Math.Abs(_rotation % 180) == 90;
        var width = rotated ? _originalHeight : _originalWidth;
        var height = rotated ? _originalWidth : _originalHeight;
        SetZoom(Math.Min(availableWidth / width, availableHeight / height));
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        UpdateImageSize();
    }

    private void Rotate(int degrees)
    {
        _rotation = ((_rotation + degrees) % 360 + 360) % 360;
        UpdateImageSize();
    }

    private void UpdateImageSize()
    {
        _image.Width = _originalWidth * _zoom;
        _image.Height = _originalHeight * _zoom;
        _image.RenderTransform = new RotateTransform(_rotation);

        var rotated = Math.Abs(_rotation % 180) == 90;
        _imageHost.Width = (rotated ? _originalHeight : _originalWidth) * _zoom;
        _imageHost.Height = (rotated ? _originalWidth : _originalHeight) * _zoom;
        _zoomText.Text = string.Format(MarkdownLocalization.GetResource(MarkdownL.ImagePreviewZoomStatus), _zoom, _rotation);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnCultureChanged(sender, e));
            return;
        }
        if (_usesDefaultTitle)
        {
            Title = MarkdownLocalization.GetResource(MarkdownL.ImagePreviewTitle);
        }

        foreach (var (button, resourceKey) in _localizedButtons)
        {
            button.Content = MarkdownLocalization.GetResource(resourceKey);
        }

        UpdateImageSize();
    }

    private async Task SaveAsAsync()
    {
        if (!StorageProvider.CanSave)
        {
            return;
        }

        var extension = Path.GetExtension(_fileName).TrimStart('.');
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "png";
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = _fileName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(MarkdownLocalization.GetResource(MarkdownL.ImagePreviewImagesFileType))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.svg"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/bmp", "image/gif", "image/svg+xml"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(_imageBytes);
    }
}
