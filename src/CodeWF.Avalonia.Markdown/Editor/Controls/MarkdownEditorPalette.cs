using global::Avalonia.Media;
using global::Avalonia.Styling;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>
/// <see cref="MarkdownEditorView"/> 的内置调色板：宿主不提供同名资源键时使用。
/// <para>
/// 键名与宿主可覆盖的资源键一致，宿主只要在应用资源里注册同名资源（含 ThemeDictionaries）
/// 就能整包换肤；本类只作为兜底，保证控件单独放进任何应用都显示正常。
/// </para>
/// </summary>
public static class MarkdownEditorPalette
{
    public const string BackgroundKey = "CodeWFMarkdownEditorBackgroundBrush";
    public const string ForegroundKey = "CodeWFMarkdownEditorForegroundBrush";
    public const string CurrentLineBackgroundKey = "CodeWFMarkdownEditorCurrentLineBackgroundBrush";
    public const string CurrentLineBorderKey = "CodeWFMarkdownEditorCurrentLineBorderBrush";
    public const string AccentKey = "CodeWFMarkdownEditorAccentBrush";
    public const string CodeKey = "CodeWFMarkdownEditorCodeBrush";
    public const string QuoteKey = "CodeWFMarkdownEditorQuoteBrush";
    public const string LinkKey = "CodeWFMarkdownEditorLinkBrush";
    public const string ImageKey = "CodeWFMarkdownEditorImageBrush";
    public const string WarningKey = "CodeWFMarkdownEditorWarningBrush";
    public const string DangerKey = "CodeWFMarkdownEditorDangerBrush";
    public const string SeparatorKey = "CodeWFMarkdownEditorSeparatorBrush";
    public const string MarkerKey = "CodeWFMarkdownEditorMarkerBrush";

    private static readonly IReadOnlyDictionary<string, Color> LightColors = new Dictionary<string, Color>
    {
        [BackgroundKey] = Color.Parse("#FFFFFF"),
        [ForegroundKey] = Color.Parse("#101828"),
        [CurrentLineBackgroundKey] = Color.Parse("#F4F7FB"),
        [CurrentLineBorderKey] = Color.Parse("#E4E9F2"),
        [AccentKey] = Color.Parse("#1264D7"),
        [CodeKey] = Color.Parse("#B42318"),
        [QuoteKey] = Color.Parse("#667085"),
        [LinkKey] = Color.Parse("#1264D7"),
        [ImageKey] = Color.Parse("#7C3AED"),
        [WarningKey] = Color.Parse("#B54708"),
        [DangerKey] = Color.Parse("#CF222E"),
        [SeparatorKey] = Color.Parse("#F2F4F7"),
        [MarkerKey] = Color.Parse("#667085"),
    };

    private static readonly IReadOnlyDictionary<string, Color> DarkColors = new Dictionary<string, Color>
    {
        [BackgroundKey] = Color.Parse("#111827"),
        [ForegroundKey] = Color.Parse("#E5E7EB"),
        [CurrentLineBackgroundKey] = Color.Parse("#1B2533"),
        [CurrentLineBorderKey] = Color.Parse("#334155"),
        [AccentKey] = Color.Parse("#60A5FA"),
        [CodeKey] = Color.Parse("#FBBF77"),
        [QuoteKey] = Color.Parse("#AAB4C3"),
        [LinkKey] = Color.Parse("#60A5FA"),
        [ImageKey] = Color.Parse("#C4B5FD"),
        [WarningKey] = Color.Parse("#FBBF24"),
        [DangerKey] = Color.Parse("#F87171"),
        [SeparatorKey] = Color.Parse("#232B38"),
        [MarkerKey] = Color.Parse("#AAB4C3"),
    };

    /// <summary>按主题变体解析内置调色板颜色；未知键返回 null。</summary>
    public static IBrush? Resolve(string? key, ThemeVariant? themeVariant)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var variant = themeVariant;
        while (variant is not null && variant != ThemeVariant.Dark && variant != ThemeVariant.Light)
            variant = variant.InheritVariant;
        var palette = variant == ThemeVariant.Dark ? DarkColors : LightColors;
        return palette.TryGetValue(key, out var color) ? new SolidColorBrush(color) : null;
    }
}
