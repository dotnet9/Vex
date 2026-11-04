using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Vex.Core.Theme;

/// <summary>强调色一族的补充派生令牌。on-accent（StudioOnPrimary）与悬停（StudioPrimaryHover）
/// 已由 Studio 引擎按对比度派生，这里只补原型药丸下载进度的混色与分栏长度。</summary>
internal static class AccentPaletteResources
{
    /// <summary>原型药丸进度左段 color-mix(in srgb, accent 74%, fg)：深浅差刚好读出「已下载」的推进。</summary>
    public static FuncMultiValueConverter<Color, Color> AccentProgress { get; } = new(colors =>
    {
        var accent = colors[0];
        var ink = colors[1];
        return Mix(accent, ink, 0.26);
    });

    /// <summary>下载进度条左侧的星号宽度：pct(0-100) → 星号 GridLength，右段自动占余量。</summary>
    public static FuncValueConverter<double, GridLength> ProgressTrack { get; } =
        new(value => new GridLength(Math.Clamp(value, 0, 100), GridUnitType.Star));

    private static Color Mix(Color left, Color right, double rightRatio)
    {
        byte Mix(byte a, byte b) => (byte)Math.Round(a * (1 - rightRatio) + b * rightRatio);
        return Color.FromRgb(Mix(left.R, right.R), Mix(left.G, right.G), Mix(left.B, right.B));
    }
}
