using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Themes;

/// <summary>
/// 排版查找的统一入口：内置目录（生成的静态表）+ 应用运行时注册的用户排版。
/// 渲染器与导出统一走这里，生成文件不感知用户数据。
/// </summary>
public static class ArticleTypographyResolver
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ArticleTypography> Custom = new(StringComparer.Ordinal);

    public static void Register(ArticleTypography typography)
    {
        if (string.IsNullOrWhiteSpace(typography.Key)) return;
        lock (Gate)
        {
            Custom[typography.Key] = typography;
        }
        MarkdownTypographyThemeRegistry.Register(typography.Key, typography.CreateResources);
    }

    public static bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (Gate)
        {
            return Custom.Remove(key);
        }
    }

    public static ArticleTypography? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (Gate)
        {
            if (Custom.TryGetValue(key, out var custom)) return custom;
        }
        return ArticleTypographyCatalog.Find(key);
    }

    public static IReadOnlyList<ArticleTypography> All
    {
        get
        {
            lock (Gate)
            {
                return Custom.Values.Concat(ArticleTypographyCatalog.All).ToList();
            }
        }
    }

    /// <summary>
    /// 以内置「简约」为模板派生用户排版：改名称/强调与装饰色/字体/行距/H2 装饰，其余沿用。
    /// <paramref name="h2Decoration"/> 取 leftBar / underline / tint / none，与编写层同名档位一致；
    /// 只重写 H2 的边框、内距、色块与圆角，正文字号与行距不动。
    /// </summary>
    public static ArticleTypography CreateCustom(string key, string name, string accentLight, string accentDark,
        string decorationLight, string decorationDark, string font, double leading, string h2Decoration = "leftBar")
    {
        var template = ArticleTypographyCatalog.Find("Simple")
            ?? ArticleTypographyCatalog.All.First();
        var headings = template.Headings.ToArray();
        if (headings.Length > 1) headings[1] = WithHeadingStyle(headings[1], h2Decoration);
        return template with
        {
            Key = key,
            Name = name,
            EnglishName = "Custom",
            Purpose = "自定义排版",
            Recommended = false,
            LightText = accentLight,
            DarkText = accentDark,
            LightDecoration = decorationLight,
            DarkDecoration = decorationDark,
            Font = font,
            Leading = Math.Clamp(leading, 1.5, 2.4),
            Headings = headings,
        };
    }

    /// <summary>把编写层的四档 H2 装饰换成渲染层资源能表达的边框/内距/色块/圆角；em 按该级字号折算成像素。</summary>
    private static ArticleHeadingStyle WithHeadingStyle(ArticleHeadingStyle heading, string decoration)
    {
        heading = heading with
        {
            BorderColor = "var(--type-decoration)", BackgroundColor = "var(--type-decoration)",
            DashedBorder = false, Corners = null
        };
        static string Em(double size, double top, double right, double bottom, double left) => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Math.Round(size * left, 2)},{Math.Round(size * top, 2)},{Math.Round(size * right, 2)},{Math.Round(size * bottom, 2)}");
        return decoration switch
        {
            "underline" => heading with { Border = "0,0,0,2", Padding = Em(heading.Size, 0, 0, .35, 0), Tint = 0, Radius = 0 },
            "tint" => heading with { Border = "0,0,0,0", Padding = Em(heading.Size, .4, .7, .4, .7), Tint = .1, Radius = 4 },
            "none" => heading with { Border = "0,0,0,0", Padding = "0,0,0,0", Tint = 0, Radius = 0 },
            _ => heading with { Border = "4,0,0,0", Padding = Em(heading.Size, .3, .7, .3, .7), Tint = 0, Radius = 0 },
        };
    }
}
