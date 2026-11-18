using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>
/// 所见即所得块视图的配色快照：由 <see cref="MarkdownLiveEditorView"/> 在宿主令牌变化时解析一次，
/// 再注入每个块视图，避免控件与宿主调色板耦合。
/// </summary>
internal sealed class MarkdownBlockViewOptions
{
    public MarkdownSyntaxOptions? SyntaxOptions { get; set; }
    public bool IsDefaultContextMenuEnabled { get; set; } = true;
    public string? ImageBasePath { get; set; }
    public string? TypographyTheme { get; set; }
    public string? TypographySize { get; set; }
    public double FontSize { get; set; } = 15;
    public double CodeFontSize { get; set; } = 13;
    public bool CodeWrap { get; set; }
    public FontFamily ContentFontFamily { get; set; } = FontFamily.Default;
    public FontFamily CodeFontFamily { get; set; } = MarkdownWysiwygPalette.CodeFontFamily;
    public IBrush? TableHeaderBackgroundBrush { get; set; }

    public IBrush TextBrush { get; private set; } = Brushes.Black;

    public IBrush MutedBrush { get; private set; } = Brushes.Gray;

    public IBrush AccentBrush { get; private set; } = Brushes.DodgerBlue;

    public IBrush CodeBrush { get; private set; } = Brushes.Firebrick;

    public IBrush LinkBrush { get; private set; } = Brushes.DodgerBlue;

    public IBrush CodeBackgroundBrush { get; private set; } = Brushes.WhiteSmoke;

    public IBrush SeparatorBrush { get; private set; } = Brushes.LightGray;

    /// <summary>应用一组解析后的画刷（null 视为“未解析”，保留原值）；有实际变化时返回 true。</summary>
    public bool Apply(
        IBrush? text,
        IBrush? muted,
        IBrush? accent,
        IBrush? code,
        IBrush? link,
        IBrush? codeBackground,
        IBrush? separator)
    {
        var changed = text is not null && !ReferenceEquals(TextBrush, text)
            || muted is not null && !ReferenceEquals(MutedBrush, muted)
            || accent is not null && !ReferenceEquals(AccentBrush, accent)
            || code is not null && !ReferenceEquals(CodeBrush, code)
            || link is not null && !ReferenceEquals(LinkBrush, link)
            || codeBackground is not null && !ReferenceEquals(CodeBackgroundBrush, codeBackground)
            || separator is not null && !ReferenceEquals(SeparatorBrush, separator);
        TextBrush = text ?? TextBrush;
        MutedBrush = muted ?? MutedBrush;
        AccentBrush = accent ?? AccentBrush;
        CodeBrush = code ?? CodeBrush;
        LinkBrush = link ?? LinkBrush;
        CodeBackgroundBrush = codeBackground ?? CodeBackgroundBrush;
        SeparatorBrush = separator ?? SeparatorBrush;
        return changed;
    }
}
