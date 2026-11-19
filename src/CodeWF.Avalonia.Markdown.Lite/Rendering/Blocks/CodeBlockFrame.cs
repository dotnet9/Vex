using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Layout;
using global::Avalonia.LogicalTree;
using global::Avalonia.Markup.Xaml.MarkupExtensions;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.Lang;
using Markdig.Syntax;
using Path = global::Avalonia.Controls.Shapes.Path;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 代码块在流式追加时只增长尾部的更新入口：实现者保留自身控件实例，
/// 由 Viewer 在 append-only 快照里原地刷新而不重建整块。
/// </summary>
internal interface IStreamingCodeBlock
{
    bool TryUpdateStreaming(CodeBlock codeBlock, IMarkdownRenderContext context);
}

/// <summary>
/// 代码块外框：左侧标题（语言 · 路径#行段），右侧工具条（折叠/换行/复制/下载）。
/// 折叠与换行按块键保存到 Viewer，流式追加时只替换代码正文，滚动位置与块内选区保留。
/// </summary>
internal sealed class CodeBlockFrame : Border, IStreamingCodeBlock
{
    /// <summary>行数达到该阈值的代码块才显示折叠按钮（短代码折叠无收益）。</summary>
    internal const int FoldableLineThreshold = 8;

    protected override Type StyleKeyOverride => typeof(Border);

    private readonly int _blockKey;
    private readonly IMarkdownRenderContext _context;
    private readonly StackPanel _header;
    private readonly StackPanel _toolbar;
    private readonly StackPanel _content;
    private readonly StackPanel _footer;
    private readonly SelectableTextBlock _title;
    private readonly ScrollViewer _scroll;
    private readonly Button _copyButton;
    private readonly Button _wrapButton;
    private readonly Button _downloadButton;
    private readonly Button _expandButton;
    private readonly Path _copyIcon;
    private readonly Path _wrapIcon;
    private Button? _foldButton;

    private Control _body;
    private MarkdownCodeFenceInfo _fence;
    private string _code;
    private string _highlightLanguage;
    private string _titleLanguage;
    private int _lineCount;
    private bool _isOpen;
    private bool _folded;
    private bool _wrap;
    private int _copyGeneration;

