using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Vex.Core.Theme;

/// <summary>
/// 强调色投影的绑定转换；明暗固定投影由 StudioPalette.axaml 的主题字典维护。
/// </summary>
public static class ElevationResources
{
    // 原型 prototype.css:430/433：强调色自身叠 15%/22%，与九套配色和用户配色一起换。
    public static FuncMultiValueConverter<Color, BoxShadows> Primary { get; } =
        new(colors => AccentShadow(colors[0], 0x26, "0 2 5 0"));
    public static FuncMultiValueConverter<Color, BoxShadows> PrimaryHover { get; } =
        new(colors => AccentShadow(colors[0], 0x38, "0 4 12 0"));

    private static BoxShadows AccentShadow(Color accent, byte alpha, string geometry)
        => BoxShadows.Parse($"{geometry} #{alpha:X2}{accent.R:X2}{accent.G:X2}{accent.B:X2}");

}
