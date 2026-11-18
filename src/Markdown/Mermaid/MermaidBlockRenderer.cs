// Mermaid 渲染手法移植自 MarkView.Avalonia.Mermaid（MIT, Copyright (c) Nicolas Musset）：
// Mermaider 输出的 SVG 使用 CSS 自定义属性（var(--_xxx)），而 SkiaSharp 不实现 CSS
// 级联会静默忽略 var()，因此渲染后需按 Mermaider 的 color-mix 公式把变量内联为具体色值。

using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;

using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Styling;
using global::Avalonia.Svg.Skia;
using global::Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;

using Markdig.Syntax;

using Mermaider;
using Mermaider.Models;

using MermaidRenderOptions = Mermaider.Models.RenderOptions;

namespace CodeWF.Avalonia.Markdown.Mermaid;

/// <summary>
/// ```mermaid 围栏代码块渲染器：Mermaider（纯 .NET，无 JavaScript）解析渲染为 SVG，
/// 后台线程出图并内联 CSS 变量，跟随明暗主题重渲，失败回落显示源码。
/// </summary>
public sealed class MermaidBlockRenderer : IMarkdownBlockRenderer
{
    private static readonly Regex ThemeChangeProperty = new("^ActualThemeVariant$", RegexOptions.Compiled);

    /// <summary>缓存图表上限：按「源码 + 主题配色」缓存，块被虚拟化回收后重建时不再重新解析渲染。</summary>
    private const int MaxCachedDiagrams = 48;

    private static readonly Dictionary<string, SvgSource?> DiagramCache = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> DiagramCacheOrder = new();
    private static readonly object DiagramCacheGate = new();
    private static readonly Dictionary<string, Task<SvgSource?>> PendingDiagrams = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim RenderSlots = new(2);

    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (context is CodeWF.Avalonia.Markdown.Controls.MarkdownViewer { SyntaxOptions.Diagrams: false }) return null;
        if (block is not FencedCodeBlock fenced
            || !string.Equals(fenced.Info?.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var diagram = (Border)CreateMermaidBlock(ExtractSource(fenced), context);
        // 交互包裹条件：编辑态（MermaidEditing.IsEnabled）走 MermaidDiagramView 的源码编辑；
        // 纯阅读（IsDiagramEditingEnabled=false）也包一层——点击弹出放大查看对话框。
        // 即时编辑块内预览（IsMediaZoomEnabled=false）不包：预览保持纯渲染，点击交给块编辑流程。
        if (context is MarkdownViewer viewer && sourceMarkdown is not null && viewer.IsMediaZoomEnabled
            && (MermaidEditing.GetIsEnabled(viewer) || !viewer.IsDiagramEditingEnabled))
        {
            var preview = diagram.Child!;
            diagram.Child = null;
            diagram.Child = new MermaidDiagramView(viewer, new(sourceMarkdown, fenced, context.CurrentBlockOrigin), preview, context);
        }
        return diagram;
    }

    internal static Control CreateMermaidBlock(string source, IMarkdownRenderContext? context = null)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // 原型 .diagram-preview：图嵌在带边框的预览卡里，四周 22 留白、最低 120。
        // 宿主把语言徽标悬浮在卡片右上角，全靠这条留白带站位——没有它，
        // 放大后图表被压扁，徽标就直接压在节点上。
        var preview = new Border
        {
            Child = image,
            MinHeight = 120,
            Padding = new Thickness(22),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        preview.Classes.Add("MdMermaidPreview");
        context?.BindTheme(preview, Border.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty);

        var border = new Border { Child = preview, MinHeight = 180 };
        border.Classes.Add("MdMermaid");

        CancellationTokenSource? cts = null;
        ScrollViewer? sizeHost = null;
        Application? themeHost = null;
        var attached = false;

        void CancelRender()
        {
            var previous = cts; cts = null;
            previous?.Cancel(); previous?.Dispose();
        }

        // 主题切换时 SVG 里的颜色是烘焙进文本的，必须重渲。
        void OnThemeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!ThemeChangeProperty.IsMatch(e.Property.Name))
            {
                return;
            }

            _ = ApplyThemeAsync();
        }

