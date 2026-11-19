using global::Avalonia;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Media.TextFormatting;
using global::Avalonia.Utilities;
using global::Avalonia.VisualTree;
using System.Collections.Concurrent;
using CodeWF.Avalonia.Markdown.MathRendering;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>保留 TextBox 的输入法、选择和光标；显示行内样式并保持源码字符索引。
/// 行内图片：源码区间以零宽占位+撑行高样式维持索引契约，图片按版面几何叠绘；
/// 光标/选区进入语法区间时显源码（muya 行为）。</summary>
internal sealed class MarkdownInlinePresenter(MarkdownBlockViewOptions options, bool plain) : TextPresenter
{
    private double _availableWidth = double.PositiveInfinity;
    protected override Size MeasureOverride(Size availableSize)
    { _availableWidth = availableSize.Width; return base.MeasureOverride(availableSize); }
    protected override Size ArrangeOverride(Size finalSize)
    { _availableWidth = finalSize.Width; return base.ArrangeOverride(finalSize); }

    /// <summary>当前版面的图片叠绘清单（CreateTextLayout 重建时刷新）。</summary>
    private readonly List<ImageOverlay> _imageOverlays = [];
    /// <summary>当前版面的行内数学叠绘清单（几何在绘制时按当前版面命中）。</summary>
    private readonly List<MathOverlay> _mathOverlays = [];
    private TextLayout? _lastLayout;

    private sealed record ImageOverlay(int Start, Bitmap? Bitmap, double Width, double Height);

    private sealed record MathOverlay(int Start, string Latex);

