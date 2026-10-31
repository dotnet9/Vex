using Avalonia;
using Avalonia.Media;

namespace Vex.Core.Theme;

/// <summary>正文里依赖应用配色的混色令牌，与排版主题的字号和留白分开维护。</summary>
internal static class DocumentPaletteResources
{
    public static void Register()
    {
        if (Application.Current is not { } app) return;
        app.ActualThemeVariantChanged += (_, _) => Apply();
        Apply();
    }

    private static void Apply()
    {
        if (Application.Current is not { } app
            || !app.TryGetResource("StudioSurfaceColor", app.ActualThemeVariant, out var panelValue)
            || !app.TryGetResource("StudioSelectedColor", app.ActualThemeVariant, out var softValue)
            || panelValue is not Color panel || softValue is not Color soft) return;

        // professional.css: th = panel 78% + accent-soft 22%; 直接读颜色令牌，避免画刷绑定更新滞后一帧。
        app.Resources["VexDocumentTableHeaderBrush"] = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Round(panel.R * .78 + soft.R * .22),
            (byte)Math.Round(panel.G * .78 + soft.G * .22),
            (byte)Math.Round(panel.B * .78 + soft.B * .22)));
    }
}
