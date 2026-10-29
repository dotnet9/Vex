namespace Vex.Core.Models;

public sealed record TypographyOption(string Name, string? Key)
{
    // 用户排版只注册在 Resolver，不在生成的静态目录里：按 Catalog 取会让自定义项的 Definition 为 null，
    // 徽章、对比度这些读 Definition 的地方当场空引用。设置里残留已删除键（升级删键、自定义排版未注册）
    // 时同样拿不到定义，回退简约排版而不是启动即崩。
    public CodeWF.Avalonia.Markdown.Themes.ArticleTypography Definition =>
        CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Find(Key)
        ?? CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Find("Simple")
        ?? CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.All.First();
    public string PurposeKey => "Vex.VexL.TypographyPurpose" + Key;
    public bool IsRecommended => Definition.Recommended;
    public string SwatchColor => Key switch
    {
        "OrangeHeart" => "#F59E7F", "ColorfulPurple" => "#9574CD",
        "TenderGreen" => "#62B78A", "RedScarlet" => "#E78294",
        "InkBlack" or "GeekBlack" => "#7C7C87", "Yamabuki" => "#D6AD54", "LanQing" => "#5AB5B4", _ => "#8E9DFB"
    };
    public string NameKey => "Vex.VexL.Typography" + Key;
}
