using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.Views;

/// <summary>视图层解析文件图标，节点模型只保留文件信息及展开状态。</summary>
public sealed class FileIconConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 5 || values[0] is not string name || values[1] is not bool folder
            || values[2] is not bool expanded || values[4] is not int level)
        {
            return null;
        }

        var theme = values[3] as ThemeVariant;
        while (theme?.InheritVariant is { } inherited) theme = inherited;
        return FileIconCatalog.Default.Resolve(name, folder, expanded, theme == ThemeVariant.Light, level == 0);
    }
}
