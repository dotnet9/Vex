using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Media;
using global::Avalonia.Media.Immutable;
using global::Avalonia.Media.TextFormatting;
using global::Avalonia.Utilities;
using global::Avalonia.VisualTree;
using CodeWF.Toolkit.Lang;
using System.Globalization;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using AvaloniaFontStyle = global::Avalonia.Media.FontStyle;
using TextMateFontStyle = TextMateSharp.Themes.FontStyle;

namespace CodeWF.Avalonia.Markdown.Rendering;

internal static class CodeHighlighter
{
    private const int MaxCacheSize = 32;
    private static readonly Dictionary<(string Language, ThemeName Theme), (IGrammar? Grammar, Theme Theme)> Cache = new();
    private static readonly Queue<(string Language, ThemeName Theme)> CacheOrder = new();
    private static readonly Dictionary<HighlightCacheKey, HighlightedLine[]> HighlightCache = new();
    private static readonly Queue<HighlightCacheKey> HighlightCacheOrder = new();

    public static Control Render(
        string code,
        string language,
        bool isDark,
        FontFamily fontFamily,
        double fontSize,
        double lineHeight,
        MarkdownCodeHighlightSession? session,
        Func<bool>? hasGlobalSelection = null,
        Func<Task>? copyGlobalSelectionAsync = null)
    {
        var themeName = isDark ? ThemeName.DarkPlus : ThemeName.LightPlus;
        var (grammar, theme) = GetOrCreateGrammar(NormalizeLanguage(language), themeName);
        var lines = grammar is null
            ? null
            : GetHighlightedLines(code, language, themeName, grammar, theme, isDark, session);
        var normalized = code.Replace("\r\n", "\n");
        var lineCount = normalized.Count(character => character == '\n') + 1;

        // 长代码按行切块：每块独立排版，流式追加只重建尾部块，前面的排版结果不重算。
        // 词法状态经同一会话跨块延续，分块不改变配色。
        if (grammar is not null && lines is not null && lineCount > MarkdownCodeChunking.ThresholdLines)
        {
            var chunked = new ChunkedHighlightedCodeBody(
                normalized, lines, fontFamily, fontSize, lineHeight, isDark,
                text => GetHighlightedLines(text, language, themeName, grammar, theme, isDark, session),
                hasGlobalSelection, copyGlobalSelectionAsync);
            return Wrap(chunked, isDark, lineCount, fontFamily, fontSize, lineHeight);
        }

        // 长代码以文本样式区间排版，避免为每个 token 建立参与主题匹配的逻辑树节点。
        SelectableTextBlock textBlock = lines is { Length: >= 64 }
            ? new HighlightedCodeTextBlock(normalized, lines)
            : new SelectableTextBlock();
        ConfigureCodeTextBlock(textBlock, fontFamily, fontSize, lineHeight, hasGlobalSelection, copyGlobalSelectionAsync);
        if (textBlock is HighlightedCodeTextBlock) return Wrap(textBlock, isDark, lineCount, fontFamily, fontSize, lineHeight);
        // SelectableTextBlock 每次收到 Inlines 变化都会重新拼接全文来校正选区。
        // 先在未绑定的集合内完成构造，避免长代码的 token 数量带来平方级复制。
        var inlines = new InlineCollection();

        if (grammar is null)
        {
            inlines.Add(new Run(code) { Foreground = isDark ? Brushes.White : Brushes.Black });
            textBlock.Inlines = inlines;
            return Wrap(textBlock, isDark, lineCount, fontFamily, fontSize, lineHeight);
        }

        for (var index = 0; index < lines!.Length; index++)
        {
            if (index > 0) inlines.Add(new LineBreak());
            foreach (var token in lines[index].Tokens)
            {
                inlines.Add(token.CreateRun());
            }

        }

        textBlock.Inlines = inlines;
        return Wrap(textBlock, isDark, lineCount, fontFamily, fontSize, lineHeight);
    }