    public CodeBlockFrame(int blockKey, CodeBlock codeBlock, IMarkdownRenderContext context)
    {
        _blockKey = blockKey;
        _context = context;
        _fence = MarkdownCodeFenceInfo.From(codeBlock);
        _code = codeBlock.Lines.ToString();
        _highlightLanguage = (codeBlock as FencedCodeBlock)?.Info is { Length: > 0 } info ? info : "text";
        _titleLanguage = _fence.ToTitle();
        _lineCount = CountLines(codeBlock);
        _isOpen = codeBlock.IsOpen;
        // 流式输出中（围栏未闭合）不允许折叠：用户正看着的代码不该被收起。
        _folded = !_isOpen && context.IsCodeBlockFolded(blockKey);
        _wrap = context.IsCodeBlockWrapped(blockKey);

        context.AddMarkdownClass(this, MarkdownStyleKeys.CodeBlock);
        context.BindTheme(this, Border.BackgroundProperty, MarkdownViewer.CodeBackgroundBrushProperty);
        context.BindTheme(this, Border.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty);

        _title = context.CreateSelectableText(MarkdownStyleKeys.CodeLanguage);
        _title.Text = _titleLanguage;
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        context.BindTheme(_title, SelectableTextBlock.ForegroundProperty, MarkdownViewer.MutedTextBrushProperty);
        context.BindTheme(_title, SelectableTextBlock.FontSizeProperty, MarkdownViewer.CodeLanguageFontSizeProperty);

        _toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        context.AddMarkdownClass(_toolbar, MarkdownStyleKeys.CodeBlockToolbar);

        _wrapIcon = CreateIcon(MarkdownCodeIcons.Wrap);
        _wrapButton = CreateToolButton("CodeWrap", MarkdownLocalization.GetResource(MarkdownL.CodeWrapLines));
        _wrapButton.Content = _wrapIcon;
        _wrapButton.Click += (_, _) => ToggleWrap();

        _downloadButton = CreateToolButton("CodeDownload", MarkdownLocalization.GetResource(MarkdownL.CodeDownload));
        _downloadButton.Content = CreateIcon(MarkdownCodeIcons.Download);
        _downloadButton.Click += async (_, _) =>
            await _context.SaveCodeBlockAsync(_code, _fence.Language ?? _highlightLanguage, _fence.Path);

        _copyIcon = CreateIcon(MarkdownCodeIcons.Copy);
        _copyButton = CreateToolButton("CodeCopy", MarkdownLocalization.GetResource(MarkdownL.Copy));
        _copyButton.Content = _copyIcon;
        _copyButton.Click += (_, _) =>
        {
            _context.CopyCodeToClipboard(_code);
            ShowCopyFeedback();
        };

        _toolbar.Children.Add(_downloadButton);
        _toolbar.Children.Add(_wrapButton);
        _toolbar.Children.Add(_copyButton);
        EnsureFoldButton();
        UpdateLocalizedChrome();

        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        headerGrid.Children.Add(_title);
        Grid.SetColumn(_toolbar, 1);
        headerGrid.Children.Add(_toolbar);

        _header = new StackPanel { Orientation = Orientation.Vertical };
        context.AddMarkdownClass(_header, MarkdownStyleKeys.CodeBlockHeader);
        context.BindTheme(_header, Control.IsVisibleProperty, MarkdownViewer.ShowCodeBlockHeaderProperty);
        _header.Children.Add(headerGrid);

        _content = new StackPanel { Orientation = Orientation.Vertical, IsVisible = !_folded };
        context.AddMarkdownClass(_content, MarkdownStyleKeys.CodeBlockContent);

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        context.AddMarkdownClass(_scroll, MarkdownStyleKeys.CodeBlockScrollViewer);
        _body = CreateBody(_code);
        _scroll.Content = _body;
        _scroll.ScrollChanged += (_, _) => UpdateScrollbarReserve();
        _content.Children.Add(_scroll);

        _expandButton = new Button
        {
            Tag = "CodeExpand",
            Content = ExpandAllText(_lineCount),
            MinHeight = 0,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        context.AddMarkdownClass(_expandButton, MarkdownStyleKeys.CodeExpandButton);
        context.BindTheme(_expandButton, Button.ForegroundProperty, MarkdownViewer.MutedTextBrushProperty);
        context.BindTheme(_expandButton, Button.FontSizeProperty, MarkdownViewer.CodeLanguageFontSizeProperty);
        _expandButton.Click += (_, _) => SetFolded(false, persist: true);

        _footer = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = _folded };
        context.AddMarkdownClass(_footer, MarkdownStyleKeys.CodeBlockFooter);
        _footer.Children.Add(_expandButton);

        var root = new StackPanel { Orientation = Orientation.Vertical };
        root.Children.Add(_header);
        root.Children.Add(_content);
        root.Children.Add(_footer);
        Child = root;
        ApplyWrapState();
        context.RaiseCodeBlockToolRender(_header, _content, codeBlock);
    }

    public bool TryUpdateStreaming(CodeBlock codeBlock, IMarkdownRenderContext context)
    {
        var code = codeBlock.Lines.ToString();
        if (!code.StartsWith(_code, StringComparison.Ordinal))
        {
            return false;
        }

        if (code.Length == _code.Length && code == _code)
        {
            return true;
        }

        _code = code;
        _fence = MarkdownCodeFenceInfo.From(codeBlock);
        _highlightLanguage = (codeBlock as FencedCodeBlock)?.Info is { Length: > 0 } info ? info : "text";
        _titleLanguage = _fence.ToTitle();
        _lineCount = CountLines(codeBlock);
        var wasOpen = _isOpen;
        _isOpen = codeBlock.IsOpen;

        _title.Text = _titleLanguage;
        _expandButton.Content = ExpandAllText(_lineCount);
        EnsureFoldButton();
        // 闭合那一刻补上折叠：流式期间不提供折叠，但状态可能已被 API 设置过。
        if (wasOpen && !_isOpen)
        {
            SetFolded(context.IsCodeBlockFolded(_blockKey), persist: false);
        }
        UpdateLocalizedChrome();

        var selection = CaptureSelection();
        // 分块正文（长代码）可原地增长：只重建尾部变化的块，前面的排版结果不重算。
        if (_body is IMarkdownCodeBodyUpdate updatable && updatable.TryUpdate(_code))
        {
            ApplyWrapState();
            RestoreSelection(selection);
            return true;
        }

        _body = CreateBody(_code);
        _scroll.Content = _body;
        ApplyWrapState();
        RestoreSelection(selection);
        return true;
    }

