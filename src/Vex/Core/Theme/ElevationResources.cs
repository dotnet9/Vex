using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Vex.Core.Theme;

/// <summary>
/// 浮起面的投影令牌：投影是一条字符串常量，不像画刷那样能随主题变体自动换色，
/// 所以按当前实际明暗变体回放成应用级资源，样式侧一律用 DynamicResource 取。
/// </summary>
public static class ElevationResources
{
    /// <summary>悬浮件（状态栏胶囊、演示模式条）的投影键，对应原型 --pop-shadow。</summary>
    public const string PopKey = "VexPopShadow";

    /// <summary>分段控件指示块的投影键，对应原型 --segmented-thumb-shadow。</summary>
    public const string SegmentedThumbKey = "VexSegmentedThumbShadow";

    /// <summary>主按钮（原型 .btn.primary）常态投影键：强调色 15% 的 0 2 5。</summary>
    public const string PrimaryKey = "VexPrimaryShadow";

    /// <summary>主按钮悬停投影键，对应原型 :hover 的 0 4 12 与 22% 扩散。</summary>
    public const string PrimaryHoverKey = "VexPrimaryShadowHover";

    // 原型 tokens.css:3-4 --pop-shadow：浅色 12% 墨绿 + 6% 墨绿双层，深色 33% + 20% 纯黑双层。
    private static readonly BoxShadows PopLight = BoxShadows.Parse("0 12 40 0 #20233E30, 0 2 8 0 #10233E30");
    private static readonly BoxShadows PopDark = BoxShadows.Parse("0 12 40 0 #55000000, 0 2 8 0 #33000000");

    // 原型 segmented.css:8 + prototype.css:24-25：浅色指示块用 1px 4px 墨投影浮起，深色把投影换成 1px 淡环。
    // 环本身是调色板令牌，由 VexSegmentedThumbBorderBrush 以描边给出，这里只需在深色档把投影清零。
    private static readonly BoxShadows ThumbLight = BoxShadows.Parse("0 1 4 0 #1F000000");

    /// <summary>按当前实际明暗变体重写应用级投影资源（启动与主题切换各调一次）。</summary>
    public static void Apply()
    {
        if (Application.Current is not { } app) return;
        var dark = IsDarkVariant(app.ActualThemeVariant);
        app.Resources[PopKey] = dark ? PopDark : PopLight;
        app.Resources[SegmentedThumbKey] = dark ? default : ThumbLight;
        // 原型 prototype.css:430/433：主按钮那圈投影直接用强调色自身叠 15%/22%，
        // 所以它跟着九套配色一起换，写成常量就会在换肤后留下一圈错色光晕。
        var accent = app.TryGetResource("VexAccentBrush", app.ActualThemeVariant, out var accentResource)
            && accentResource is ISolidColorBrush accentBrush ? accentBrush.Color : default;
        app.Resources[PrimaryKey] = AccentShadow(accent, 0x26, "0 2 5 0");
        app.Resources[PrimaryHoverKey] = AccentShadow(accent, 0x38, "0 4 12 0");
    }

    private static BoxShadows AccentShadow(Color accent, byte alpha, string geometry)
        => BoxShadows.Parse($"{geometry} #{alpha:X2}{accent.R:X2}{accent.G:X2}{accent.B:X2}");

    /// <summary>九套配色各有一个自定义变体（如 IndigoDark），只沿 InheritVariant 往上才落到标准明暗变体，
    /// 直接和 ThemeVariant.Dark 比会永远为假。</summary>
    private static bool IsDarkVariant(ThemeVariant? variant)
    {
        while (variant is not null && variant != ThemeVariant.Dark && variant != ThemeVariant.Light)
            variant = variant.InheritVariant;
        return variant == ThemeVariant.Dark;
    }
}