    private static SelectableTextBlock ConfigureCodeTextBlock(
        SelectableTextBlock textBlock,
        FontFamily fontFamily,
        double fontSize,
        double lineHeight,
        Func<bool>? hasGlobalSelection,
        Func<Task>? copyGlobalSelectionAsync)
    {
        textBlock.TextWrapping = TextWrapping.NoWrap;
        textBlock.FontFamily = fontFamily;
        textBlock.FontSize = fontSize;
        textBlock.LineHeight = lineHeight;
        // 选区底/前景不再本地写死：绑 Semi 的 TextBlockSelection* 资源（与宿主 SelectableTextBlock
        // 样式同源），宿主可按资源键覆盖；此前深色主题下固定蓝底白字。
        textBlock.Bind(SelectableTextBlock.SelectionBrushProperty,
            textBlock.GetResourceObservable("TextBlockSelectionBackground"));
        textBlock.Bind(SelectableTextBlock.SelectionForegroundBrushProperty,
            textBlock.GetResourceObservable("TextBlockSelectionForeground"));
        textBlock.Classes.Add(MarkdownStyleKeys.CodeBlockText);
        TextOptions.SetBaselinePixelAlignment(textBlock, BaselinePixelAlignment.Aligned);
        textBlock.ContextMenu = CreateCopyContextMenu(textBlock, hasGlobalSelection, copyGlobalSelectionAsync);
        return textBlock;
    }

