using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Vex.Core.Theme;

/// <summary>正文里依赖应用配色的混色令牌，与排版主题的字号和留白分开维护。</summary>
internal static class DocumentPaletteResources
{
    // 状态资源既可能是 Color，也可能是主题引擎提供的画刷；Color 属性不能直接接收后者。
    public static FuncMultiValueConverter<object, object> ResourceColor { get; } = new(values =>
        values.FirstOrDefault() switch
        {
            Color color => color,
            ISolidColorBrush brush => brush.Color,
            _ => AvaloniaProperty.UnsetValue
        });

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
