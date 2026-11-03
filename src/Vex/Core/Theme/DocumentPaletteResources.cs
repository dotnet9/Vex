using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Vex.Core.Theme;

/// <summary>正文里依赖应用配色的混色令牌，与排版主题的字号和留白分开维护。</summary>
internal static class DocumentPaletteResources
{
    // professional.css: th = panel 78% + accent-soft 22%，由 AXAML 动态颜色绑定驱动。
    public static FuncMultiValueConverter<Color, Color> TableHeader { get; } = new(colors =>
    {
        var panel = colors[0];
        var soft = colors[1];
        return Color.FromRgb(
            (byte)Math.Round(panel.R * .78 + soft.R * .22),
            (byte)Math.Round(panel.G * .78 + soft.G * .22),
            (byte)Math.Round(panel.B * .78 + soft.B * .22));
    });
}