    private static Control Wrap(
        Control content,
        bool isDark,
        int lineCount,
        FontFamily fontFamily,
        double fontSize,
        double lineHeight)
    {
        // 分块正文自带行号列（行号与代码列等高），这里只包滚动容器。
        if (content is not ChunkedHighlightedCodeBody)
        {
            var lineNumbers = CreateLineNumbers(lineCount, isDark, fontFamily, fontSize, lineHeight);
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                }
            };
            grid.Children.Add(lineNumbers);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
            content = grid;
        }

        var scrollViewer = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        scrollViewer.Classes.Add(MarkdownStyleKeys.CodeBlockScrollViewer);
        scrollViewer.ScrollChanged += (_, _) =>
        {
            // Semi 的横向滚动条覆盖内容；溢出时预留实际轨道高度，最后一行仍可见。
            var height = scrollViewer.Extent.Width > scrollViewer.Viewport.Width + .5
                ? Math.Max(16, scrollViewer.GetVisualDescendants().OfType<ScrollBar>()
                    .FirstOrDefault(bar => bar.Orientation == global::Avalonia.Layout.Orientation.Horizontal)?.Bounds.Height ?? 0)
                : 0;
            var margin = content.Margin;
            if (Math.Abs(margin.Bottom - height) > .5) content.Margin = new Thickness(margin.Left, margin.Top, margin.Right, height);
        };
        return scrollViewer;
    }

    private static SelectableTextBlock CreateLineNumbers(
        int lineCount,
        bool isDark,
        FontFamily fontFamily,
        double fontSize,
        double lineHeight)
    {
        return new SelectableTextBlock
        {
            Tag = "CodeLineNumbers",
            Text = string.Join(Environment.NewLine, Enumerable.Range(1, lineCount).Select(i => i.ToString())),
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = fontFamily,
            FontSize = fontSize,
            LineHeight = lineHeight,
            // 依据实际代码背景选择行号颜色，不随窗口明暗误判，也不额外衰减透明度。
            Foreground = new ImmutableSolidColorBrush(isDark ? Color.FromRgb(0xAD, 0xB7, 0xC5) : Color.FromRgb(0x52, 0x60, 0x75)),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 12, 0),
            IsHitTestVisible = false
        };
    }

    private static ContextMenu CreateCopyContextMenu(
        SelectableTextBlock textBlock,
        Func<bool>? hasGlobalSelection,
        Func<Task>? copyGlobalSelectionAsync)
    {
        var copySelectionItem = new MenuItem
        {
            Header = MarkdownLocalization.GetResource(MarkdownL.CopyRenderedText)
        };
        copySelectionItem.Click += async (_, _) =>
        {
            if (hasGlobalSelection?.Invoke() == true && copyGlobalSelectionAsync is not null)
            {
                await copyGlobalSelectionAsync();
            }
            else
            {
                textBlock.Copy();
            }
        };
        return new ContextMenu { ItemsSource = new[] { copySelectionItem } };
    }

    /// <summary>启动时预热常用语言在明暗两套主题下的语法装载，避免首个代码块渲染时才付语法解析成本。</summary>
    public static void Prewarm(params string[] languages)
    {
        foreach (var language in languages)
        {
            var normalized = NormalizeLanguage(language);
            _ = GetOrCreateGrammar(normalized, ThemeName.DarkPlus);
            _ = GetOrCreateGrammar(normalized, ThemeName.LightPlus);
        }
    }

    private static (IGrammar? Grammar, Theme Theme) GetOrCreateGrammar(string language, ThemeName themeName)
    {
        var key = (language, themeName);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= MaxCacheSize)
        {
            var oldest = CacheOrder.Dequeue();
            Cache.Remove(oldest);
        }

        var options = new RegistryOptions(themeName);
        var registry = new Registry(options);
        var theme = registry.GetTheme();
        var scope = options.GetScopeByLanguageId(language);
        var grammar = string.IsNullOrWhiteSpace(scope) ? null : registry.LoadGrammar(scope);
        grammar ??= registry.LoadGrammar(options.GetScopeByLanguageId("log"));

        var value = (grammar, theme);
        Cache[key] = value;
        CacheOrder.Enqueue(key);
        return value;
    }

    /// <summary>
    /// 选择高亮缓存策略：有会话时走行级增量（流式只重算增长行），否则走整码 LRU。
    /// 语言/主题变化时整会话重算——跨行状态与配色都随主题变。
    /// </summary>
    private static HighlightedLine[] GetHighlightedLines(
        string code,
        string language,
        ThemeName themeName,
        IGrammar grammar,
        Theme theme,
        bool isDark,
        MarkdownCodeHighlightSession? session)
    {
        if (session is null)
        {
            return GetOrCreateHighlightedLines(code, NormalizeLanguage(language), themeName, grammar, theme, isDark);
        }

        var tag = (language, themeName);
        if (!Equals(session.Tag, tag))
        {
            session.Reset();
            session.Tag = tag;
        }

        var updated = session.Update(code, (string line, object? startState, out object? endState) =>
        {
            var tokens = TokenizeSingle(
                line, grammar, theme, isDark, startState as IStateStack, out var nextState);
            endState = nextState;
            return tokens;
        });
        return updated.Cast<HighlightedLine>().ToArray();
    }

    private static HighlightedLine[] GetOrCreateHighlightedLines(
        string code,
        string language,
        ThemeName themeName,
        IGrammar grammar,
        Theme theme,
        bool isDark)
    {
        var key = new HighlightCacheKey(code, language, themeName);
        if (HighlightCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (HighlightCache.Count >= MaxCacheSize)
        {
            var oldest = HighlightCacheOrder.Dequeue();
            HighlightCache.Remove(oldest);
        }

        var highlightedLines = Tokenize(code, grammar, theme, isDark);
        HighlightCache[key] = highlightedLines;
        HighlightCacheOrder.Enqueue(key);
        return highlightedLines;
    }

    private static HighlightedLine[] Tokenize(string code, IGrammar grammar, Theme theme, bool isDark)
    {
        var lines = new List<HighlightedLine>();
        IStateStack? ruleStack = null;
        foreach (var line in code.Replace("\r\n", "\n").Split('\n'))
        {
            lines.Add(TokenizeSingle(line, grammar, theme, isDark, ruleStack, out ruleStack));
        }

        return lines.ToArray();
    }

    private static HighlightedLine TokenizeSingle(
        string line,
        IGrammar grammar,
        Theme theme,
        bool isDark,
        IStateStack? startState,
        out IStateStack? endState)
    {
        var result = grammar.TokenizeLine(line, startState, TimeSpan.FromSeconds(2));
        endState = result.RuleStack;
        var tokens = new List<HighlightedToken>();

        foreach (var token in result.Tokens)
        {
            var start = Math.Min(token.StartIndex, line.Length);
            var end = Math.Min(token.EndIndex, line.Length);
            if (end <= start)
            {
                continue;
            }

            tokens.Add(CreateHighlightedToken(line[start..end], token.Scopes, theme, isDark));
        }

        return new HighlightedLine(tokens);
    }

    private static Run CreateRun(string text, IEnumerable<string> scopes, Theme theme, bool isDark)
    {
        return CreateHighlightedToken(text, scopes, theme, isDark).CreateRun();
    }

    private static HighlightedToken CreateHighlightedToken(string text, IEnumerable<string> scopes, Theme theme, bool isDark)
    {
        var foregroundId = -1;
        var backgroundId = -1;
        var fontStyle = TextMateFontStyle.None;

        foreach (var rule in theme.Match(scopes.ToList()))
        {
            if (foregroundId == -1 && rule.foreground > 0)
            {
                foregroundId = rule.foreground;
            }

            if (backgroundId == -1 && rule.background > 0)
            {
                backgroundId = rule.background;
            }

            if (rule.fontStyle > 0)
            {
                fontStyle |= (TextMateFontStyle)rule.fontStyle;
            }
        }

        return new HighlightedToken(
            text,
            foregroundId == -1
                ? (isDark ? Brushes.White : Brushes.Black)
                : new ImmutableSolidColorBrush(ParseColor(theme.GetColor(foregroundId))),
            backgroundId == -1
                ? null
                : new ImmutableSolidColorBrush(ParseColor(theme.GetColor(backgroundId))),
            (fontStyle & TextMateFontStyle.Bold) != 0 ? FontWeight.Bold : FontWeight.Normal,
            (fontStyle & TextMateFontStyle.Italic) != 0 ? AvaloniaFontStyle.Italic : AvaloniaFontStyle.Normal);
    }

    // 别名表移植自 StreamMark（MIT）LanguageAliases：围栏语言名/常见别名 → TextMateSharp.Grammars 的 language id。
    // 表里没有的名字按 language id 原样解析，仍查不到由 GetOrCreateGrammar 回退 log 语法。
    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.Ordinal)
    {
        // C#
        ["cs"] = "csharp", ["csharp"] = "csharp", ["c#"] = "csharp", ["c-sharp"] = "csharp", ["csx"] = "csharp", ["cake"] = "csharp",
        // JavaScript / TypeScript
        ["js"] = "javascript", ["javascript"] = "javascript", ["mjs"] = "javascript", ["cjs"] = "javascript", ["node"] = "javascript",
        ["ecmascript"] = "javascript", ["es6"] = "javascript",
        ["jsx"] = "javascriptreact", ["javascriptreact"] = "javascriptreact",
        ["ts"] = "typescript", ["typescript"] = "typescript", ["mts"] = "typescript", ["cts"] = "typescript",
        ["tsx"] = "typescriptreact", ["typescriptreact"] = "typescriptreact",
        // Python
        ["py"] = "python", ["python"] = "python", ["python3"] = "python", ["py3"] = "python", ["pyw"] = "python", ["gyp"] = "python",
        // Shell
        ["sh"] = "shellscript", ["bash"] = "shellscript", ["shell"] = "shellscript", ["zsh"] = "shellscript", ["ksh"] = "shellscript",
        ["console"] = "shellscript", ["shellscript"] = "shellscript", ["shell-session"] = "shellscript", ["shellsession"] = "shellscript",
        ["terminal"] = "shellscript",
        ["ps1"] = "powershell", ["powershell"] = "powershell", ["pwsh"] = "powershell", ["ps"] = "powershell", ["psm1"] = "powershell",
        ["psd1"] = "powershell",
        ["bat"] = "bat", ["cmd"] = "bat", ["batch"] = "bat", ["batchfile"] = "bat",
        // 数据 / 配置
        ["json"] = "json", ["json5"] = "jsonc", ["jsonc"] = "jsonc", ["jsonl"] = "json", ["ndjson"] = "json", ["geojson"] = "json",
        ["yaml"] = "yaml", ["yml"] = "yaml",
        ["toml"] = "ini", // TextMateSharp.Grammars 没有 TOML 语法，INI 语法对 TOML 的键值/节/注释足够接近
        ["ini"] = "ini", ["cfg"] = "properties", ["conf"] = "properties", ["properties"] = "properties", ["editorconfig"] = "ini",
        ["gitconfig"] = "properties",
        ["xml"] = "xml", ["svg"] = "xml", ["xaml"] = "xml", ["axaml"] = "xml", ["xsd"] = "xml", ["plist"] = "xml", ["csproj"] = "xml",
        ["fsproj"] = "xml", ["vbproj"] = "xml", ["props"] = "xml", ["targets"] = "xml", ["msbuild"] = "xml", ["nuspec"] = "xml",
        ["resx"] = "xml", ["rss"] = "xml", ["atom"] = "xml", ["wsdl"] = "xml", ["manifest"] = "xml",
        ["xsl"] = "xsl", ["xslt"] = "xsl",
        ["dockerfile"] = "dockerfile", ["docker"] = "dockerfile", ["containerfile"] = "dockerfile",
        ["makefile"] = "makefile", ["make"] = "makefile", ["mk"] = "makefile", ["mak"] = "makefile",
        ["diff"] = "diff", ["patch"] = "diff", ["udiff"] = "diff",
        ["gitignore"] = "ignore", ["ignore"] = "ignore", ["dockerignore"] = "ignore",
        ["log"] = "log",
        // 标记 / 文档
        ["md"] = "markdown", ["markdown"] = "markdown", ["mkd"] = "markdown", ["mdown"] = "markdown",
        ["html"] = "html", ["htm"] = "html", ["xhtml"] = "html", ["shtml"] = "html", ["vue"] = "html", ["svelte"] = "html",
        ["css"] = "css", ["scss"] = "scss", ["less"] = "less",
        ["tex"] = "latex", ["latex"] = "latex", ["bibtex"] = "bibtex", ["bib"] = "bibtex",
        ["adoc"] = "asciidoc", ["asciidoc"] = "asciidoc",
        ["typ"] = "typst", ["typst"] = "typst",
        ["razor"] = "razor", ["cshtml"] = "razor",
        ["hbs"] = "handlebars", ["handlebars"] = "handlebars",
        ["pug"] = "jade", ["jade"] = "jade",
        // 系统语言
        ["rs"] = "rust", ["rust"] = "rust",
        ["go"] = "go", ["golang"] = "go",
        ["java"] = "java",
        ["swift"] = "swift",
        ["cpp"] = "cpp", ["c++"] = "cpp", ["cc"] = "cpp", ["cxx"] = "cpp", ["hpp"] = "cpp", ["hh"] = "cpp", ["hxx"] = "cpp",
        ["h++"] = "cpp", ["ino"] = "cpp", ["arduino"] = "cpp",
        ["c"] = "c", ["h"] = "c",
        ["cuda"] = "cuda-cpp", ["cu"] = "cuda-cpp",
        ["objc"] = "objective-c", ["objective-c"] = "objective-c", ["objectivec"] = "objective-c",
        ["objcpp"] = "objective-cpp", ["objective-cpp"] = "objective-cpp", ["objectivecpp"] = "objective-cpp",
        ["fs"] = "fsharp", ["fsharp"] = "fsharp", ["f#"] = "fsharp", ["fsx"] = "fsharp", ["fsi"] = "fsharp",
        ["vb"] = "vb", ["vbnet"] = "vb", ["vb.net"] = "vb", ["vbs"] = "vb", ["vbscript"] = "vb", ["visualbasic"] = "vb",
        ["dart"] = "dart",
        ["asm"] = "x86asm", ["x86asm"] = "x86asm", ["nasm"] = "x86asm", ["x86"] = "x86asm", ["assembly"] = "x86asm",
        ["hlsl"] = "hlsl", ["shaderlab"] = "shaderlab",
        ["pascal"] = "pascal", ["delphi"] = "pascal", ["pas"] = "pascal",
        // 脚本语言
        ["rb"] = "ruby", ["ruby"] = "ruby", ["gemspec"] = "ruby", ["rake"] = "ruby", ["podfile"] = "ruby",
        ["php"] = "php",
        ["lua"] = "lua",
        ["r"] = "r", ["rscript"] = "r",
        ["pl"] = "perl", ["perl"] = "perl", ["pm"] = "perl", ["raku"] = "perl6", ["perl6"] = "perl6",
        ["jl"] = "julia", ["julia"] = "julia",
        ["groovy"] = "groovy", ["gradle"] = "groovy", ["jenkinsfile"] = "groovy", ["nextflow"] = "groovy",
        ["clj"] = "clojure", ["clojure"] = "clojure", ["cljs"] = "clojure", ["cljc"] = "clojure", ["edn"] = "clojure",
        ["coffee"] = "coffeescript", ["coffeescript"] = "coffeescript",
        ["sql"] = "sql", ["mysql"] = "sql", ["postgres"] = "sql", ["postgresql"] = "sql", ["psql"] = "sql", ["pgsql"] = "sql",
        ["sqlite"] = "sql", ["tsql"] = "sql", ["plsql"] = "sql", ["mssql"] = "sql", ["sqlserver"] = "sql",
    };

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "text";
        }

        var value = language.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        return LanguageAliases.TryGetValue(value, out var mapped) ? mapped : value;
    }

    private static Color ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length == 8)
        {
            return Color.FromArgb(
                byte.Parse(value[..2], NumberStyles.HexNumber),
                byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber),
                byte.Parse(value.Substring(6, 2), NumberStyles.HexNumber));
        }

        return Color.FromRgb(
            byte.Parse(value[..2], NumberStyles.HexNumber),
            byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber),
            byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber));
    }

    private sealed record HighlightedLine(IReadOnlyList<HighlightedToken> Tokens);

    private sealed class HighlightedCodeTextBlock : SelectableTextBlock
    {
        private readonly IReadOnlyList<HighlightedLine> _lines;
        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);
        public HighlightedCodeTextBlock(string text, IReadOnlyList<HighlightedLine> lines)
        { _lines = lines; Text = text; }

        protected override TextLayout CreateTextLayout(string? text)
        {
            var styles = new List<ValueSpan<TextRunProperties>>();
            var start = 0;
            var selectionStart = Math.Min(SelectionStart, SelectionEnd);
            var selectionEnd = Math.Max(SelectionStart, SelectionEnd);
            foreach (var line in _lines)
            {
                foreach (var token in line.Tokens)
                {
                    var end = start + token.Text.Length;
                    var cutStart = Math.Clamp(selectionStart, start, end);
                    var cutEnd = Math.Clamp(selectionEnd, start, end);
                    Add(start, cutStart, token.Foreground);
                    Add(cutStart, cutEnd, SelectionForegroundBrush ?? token.Foreground);
                    Add(cutEnd, end, token.Foreground);
                    start = end;
                    void Add(int from, int to, IBrush? foreground)
                    {
                        if (to <= from) return;
                        styles.Add(new(from, to - from, new GenericTextRunProperties(
                            new Typeface(FontFamily, token.FontStyle, token.FontWeight, FontStretch), FontSize,
                            foregroundBrush: foreground, backgroundBrush: token.Background)));
                    }
                }
                start++; // 源文本使用 LF；每行末尾的换行沿用默认样式。
            }
            return new TextLayout(text ?? string.Empty, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize, Foreground, TextAlignment, TextWrapping, lineHeight: LineHeight,
                letterSpacing: LetterSpacing, textStyleOverrides: styles);
        }
    }

    /// <summary>
    /// 长代码分块正文：每块 <see cref="MarkdownCodeChunking.LinesPerChunk"/> 行独立排版，
    /// 行号按整篇行数一次生成（与代码列等高）；流式追加时只重建首个变化行所在的块及其后续块。
    /// </summary>
    private sealed class ChunkedHighlightedCodeBody : Grid, IMarkdownCodeBodyUpdate
    {
        private readonly StackPanel _codeColumn;
        private readonly Func<string, HighlightedLine[]?> _tokenize;
        private readonly FontFamily _fontFamily;
        private readonly double _fontSize;
        private readonly double _lineHeight;
        private readonly Func<bool>? _hasGlobalSelection;
        private readonly Func<Task>? _copyGlobalSelectionAsync;
        private readonly List<HighlightedCodeTextBlock> _chunks = [];
        private string _normalized;

        public ChunkedHighlightedCodeBody(
            string code,
            HighlightedLine[] lines,
            FontFamily fontFamily,
            double fontSize,
            double lineHeight,
            bool isDark,
            Func<string, HighlightedLine[]?> tokenize,
            Func<bool>? hasGlobalSelection,
            Func<Task>? copyGlobalSelectionAsync)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            _fontFamily = fontFamily;
            _fontSize = fontSize;
            _lineHeight = lineHeight;
            _normalized = code;
            _tokenize = tokenize;
            _hasGlobalSelection = hasGlobalSelection;
            _copyGlobalSelectionAsync = copyGlobalSelectionAsync;

            var codeLines = code.Split('\n');
            Children.Add(CreateLineNumbers(codeLines.Length, isDark, fontFamily, fontSize, lineHeight));
            _codeColumn = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Vertical };
            Grid.SetColumn(_codeColumn, 1);
            Children.Add(_codeColumn);
            for (var chunk = 0; chunk < ChunkCount(codeLines.Length); chunk++)
            {
                AddChunk(chunk, codeLines, lines);
            }
        }

        public bool TryUpdate(string code)
        {
            var normalized = code.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            if (lines.Length <= MarkdownCodeChunking.ThresholdLines)
            {
                return false;
            }

            var highlighted = _tokenize(normalized);
            if (highlighted is null || highlighted.Length != lines.Length)
            {
                return false;
            }

            var oldLines = _normalized.Split('\n');
            var shared = Math.Min(oldLines.Length, lines.Length);
            var firstChanged = 0;
            while (firstChanged < shared && oldLines[firstChanged] == lines[firstChanged])
            {
                firstChanged++;
            }

            var firstChunk = firstChanged / MarkdownCodeChunking.LinesPerChunk;
            while (_chunks.Count > firstChunk)
            {
                _codeColumn.Children.Remove(_chunks[^1]);
                _chunks.RemoveAt(_chunks.Count - 1);
            }

            for (var chunk = firstChunk; chunk < ChunkCount(lines.Length); chunk++)
            {
                AddChunk(chunk, lines, highlighted);
            }

            _normalized = normalized;
            return true;
        }

        private void AddChunk(int chunk, string[] lines, IReadOnlyList<HighlightedLine> highlighted)
        {
            var start = chunk * MarkdownCodeChunking.LinesPerChunk;
            var end = Math.Min(start + MarkdownCodeChunking.LinesPerChunk, lines.Length);
            var chunkLines = new HighlightedLine[end - start];
            for (var line = start; line < end; line++)
            {
                chunkLines[line - start] = highlighted[line];
            }

            var text = string.Join('\n', lines, start, end - start);
            var block = new HighlightedCodeTextBlock(text, chunkLines);
            ConfigureCodeTextBlock(block, _fontFamily, _fontSize, _lineHeight, _hasGlobalSelection, _copyGlobalSelectionAsync);
            _chunks.Add(block);
            _codeColumn.Children.Add(block);
        }

        private static int ChunkCount(int lineCount) =>
            (lineCount + MarkdownCodeChunking.LinesPerChunk - 1) / MarkdownCodeChunking.LinesPerChunk;
    }

    private sealed record HighlightedToken(
        string Text,
        IBrush? Foreground,
        IBrush? Background,
        FontWeight FontWeight,
        AvaloniaFontStyle FontStyle)
    {
        public Run CreateRun()
        {
            return new Run
            {
                Text = Text,
                Foreground = Foreground,
                Background = Background,
                FontWeight = FontWeight,
                FontStyle = FontStyle
            };
        }
    }

    private readonly record struct HighlightCacheKey(string Code, string Language, ThemeName Theme);
}
