using System.Globalization;
using Avalonia.Data.Converters;

namespace Vex.Core.Converters;

/// <summary>格式条宽度：源码窗格可见时取其实际列宽，否则回退整个工作区宽度。</summary>
public sealed class FirstPositiveWidthConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 3
            && values[0] is bool visible && visible
            && values[1] is double editorWidth && editorWidth > 0)
        {
            return editorWidth;
        }
        return values.Count == 3 && values[2] is double surfaceWidth && surfaceWidth > 0
            ? surfaceWidth
            : Avalonia.AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
