using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Styling;
using CodeWF.Avalonia.MindView.Controls;

namespace CodeWF.Avalonia.MindView;

internal static class MindViewThemeResources
{
    public static IBrush AccentForeground(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        var luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        return luminance > .18 ? Brushes.Black : Brushes.White;
    }
    public static double GetDouble(AvaloniaObject? owner, string key, double fallback)
    {
        return TryGetResource(owner, key, out var value) ? ToDouble(value, fallback) : fallback;
    }

    public static int GetInt32(AvaloniaObject? owner, string key, int fallback)
    {
        return (int)Math.Round(GetDouble(owner, key, fallback));
    }

    public static string GetString(AvaloniaObject? owner, string key, string fallback)
    {
        return TryGetResource(owner, key, out var value) && value is not null
            ? value.ToString() ?? fallback
            : fallback;
    }

    public static IBrush GetBrush(AvaloniaObject? owner, string key, string lightFallback, string darkFallback)
    {
        if (TryGetResource(owner, key, out var value))
        {
            if (value is IBrush brush)
            {
                return brush;
            }

            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }

            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return Brush.Parse(text);
            }
        }

        return Brush.Parse(IsDark(owner) ? darkFallback : lightFallback);
    }

    public static Color GetColor(AvaloniaObject? owner, string key, string lightFallback, string darkFallback)
    {
        if (TryGetResource(owner, key, out var value))
        {
            if (value is Color color)
            {
                return color;
            }

            if (value is ISolidColorBrush solidColorBrush)
            {
                return solidColorBrush.Color;
            }

            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return Color.Parse(text);
            }
        }

        return Color.Parse(IsDark(owner) ? darkFallback : lightFallback);
    }

    public static Thickness GetThickness(AvaloniaObject? owner, string key, Thickness fallback)
    {
        if (!TryGetResource(owner, key, out var value))
        {
            return fallback;
        }

        return value switch
        {
            Thickness thickness => thickness,
            double uniform => new Thickness(uniform),
            int uniform => new Thickness(uniform),
            string text when !string.IsNullOrWhiteSpace(text) => Thickness.Parse(text),
            _ => fallback
        };
    }

    public static CornerRadius GetCornerRadius(AvaloniaObject? owner, string key, CornerRadius fallback)
    {
        if (!TryGetResource(owner, key, out var value))
        {
            return fallback;
        }

        return value switch
        {
            CornerRadius cornerRadius => cornerRadius,
            double uniform => new CornerRadius(uniform),
            int uniform => new CornerRadius(uniform),
            string text when !string.IsNullOrWhiteSpace(text) => CornerRadius.Parse(text),
            _ => fallback
        };
    }

    public static BoxShadows GetBoxShadows(AvaloniaObject? owner, string key, string fallback)
    {
        if (TryGetResource(owner, key, out var value))
        {
            if (value is BoxShadows boxShadows)
            {
                return boxShadows;
            }

            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return BoxShadows.Parse(text);
            }
        }

        return BoxShadows.Parse(fallback);
    }

    public static bool IsDark(AvaloniaObject? owner)
    {
        return owner switch
        {
            MindMapEditor editor => editor.IsDarkTheme,
            MindMapMiniMap miniMap => miniMap.IsDarkTheme,
            StyledElement styledElement => IsDarkVariant(styledElement.ActualThemeVariant),
            _ => false
        };
    }

    internal static bool IsDarkVariant(ThemeVariant? variant)
    {
        while (variant is not null && variant != ThemeVariant.Dark && variant != ThemeVariant.Light)
            variant = variant.InheritVariant;
        return variant == ThemeVariant.Dark;
    }

    private static bool TryGetResource(AvaloniaObject? owner, string key, out object? value)
    {
        if (owner is StyledElement styledElement)
        {
            // 自定义变体保留自身资源；显式明暗属性仍可选择标准变体。
            var themeVariant = styledElement.ActualThemeVariant;
            if (IsDark(owner) != IsDarkVariant(themeVariant))
                themeVariant = IsDark(owner) ? ThemeVariant.Dark : ThemeVariant.Light;
            if (styledElement.TryFindResource(key, themeVariant, out value))
            {
                return true;
            }
        }

        var application = Application.Current;
        if (application is not null && application.TryGetResource(key, IsDark(owner) ? ThemeVariant.Dark : null, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static double ToDouble(object? value, double fallback)
    {
        return value switch
        {
            double number => number,
            float number => number,
            decimal number => (double)number,
            int number => number,
            long number => number,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
            _ => fallback
        };
    }
}