        // ScrollViewer 会给子项传递无穷可用宽度：钳制 MaxWidth 到视口宽度并随尺寸变化更新。
        // 订阅属于整个图表块；虚拟化会反复挂载，不能在每次挂载时追加匿名清理回调。
        border.AttachedToVisualTree += (_, _) =>
        {
            if (attached) return;
            attached = true;
            themeHost = Application.Current;
            if (themeHost is not null) themeHost.PropertyChanged += OnThemeChanged;
            sizeHost = border.FindAncestorOfType<ScrollViewer>();
            if (sizeHost is not null) sizeHost.SizeChanged += OnSizeChanged;
            UpdateWidth(); _ = ApplyThemeAsync();
        };
        border.DetachedFromVisualTree += (_, _) =>
        {
            attached = false;
            if (sizeHost is not null) sizeHost.SizeChanged -= OnSizeChanged;
            if (themeHost is not null) themeHost.PropertyChanged -= OnThemeChanged;
            sizeHost = null; themeHost = null; CancelRender();
        };
        void UpdateWidth()
        {
            // 只钳上限：image 是 DownOnly，窄图仍按自身尺寸居中，宽图则能用满整个视口。
            // 这里曾写死 800，宿主阅读宽大于 800 时（如 1100 栏宽）C4/时序图会被无故压小一档。
            if (sizeHost?.Viewport.Width is > 0 and var width) image.MaxWidth = width;
        }
        void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateWidth();

        // Mermaid 解析渲染与 SkiaSharp SVG 加载都是 CPU 密集操作，放后台线程；
        // CTS 用于主题快速切换时取消在途渲染。
        async Task ApplyThemeAsync()
        {
            if (!attached) return;
            CancelRender();
            var localCts = cts = new CancellationTokenSource();
            var token = localCts.Token;

            var options = GetRenderOptions(source);
            var cacheKey = CreateCacheKey(source, options);

            try
            {
                var svgSource = await ResolveSvgSourceAsync(cacheKey, source, options).WaitAsync(token);

                if (!token.IsCancellationRequested)
                {
                    if (svgSource is null)
                    {
                        // 上次渲染已失败且结果被缓存：本次直接回落，不重复尝试。
                        ShowFallback("Mermaid render failed.");
                        return;
                    }

                    image.Source = new SvgImage { Source = svgSource };
                    border.MinHeight = 0;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                {
                    ShowFallback(exception.Message);
                }
            }

            void ShowFallback(string message)
            {
                var panel = new StackPanel { Spacing = 4 };
                panel.Children.Add(new TextBlock { Text = $"Mermaid render error: {message}" });
                panel.Children.Add(new TextBlock { Text = source, TextWrapping = TextWrapping.Wrap });
                border.Child = panel;
                border.MinHeight = 0;
                border.Classes.Clear();
                border.Classes.Add("MdMermaidFallback");
            }
        }

        return border;
    }

    internal static Task<SvgSource?> ResolveSvgSourceAsync(string cacheKey, string source, MermaidRenderOptions options)
    {
        lock (DiagramCacheGate)
        {
            if (DiagramCache.TryGetValue(cacheKey, out var cached))
            {
                DiagramCacheOrder.Remove(cacheKey);
                DiagramCacheOrder.AddLast(cacheKey);
                return Task.FromResult(cached);
            }
            if (PendingDiagrams.TryGetValue(cacheKey, out var pending)) return pending;
            var task = Task.Run(async () =>
            {
                await RenderSlots.WaitAsync().ConfigureAwait(false);
                try { return ResolveSvgSource(cacheKey, source, options); }
                finally
                {
                    RenderSlots.Release();
                    lock (DiagramCacheGate) PendingDiagrams.Remove(cacheKey);
                }
            });
            PendingDiagrams.Add(cacheKey, task);
            return task;
        }
    }