    private static readonly ConcurrentDictionary<string, Bitmap?> ImageBank = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, MarkdownMathPainter> MathPainters = new(StringComparer.Ordinal);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 嵌入对象区间的显源码/叠绘随光标与选区切换，需要整层重建（纯文本块零开销）。
        if ((_imageOverlays.Count > 0 || _mathOverlays.Count > 0)
            && change.Property.Name is "CaretIndex" or "SelectionStart" or "SelectionEnd")
            InvalidateTextLayout();
    }

    private Bitmap? LoadImage(string url)
    {
        var path = ResolveImagePath(url, options.ImageBasePath);
        if (path is null) return null;
        return ImageBank.GetOrAdd(path, Decode);
        static Bitmap? Decode(string p)
        {
            try
            {
                if (!File.Exists(p)) return null;
                using var stream = File.OpenRead(p);
                return Bitmap.DecodeToWidth(stream, 640);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string? ResolveImagePath(string url, string? basePath)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Scheme == "file" ? uri.LocalPath : null; // http(s) 等远程图暂不内嵌，回退源码显示
        if (Path.IsPathRooted(url)) return url;
        if (string.IsNullOrEmpty(basePath)) return null;
        // 应用契约：basePath 为文档完整路径（相对图片相对其所在目录解析）。
        var baseDir = File.Exists(basePath) ? Path.GetDirectoryName(basePath) : basePath;
        return string.IsNullOrEmpty(baseDir) ? null : Path.GetFullPath(Path.Combine(baseDir, url));
    }

    protected override TextLayout CreateTextLayout()
    {
        if (plain) return base.CreateTextLayout();
        var source = Text ?? "";
        var face = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var styles = new List<ValueSpan<TextRunProperties>>();
        _imageOverlays.Clear();
        _mathOverlays.Clear();
        // 隐藏的语法仍占一个源码索引，但不能包含实际换行而影响段落排版。
        var display = Enumerable.Repeat('\u200b', source.Length).ToArray();
        var cursor = 0;
        foreach (var run in MarkdownTextRunParser.Parse(source))
        {
            var start = Math.Clamp(run.SourceStart, cursor, source.Length);
            if (start > cursor) Hide(cursor, start - cursor);
            if ((run.ImageUrl is not null || run.MathLatex is not null) && run.SourceLength > 0)
            {
                // 嵌入对象（行内图片/行内数学）：整个语法区间显源码（光标/选区在内）或占位叠绘。
                var spanLength = Math.Min(run.SourceLength, source.Length - start);
                var revealed = SelectionStart != SelectionEnd
                    ? start < SelectionEnd && SelectionStart < start + spanLength
                    : CaretIndex > start && CaretIndex < start + spanLength;
                if (revealed)
                {
                    source.AsSpan(start, spanLength).CopyTo(display.AsSpan(start));
                    styles.Add(new(start, spanLength,
                        new GenericTextRunProperties(face, FontSize, foregroundBrush: Foreground)));
                }
                else
                {
                    var em = FontSize * 2;
                    if (run.ImageUrl is not null)
                    {
                        var bitmap = LoadImage(run.ImageUrl);
                        var (width, height) = ImageDrawSize(bitmap);
                        em = bitmap is null ? FontSize : Math.Min(height, FontSize * 2);
                        if (bitmap is not null)
                            _imageOverlays.Add(new ImageOverlay(start, bitmap, width, height));
                    }
                    else
                    {
                        _mathOverlays.Add(new MathOverlay(start, run.MathLatex!));
                    }
                    // 占位字符用空格而非零宽符：全 em 下零宽符触发字体回退会崩掉整行 shaping
                    //（实测整行文字消失）；空格有真实宽度（预留对象横空间）、无回退、透明不可见。
                    display.AsSpan(start, spanLength).Fill(' ');
                    styles.Add(new(start, spanLength, new GenericTextRunProperties(face,
                        em, foregroundBrush: Brushes.Transparent)));
                }
                cursor = start + spanLength;
                continue;
            }
            var length = Math.Min(run.Text.Length, source.Length - start);
            if (length > 0)
            {
                run.Text.AsSpan(0, length).CopyTo(display.AsSpan(start));
                var typeface = new Typeface(run.Code ? options.CodeFontFamily : FontFamily,
                    run.Italic ? FontStyle.Italic : FontStyle, run.Bold ? FontWeight.Bold : FontWeight, FontStretch);
                var decoration = run.Strike ? TextDecorations.Strikethrough : run.LinkUrl is not null ? TextDecorations.Underline : null;
                // 样式优先级：行内代码 > <span style>（对比度不足回退主题） > 链接 > 默认前景。
                var themeTextColor = (Foreground as SolidColorBrush)?.Color ?? Colors.Black;
                var spanBackgroundColor = MarkdownSpanStyle.TryGetColor(run.Background);
                var foreground = run.Code ? options.CodeBrush
                    : MarkdownSpanStyle.EnsureReadable(
                          MarkdownSpanStyle.TryGetColor(run.Foreground),
                          spanBackgroundColor,
                          themeTextColor) is { } readable
                        ? new SolidColorBrush(readable)
                        : (run.LinkUrl is not null ? options.LinkBrush : Foreground);
                var background = run.Code ? options.CodeBackgroundBrush
                    : spanBackgroundColor is { } backgroundColor ? new SolidColorBrush(backgroundColor) : null;
                styles.Add(new(start, length, new GenericTextRunProperties(typeface, run.FontSize ?? FontSize, decoration,
                    foreground, background)));
            }
            cursor = start + length;
        }
        if (cursor < source.Length) Hide(cursor, source.Length - cursor);
        var text = new string(display);
        if (text.Length == 0)
        {
            // 空块光标需要行高支撑：占位/隐藏管线对空源码产出空 TextLayout，
            // HitTestTextPosition(0) 高度为 0，光标画出即不可见（真机"没有光标"根因）。
            // 布局层填一个透明空格，仅撑行高；源码索引契约不动（TextBox.Text 仍为空）。
            text = " ";
            styles.Add(new(0, 1, new GenericTextRunProperties(face, FontSize, foregroundBrush: Brushes.Transparent)));
        }
        var compositionStart = Math.Clamp(CaretIndex, 0, source.Length);
        var compositionLength = PreeditText?.Length ?? 0;
        if (compositionLength > 0)
        {
            // 预编辑沿用光标处的富文本样式，不重新解析未提交的 Markdown。
            var inherited = styles.FirstOrDefault(s => s.Start <= compositionStart && s.Start + s.Length > compositionStart && s.Value.FontRenderingEmSize > .01).Value
                ?? styles.LastOrDefault(s => s.Start < compositionStart && s.Value.FontRenderingEmSize > .01).Value
                ?? new GenericTextRunProperties(face, FontSize, foregroundBrush: Foreground);
            var shifted = new List<ValueSpan<TextRunProperties>>();
            foreach (var span in styles)
            {
                var end = span.Start + span.Length;
                if (end <= compositionStart) shifted.Add(span);
                else if (span.Start >= compositionStart) shifted.Add(new(span.Start + compositionLength, span.Length, span.Value));
                else
                {
                    shifted.Add(new(span.Start, compositionStart - span.Start, span.Value));
                    shifted.Add(new(compositionStart + compositionLength, end - compositionStart, span.Value));
                }
            }
            shifted.Add(new(compositionStart, compositionLength, inherited));
            styles = shifted.OrderBy(s => s.Start).ToList();
            text = text.Insert(compositionStart, PreeditText!);
        }
        var selectionStart = Math.Min(SelectionStart, SelectionEnd);
        var selectionEnd = Math.Max(SelectionStart, SelectionEnd);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        foreach (var span in styles)
        {
            var end = span.Start + span.Length;
            var boundaries = new[] { span.Start, end, selectionStart, selectionEnd, compositionStart, compositionStart + compositionLength }
                .Where(index => index >= span.Start && index <= end).Distinct().Order().ToArray();
            for (var i = 0; i + 1 < boundaries.Length; i++)
            {
                var start = boundaries[i]; var finish = boundaries[i + 1]; var properties = span.Value;
                var composing = compositionLength > 0 && start >= compositionStart && finish <= compositionStart + compositionLength;
                var selected = start >= selectionStart && finish <= selectionEnd && selectionStart != selectionEnd;
                if ((selected && SelectionForegroundBrush is not null || composing) && properties.FontRenderingEmSize > .01)
                    properties = new GenericTextRunProperties(properties.Typeface, properties.FontRenderingEmSize,
                        composing ? TextDecorations.Underline : properties.TextDecorations,
                        selected ? SelectionForegroundBrush : properties.ForegroundBrush,
                        selected && SelectionForegroundBrush is not null ? null : properties.BackgroundBrush,
                        properties.BaselineAlignment, properties.CultureInfo, properties.FontFeatures);
                overrides.Add(new(start, finish - start, properties));
            }
        }
        var mixedSizes = styles.Any(span => span.Value.FontRenderingEmSize > FontSize + .01);
        var layoutFontSize = mixedSizes ? MarkdownLineMetrics.StrutFontSize(face, FontSize, LineHeight) : FontSize;
        var layout = new TextLayout(text, face, layoutFontSize, Foreground, TextAlignment, TextWrapping,
            maxWidth: _availableWidth > 0 ? _availableWidth : double.PositiveInfinity,
            flowDirection: FlowDirection, fontFeatures: FontFeatures,
            lineHeight: mixedSizes ? double.NaN : LineHeight, letterSpacing: LetterSpacing, textStyleOverrides: overrides);
        _lastLayout = layout;
        return layout;
        void Hide(int start, int length) => styles.Add(new(start, length,
            new GenericTextRunProperties(face, .001, foregroundBrush: Brushes.Transparent)));
    }

    /// <summary>显示尺寸：高 2×正文行字号封顶（更高会毒化整行版面，实测文字消失）、宽随可用宽收，保纵横比。</summary>
    private (double Width, double Height) ImageDrawSize(Bitmap? bitmap)
    {
        if (bitmap is null) return (0, 0);
        var maxWidth = Math.Max(80, _availableWidth - 32);
        var height = Math.Min(bitmap.PixelSize.Height, FontSize * 2);
        var width = bitmap.PixelSize.Width * height / bitmap.PixelSize.Height;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = bitmap.PixelSize.Height * width / bitmap.PixelSize.Width;
        }
        return (width, height);
    }

    /// <summary>叠绘嵌入对象（行内图片 + 行内数学；宿主 TextBox 渲染层调用，presenter 自身 Render 密封）。
    /// 几何来自当前 TextLayout 命中测试，坐标映射到 coordinateSpace。</summary>
    internal void DrawImageOverlays(DrawingContext context, Visual coordinateSpace)
    {
        if ((_imageOverlays.Count == 0 && _mathOverlays.Count == 0) || _lastLayout is null) return;
        var offset = this.TranslatePoint(default, coordinateSpace);
        foreach (var overlay in _imageOverlays)
        {
            if (overlay.Bitmap is null) continue;
            var origin = _lastLayout.HitTestTextPosition(overlay.Start);
            var top = origin.Y + Math.Max(0, (origin.Height - overlay.Height) / 2);
            var pos = offset is { } o ? new Point(origin.X + o.X, top + o.Y) : new Point(origin.X, top);
            context.DrawImage(overlay.Bitmap, new Rect(pos, new Size(overlay.Width, overlay.Height)));
        }

        if (_mathOverlays.Count == 0) return;
        var foreground = (Foreground as SolidColorBrush)?.Color ?? Colors.Black;
        foreach (var overlay in _mathOverlays)
        {
            var painter = MathPainters.GetOrAdd(overlay.Latex, latex => new MarkdownMathPainter
            {
                LaTeX = latex,
                DisplayErrorInline = false
            });
            painter.FontSize = (float)FontSize;
            painter.TextColor = foreground;
            painter.ErrorColor = foreground;
            var bounds = painter.Measure();
            var size = new Size(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
            var origin = _lastLayout.HitTestTextPosition(overlay.Start);
            var top = origin.Y + Math.Max(0, (origin.Height - size.Height) / 2);
            var pos = offset is { } o ? new Point(origin.X + o.X, top + o.Y) : new Point(origin.X, top);
            using var translated = context.PushTransform(Matrix.CreateTranslation(pos));
            var canvas = new AvaloniaMathCanvas(context, size);
            painter.Draw(canvas, CSharpMath.Rendering.FrontEnd.TextAlignment.Left);
        }
    }
}