    private Control CreateBody(string code)
    {
        var view = _context.CodeHighlighter?.Invoke(
            code,
            _highlightLanguage,
            _context.CodeBlockIsDark,
            _context.CodeFontFamily,
            _context.CodeBlockFontSize,
            _context.CodeBlockLineHeight,
            _context.GetCodeHighlightSession(_blockKey),
            () => _context.HasSelection,
            _context.CopySelectionAsync);

        if (view is null)
        {
            return CreatePlainBody(code);
        }

        // 正文滚动由外框统一持有：保留同一个 ScrollViewer 才能在流式追加时守住横向偏移。
        return view is ScrollViewer { Content: Control inner } ? inner : view;
    }

    private Control CreatePlainBody(string code)
    {
        if (PlainCodeChunkBody.SplitLines(code).Length > MarkdownCodeChunking.ThresholdLines)
        {
            return new PlainCodeChunkBody(_context, code);
        }

        var textBlock = _context.CreateSelectableText("MdCodeBlockPlain");
        textBlock.FontFamily = _context.CodeFontFamily;
        textBlock.FontSize = _context.CodeBlockFontSize;
        textBlock.LineHeight = _context.CodeBlockLineHeight;
        textBlock.TextWrapping = TextWrapping.NoWrap;
        textBlock[!SelectableTextBlock.ForegroundProperty] =
            new DynamicResourceExtension(MarkdownStyleKeys.TextBrushResource);

        var lines = code.TrimEnd().Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            textBlock.Inlines?.Add(new Run(lines[i]));
            if (i < lines.Length - 1)
            {
                textBlock.Inlines?.Add(new LineBreak());
            }
        }

