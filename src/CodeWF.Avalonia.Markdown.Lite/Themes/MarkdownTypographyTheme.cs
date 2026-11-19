namespace CodeWF.Avalonia.Markdown.Themes;

/// <summary>
/// Markdown 内置排版主题 Key 集合。保持字符串常量是为了允许应用通过
/// <see cref="MarkdownTypographyThemeRegistry" /> 注册自己的主题 Key。
/// </summary>
public static class MarkdownTypographyThemes
{
    public const string Basic = "Basic";
    public const string OrangeHeart = "OrangeHeart";
    public const string InkBlack = "InkBlack";
    public const string ColorfulPurple = "ColorfulPurple";
    public const string TenderGreen = "TenderGreen";
    public const string RedScarlet = "RedScarlet";
    public const string TechnologyBlue = "TechnologyBlue";
    public const string LanQing = "LanQing";
    public const string Yamabuki = "Yamabuki";
    public const string GeekBlack = "GeekBlack";
    public const string Simple = "Simple";
    public const string WeChatFormat = "WeChatFormat";
}

/// <summary>
/// Markdown 排版尺寸 Key 集合，可与任意排版主题叠加使用。
/// </summary>
public static class MarkdownTypographySizes
{
    /// <summary>紧凑档：基准正文 13px（规则 §11）。</summary>
    public const string Compact = "Compact";
    /// <summary>正常档：基准正文 15px。</summary>
    public const string Normal = "Normal";
    /// <summary>宽松档：基准正文 17px。</summary>
    public const string Relaxed = "Relaxed";
}
