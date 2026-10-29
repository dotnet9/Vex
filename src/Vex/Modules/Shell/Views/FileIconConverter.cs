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
        // 5 值为树形行（末位 TreeViewItem.Level）；4 值为列表行（无层级，按非根处理）。
        if (values.Count is not (4 or 5) || values[0] is not string name || values[1] is not bool folder
            || values[2] is not bool expanded)
        {
            return null;
        }

        var level = values.Count > 4 && values[4] is int depth ? depth : 1;
        var theme = values[3] as ThemeVariant;
        while (theme?.InheritVariant is { } inherited) theme = inherited;
        return FileIconCatalog.Default.Resolve(name, folder, expanded, theme == ThemeVariant.Light, level == 0);
    }
}