        return textBlock;
    }

    private void ToggleWrap()
    {
        _wrap = !_wrap;
        ApplyWrapState();
        _context.PersistCodeBlockWrap(_blockKey, _wrap);
    }

    private void ApplyWrapState()
    {
        foreach (var text in EnumerateCodeTextBlocks())
        {
            if (IsLineNumbers(text))
            {
                text.IsVisible = !_wrap;
            }
            else
            {
                text.TextWrapping = _wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            }
        }

        _scroll.HorizontalScrollBarVisibility = _wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        Classes.Set("wrapped", _wrap);
        UpdateLocalizedChrome();
    }

    private void SetFolded(bool folded, bool persist)
    {
        if (_folded == folded)
        {
            return;
        }

        _folded = folded;
        _content.IsVisible = !folded;
        _footer.IsVisible = folded;
        UpdateFoldButtonContent();
        if (persist)
        {
            _context.PersistCodeBlockFolded(_blockKey, folded);
        }

        InvalidateMeasure();
    }

    private void EnsureFoldButton()
    {
        var collapsible = _context.EnableCodeBlockFolding
                          && !_isOpen
                          && _lineCount >= FoldableLineThreshold;
        if (collapsible && _foldButton is null)
        {
            _foldButton = CreateToolButton("CodeFold", _titleLanguage, mutedForeground: false);
            _foldButton.Width = 20;
            _foldButton.FontSize = 12;
            _context.BindTheme(_foldButton, Button.ForegroundProperty, MarkdownViewer.TextBrushProperty);
            _foldButton.Click += (_, _) => SetFolded(!_folded, persist: true);
            _toolbar.Children.Insert(0, _foldButton);
            UpdateFoldButtonContent();
        }
        else if (!collapsible && _foldButton is not null)
        {
            _toolbar.Children.Remove(_foldButton);
            _foldButton = null;
            if (_folded)
            {
                SetFolded(false, persist: false);
            }
        }

        if (collapsible)
        {
            UpdateFoldButtonContent();
        }
    }

    private void UpdateFoldButtonContent()
    {
        if (_foldButton is null)
        {
            return;
        }

        var glyph = _folded ? "▸" : "▾";
        _foldButton.Content = glyph;
        AutomationProperties.SetName(_foldButton, $"{_titleLanguage} {glyph}");
    }

    private void UpdateLocalizedChrome()
    {
        // 多代码块场景下读屏器需要可区分的按钮名称："语言 操作"。
        AutomationProperties.SetName(_copyButton,
            $"{_titleLanguage} {MarkdownLocalization.GetResource(MarkdownL.Copy)}");
        AutomationProperties.SetName(_wrapButton,
            $"{_titleLanguage} {MarkdownLocalization.GetResource(_wrap ? MarkdownL.CodeUnwrapLines : MarkdownL.CodeWrapLines)}");
        AutomationProperties.SetName(_downloadButton,
            $"{_titleLanguage} {MarkdownLocalization.GetResource(MarkdownL.CodeDownload)}");
        // 工具条是图标钮，操作文案只在 ToolTip 上；语言切换时框架可能按块身份原样复用，
        // 这里统一重写，悬停提示才与读屏名字保持同文化。
        ToolTip.SetTip(_copyButton, MarkdownLocalization.GetResource(MarkdownL.Copy));
        ToolTip.SetTip(_wrapButton,
            MarkdownLocalization.GetResource(_wrap ? MarkdownL.CodeUnwrapLines : MarkdownL.CodeWrapLines));
        ToolTip.SetTip(_downloadButton, MarkdownLocalization.GetResource(MarkdownL.CodeDownload));
    }

    private Button CreateToolButton(string tag, string label, bool mutedForeground = true)
    {
        var button = new Button
        {
            Tag = tag,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            Width = 24,
            Height = 24,
            MinWidth = 0,
            MinHeight = 0,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _context.AddMarkdownClass(button, MarkdownStyleKeys.CodeBlockToolButton);
        if (mutedForeground)
        {
            _context.BindTheme(button, Button.ForegroundProperty, MarkdownViewer.MutedTextBrushProperty);
        }

        ToolTip.SetTip(button, label);
        AutomationProperties.SetName(button, label);
        return button;
    }

    private Path CreateIcon(Geometry geometry)
    {
        var path = new Path
        {
            Data = geometry,
            Width = 16,
            Height = 16,
            Stretch = Stretch.None,
            StrokeThickness = 1.4,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        };
        _context.BindTheme(path, Shape.StrokeProperty, MarkdownViewer.MutedTextBrushProperty);
        return path;
    }

    private void ShowCopyFeedback()
    {
        var generation = ++_copyGeneration;
        _copyIcon.Data = MarkdownCodeIcons.Check;
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _copyGeneration)
            {
                _copyIcon.Data = MarkdownCodeIcons.Copy;
            }
        }, TimeSpan.FromSeconds(1.5));
    }

    private void UpdateScrollbarReserve()
    {
        var height = _scroll.Extent.Width > _scroll.Viewport.Width + .5
            ? Math.Max(16, _scroll.GetVisualDescendants().OfType<ScrollBar>()
                .FirstOrDefault(bar => bar.Orientation == Orientation.Horizontal)?.Bounds.Height ?? 0)
            : 0;
        if (_scroll.Content is not Control content)
        {
            return;
        }

        var margin = content.Margin;
        if (Math.Abs(margin.Bottom - height) > .5)
        {
            content.Margin = new Thickness(margin.Left, margin.Top, margin.Right, height);
        }
    }

    private IEnumerable<SelectableTextBlock> EnumerateCodeTextBlocks()
    {
        if (_body is SelectableTextBlock self)
        {
            yield return self;
        }

        foreach (var text in _body.GetLogicalDescendants().OfType<SelectableTextBlock>())
        {
            yield return text;
        }
    }

    private static bool IsLineNumbers(SelectableTextBlock text) => text.Tag as string == "CodeLineNumbers";

    private (int Start, int End) CaptureSelection()
    {
        // 分块正文按「整篇代码偏移」记录选区：块间换行计 1 个字符，追加后偏移不变。
        var offset = 0;
        foreach (var text in EnumerateCodeTextBlocks())
        {
            if (IsLineNumbers(text))
            {
                continue;
            }

            var length = CodeTextLength(text);
            var start = Math.Min(text.SelectionStart, text.SelectionEnd);
            var end = Math.Max(text.SelectionStart, text.SelectionEnd);
            if (end > start)
            {
                return (offset + start, offset + end);
            }

            offset += length + 1;
        }

        return (0, 0);
    }

    private void RestoreSelection((int Start, int End) selection)
    {
        if (selection.End <= selection.Start)
        {
            return;
        }

        var offset = 0;
        foreach (var text in EnumerateCodeTextBlocks())
        {
            if (IsLineNumbers(text))
            {
                continue;
            }

            var length = CodeTextLength(text);
            if (length > 0 && selection.Start <= offset + length)
            {
                text.SelectionStart = Math.Clamp(selection.Start - offset, 0, length);
                text.SelectionEnd = Math.Clamp(selection.End - offset, 0, length);
                return;
            }

            offset += length + 1;
        }
    }

    private static int CodeTextLength(SelectableTextBlock text) =>
        text.Text?.Length ?? text.Inlines?.Text?.Length ?? 0;

    private static int CountLines(CodeBlock codeBlock) => Math.Max(1, codeBlock.Lines.Count);

    private static string ExpandAllText(int lineCount) =>
        string.Format(MarkdownLocalization.GetResource(MarkdownL.CodeExpandAllLinesFormat), lineCount);
}