    /// <summary>
    /// 取图表 SVG：按「源码 + 主题配色」缓存，块被虚拟化回收后重新物化时
    /// 直接复用已解析的 SVG，不再跑一遍布局与 CSS 变量内联。
    /// 渲染失败以 null 缓存，避免每次重建都重试同一份坏图。
    /// </summary>
    private static SvgSource? ResolveSvgSource(string cacheKey, string source, MermaidRenderOptions options)
    {
        lock (DiagramCacheGate)
        {
            if (DiagramCache.TryGetValue(cacheKey, out var cached))
            {
                DiagramCacheOrder.Remove(cacheKey);
                DiagramCacheOrder.AddLast(cacheKey);
                return cached;
            }
        }

        SvgSource? svgSource = null;
        try
        {
            var svg = MermaidRenderer.RenderSvg(source, options);
            // 象限图统一化必须先于 CSS 变量内联：内联会把 var(--_group-fill) 等目标串改写为具体色值，
            // 统一化的精确匹配将全部落空（12.1.6.1 回归根因）。
            if (options.Style == DiagramStyle.Quiet && Regex.IsMatch(source, @"^\s*quadrantChart\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                svg = NormalizeQuadrantCells(svg);
            svg = InlineCssVariables(svg, options);
            if (Regex.IsMatch(source, @"^\s*sankey(?:-beta)?\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                svg = FitSankeyLabels(svg);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
            svgSource = SvgSource.LoadFromStream(stream);
        }
        catch
        {
            svgSource = null;
        }

        lock (DiagramCacheGate)
        {
            DiagramCache[cacheKey] = svgSource;
            DiagramCacheOrder.Remove(cacheKey);
            DiagramCacheOrder.AddLast(cacheKey);
            while (DiagramCacheOrder.Count > MaxCachedDiagrams && DiagramCacheOrder.First is { } oldest)
            {
                DiagramCacheOrder.RemoveFirst();
                DiagramCache.Remove(oldest.Value);
            }
        }

        return svgSource;
    }

    /// <summary>桑基图画布未计入左右外置标签，扩展 SVG 视口后再等比缩放。</summary>
    internal static string FitSankeyLabels(string svg)
    {
        var document = XDocument.Parse(svg);
        var root = document.Root!;
        var viewBox = root.Attribute("viewBox")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        var left = viewBox[0];
        var right = left + viewBox[2];
        using var typeface = SKTypeface.FromFamilyName("Inter");
        foreach (var text in root.Descendants().Where(element => element.Name.LocalName == "text"))
        {
            if (!double.TryParse(text.Attribute("x")?.Value, CultureInfo.InvariantCulture, out var x)) continue;
            var size = float.TryParse(text.Attribute("font-size")?.Value, CultureInfo.InvariantCulture, out var parsedSize) ? parsedSize : 14;
            using var font = new SKFont(typeface, size);
            // 非拉丁文字留足全角字宽，避免缺字回退字体的量测差异再次裁掉文字。
            var fallbackWidth = text.Value.EnumerateRunes().Sum(rune => Rune.GetUnicodeCategory(rune) is
                UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark ? 0 : rune.IsAscii ? size * .75 : size);
            var width = Math.Max(font.MeasureText(text.Value), fallbackWidth);
            var anchor = text.Attribute("text-anchor")?.Value;
            var start = anchor == "end" ? x - width : anchor == "middle" ? x - width / 2 : x;
            left = Math.Min(left, start - 8);
            right = Math.Max(right, start + width + 8);
        }
        root.SetAttributeValue("viewBox", string.Join(" ", new[] { left, viewBox[1], right - left, viewBox[3] }
            .Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))));
        root.SetAttributeValue("width", (right - left).ToString("0.###", CultureInfo.InvariantCulture));
        return document.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>缓存 Key 含主题配色：同一份图表在明暗主题下是两张不同的图。</summary>
    internal static string CreateCacheKey(string source, MermaidRenderOptions options) =>
        $"{options.Bg}|{options.Fg}|{options.Accent}|{options.Line}|{options.Border}|{options.Surface}|{options.Muted}|{options.Default}|{string.Join(',', options.DataPalette ?? [])}|{options.FontSize}|{options.NodeSpacing}|{options.LayerSpacing}|{options.RoundedEdges}|{options.Style}|{options.Gradient}|{options.Tint}|{source}";

    /// <summary>
    /// 按当前主题变体构造渲染选项；颜色来自 CodeWFMarkdownMermaid* 画刷资源
    /// （排版主题可覆盖），字面量仅作资源缺失时的兜底。
    /// </summary>
    internal static MermaidRenderOptions GetRenderOptions(string? source = null)
    {
        var app = Application.Current;
        var theme = app?.ActualThemeVariant ?? ThemeVariant.Light;
        var isDark = IsDarkTheme(theme);

        return new MermaidRenderOptions
        {
            Bg = ResolveHex(app, theme, "CodeWFMarkdownMermaidBackground", isDark ? "#18181B" : "#FFFFFF"),
            Fg = ResolveHex(app, theme, "CodeWFMarkdownMermaidForeground", isDark ? "#FAFAFA" : "#27272A"),
            Accent = ResolveHex(app, theme, "CodeWFMarkdownMermaidAccent", isDark ? "#60A5FA" : "#3B82F6"),
            Line = ResolveOptionalHex(app, theme, "CodeWFMarkdownMermaidLine"),
            Border = ResolveOptionalHex(app, theme, "CodeWFMarkdownMermaidBorder"),
            Surface = ResolveOptionalHex(app, theme, "CodeWFMarkdownMermaidSurface"),
            Muted = ResolveOptionalHex(app, theme, "CodeWFMarkdownMermaidMuted"),
            Default = ResolveOptionalHex(app, theme, "CodeWFMarkdownMermaidDefault"),
            DataPalette = ResolveDataPalette(app, theme, source),
            // 渲染密度联动（规则 §11）：宿主可经 CodeWFMarkdownMermaidFontSize / NodeSpacing /
            // LayerSpacing / RoundedEdges 资源把正文字号档传导给图表（缺省用 Mermaider 内置值）。
            FontSize = ResolveOptionalDouble(app, theme, "CodeWFMarkdownMermaidFontSize") is { } fontSize
                ? fontSize.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "px" : null,
            NodeSpacing = ResolveOptionalDouble(app, theme, "CodeWFMarkdownMermaidNodeSpacing"),
            LayerSpacing = ResolveOptionalDouble(app, theme, "CodeWFMarkdownMermaidLayerSpacing"),
            RoundedEdges = ResolveOptionalBool(app, theme, "CodeWFMarkdownMermaidRoundedEdges") ?? true,
            Transparent = true,
            // 象限图渲染后经 NormalizeQuadrantCells 统一为四格同色（原型口径：浅 accent 底、清晰描边、直角）；
            // 全局 Quiet 不再对 quadrantChart 特判 Tonal，数据调色板也不再喂给它（filled 集合已剔除）。
            Style = DiagramStyle.Quiet
        };
    }

    private static string[]? ResolveDataPalette(Application? app, ThemeVariant theme, string? source)
    {
        var filled = source is not null && Regex.IsMatch(source, @"^\s*(?:pie|journey|timeline|treemap)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        foreach (var key in filled ? new[] { "CodeWFMarkdownMermaidFillPalette", "CodeWFMarkdownMermaidDataPalette" } : new[] { "CodeWFMarkdownMermaidDataPalette" })
            if (app?.TryGetResource(key, theme, out var palette) == true && palette is string colors
                && colors.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } values) return values;
        return null;
    }

    private static string? ResolveOptionalHex(Application? app, ThemeVariant theme, string key) =>
        app?.TryGetResource(key, theme, out var resource) == true && resource is ISolidColorBrush
            ? ResolveHex(app, theme, key, "#000000") : null;

    internal static bool IsDarkTheme(ThemeVariant variant)
    {
        for (ThemeVariant? current = variant; current is not null; current = current.InheritVariant)
            if (current == ThemeVariant.Dark) return true;
        return false;
    }

    private static string ResolveHex(Application? app, ThemeVariant theme, string resourceKey, string fallback)
    {
        if (app != null
            && app.TryGetResource(resourceKey, theme, out var resource)
            && resource is ISolidColorBrush brush)
        {
            var color = brush.Color;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return fallback;
    }

    /// <summary>可选数值资源（渲染密度联动）；缺失或类型不符返回 null，交由 Mermaider 内置值。</summary>
    private static double? ResolveOptionalDouble(Application? app, ThemeVariant theme, string resourceKey)
    {
        if (app?.TryGetResource(resourceKey, theme, out var resource) == true)
        {
            if (resource is double value) return value;
            if (resource is int integral) return integral;
        }
        return null;
    }

    private static bool? ResolveOptionalBool(Application? app, ThemeVariant theme, string resourceKey)
        => app?.TryGetResource(resourceKey, theme, out var resource) == true && resource is bool value
            ? value : null;

    private readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>
    /// 把 Mermaider 的 CSS 自定义属性引用展开为字面量色值。表中的公式镜像自 Mermaider 样式块里的
    /// color-mix 定义；命中表值即用它，不看 Mermaider 自带的回退，保证同一主题升级前后色值不变。
    /// <summary>
    ///     象限图四格统一化（原型口径）：Quiet 会给一格强调填充而其余为 group-fill（趋近底色，
    ///     观感近乎无填充），这里以「强调格」的浅 accent 底与 accent 描边为准，把其余三格统一为
    ///     同款 accent tint，并把圆角改直角——原型象限图为四格同色浅 primary 底、清晰 1px 描边、直角矩形。
    /// </summary>
    private static string NormalizeQuadrantCells(string svg)
    {
        var accentTintFill = "fill=\"color-mix(in srgb, var(--accent, var(--fg)) 6%, var(--bg))\"";
        var groupFill = "fill=\"var(--_group-fill)\"";
        var accentStroke = "stroke=\"color-mix(in srgb, var(--accent, var(--fg)) 42%, var(--bg))\"";
        var softStroke = "stroke=\"var(--_line-soft)\"";
        var rounded = " rx=\"10\" ry=\"10\"";
        var normalized = svg
            .Replace(groupFill, accentTintFill)
            .Replace(softStroke, accentStroke)
            .Replace("stroke-width=\"1.25\"", "stroke-width=\"1\"");
        if (!ReferenceEquals(normalized, svg))
        {
            // 仅象限单元矩形参与：rx="10" 只出现在格子上（点为圆形、标题无框），直接移除。
            normalized = normalized.Replace(rounded, string.Empty);
        }
        return normalized;
    }

    internal static string InlineCssVariables(string svg, MermaidRenderOptions options)
    {
        var bg = Parse(options.Bg ?? "#FFFFFF");
        var fg = Parse(options.Fg ?? "#27272A");
        var acc = Parse(options.Accent ?? "#3b82f6");

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--bg"] = Hex(bg),
            ["--fg"] = Hex(fg),
            ["--accent"] = Hex(acc),
            ["--line"] = options.Line ?? Hex(Mix(fg, 32, bg)),
            ["--border"] = options.Border ?? Hex(Mix(fg, 10, bg)),
            ["--surface"] = options.Surface ?? Hex(Mix(fg, 4, bg)),
            ["--muted"] = options.Muted ?? Hex(Mix(fg, 55, bg)),
            ["--fs-xs"] = "12",
            ["--fs-s"] = "14",
            ["--fs-m"] = "16",
            ["--fs-l"] = "18",
            ["--_text"] = Hex(fg),
            ["--_text-sec"] = Hex(Mix(fg, 55, bg)),
            ["--_text-muted"] = Hex(Mix(fg, 35, bg)),
            ["--_text-faint"] = Hex(Mix(fg, 20, bg)),
            ["--_line"] = Hex(Mix(fg, 32, bg)),
            ["--_line-soft"] = Hex(Mix(fg, 14, bg)),
            ["--_line-strong"] = Hex(Mix(fg, 68, bg)),
            ["--_arrow"] = Hex(acc),
            ["--_node-fill"] = Hex(Mix(fg, 4, bg)),
            ["--_node-stroke"] = Hex(Mix(fg, 22, bg)),
            ["--_group-fill"] = Hex(bg),
            ["--_group-hdr"] = Hex(Mix(fg, 4, bg)),
            ["--_group-stroke"] = Hex(Mix(fg, 10, bg)),
            ["--_inner-stroke"] = Hex(Mix(fg, 10, bg)),
            ["--_key-badge"] = Hex(Mix(fg, 8, bg)),
            ["--_accent-fill"] = Hex(Mix(acc, 8, bg)),
            ["--_accent-stroke"] = Hex(Mix(acc, 20, bg)),
            ["--_accent-text"] = Hex(Mix(acc, 65, bg))
        };

        // 象限图四格这类写法是 color-mix(in srgb, var(--accent, #FAFAFA) 8%, #18181B)：
        // 回退语法里的 var() 不先展开，任何按字面量匹配 color-mix 的做法都会漏掉它，Skia 随后画成黑色。
        return CssColorResolver.Flatten(svg, tokens);

        static Rgb Parse(string hex)
        {
            hex = hex.TrimStart('#');
            return new Rgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }

        // color-mix(in srgb, a N%, b) —— sRGB 空间线性插值
        static Rgb Mix(Rgb a, double aPercent, Rgb b)
        {
            double bPercent = 100 - aPercent;
            return new Rgb(
                (byte)(a.R * aPercent / 100 + b.R * bPercent / 100),
                (byte)(a.G * aPercent / 100 + b.G * bPercent / 100),
                (byte)(a.B * aPercent / 100 + b.B * bPercent / 100));
        }

        static string Hex(Rgb color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string ExtractSource(FencedCodeBlock block)
    {
        if (block.Lines.Lines is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var line in block.Lines.Lines)
        {
            builder.AppendLine(line.ToString().TrimEnd());
        }

        return builder.ToString();
    }
}
