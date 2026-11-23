using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Views;

/// <summary>
/// Git 徽章三件套转换（设计原型 design/Git-Badges.html 的落地端）：
/// 字形 = octicon StreamGeometry（WorkbenchStyles 六个 WBGit*Icon 资源），
/// 颜色 = AppPalette 五支 VexGit*Brush（深浅两档，GitHub Desktop 权威配色），
/// 提示词 = IAppLocalizer 四语。None 一律映射 null，Data/IsVisible/Fill 共用。
/// </summary>
public static class GitBadgeConverters
{
    public static readonly IValueConverter NotNone = new NotNoneConverter();
    public static readonly IValueConverter Geometry = new GeometryConverter();
    public static readonly IValueConverter Word = new WordConverter();
    public static readonly IMultiValueConverter Brush = new BrushConverter();

    private static IAppLocalizer? Localizer
    {
        get
        {
            try
            {
                return App.Services.GetService(typeof(IAppLocalizer)) as IAppLocalizer;
            }
            catch
            {
                // 设计器/headless 无服务容器：提示词回落键名。
                return null;
            }
        }
    }

    private static string? GeometryKey(WorkspaceGitStatus status) => status switch
    {
        WorkspaceGitStatus.New or WorkspaceGitStatus.Untracked => "WBGitAddedIcon",
        WorkspaceGitStatus.Modified => "WBGitModifiedIcon",
        WorkspaceGitStatus.Renamed => "WBGitRenamedIcon",
        WorkspaceGitStatus.Deleted => "WBGitRemovedIcon",
        WorkspaceGitStatus.Conflicted => "WBGitConflictedIcon",
        _ => null,
    };

    private static string BrushKey(WorkspaceGitStatus status) => status switch
    {
        WorkspaceGitStatus.New or WorkspaceGitStatus.Untracked => "VexGitNewBrush",
        WorkspaceGitStatus.Modified => "VexGitModifiedBrush",
        WorkspaceGitStatus.Renamed => "VexGitRenamedBrush",
        WorkspaceGitStatus.Deleted => "VexGitDeletedBrush",
        WorkspaceGitStatus.Conflicted => "VexGitConflictedBrush",
        _ => "VexGitNewBrush",
    };

    private sealed class NotNoneConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is WorkspaceGitStatus status && status != WorkspaceGitStatus.None;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class GeometryConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not WorkspaceGitStatus status || status == WorkspaceGitStatus.None) return null;
            var key = GeometryKey(status);
            if (key is null) return null;
            return Application.Current?.TryGetResource(key, ThemeVariant.Default, out var geometry) == true
                ? geometry
                : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class BrushConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 1 || values[0] is not WorkspaceGitStatus status || status == WorkspaceGitStatus.None)
            {
                return null;
            }

            var theme = values.Count > 1 && values[1] is ThemeVariant variant ? variant : ThemeVariant.Default;
            return Application.Current?.TryGetResource(BrushKey(status), theme, out var brush) == true ? brush : null;
        }
    }

    private sealed class WordConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not WorkspaceGitStatus status || status == WorkspaceGitStatus.None) return null;
            var key = status switch
            {
                WorkspaceGitStatus.New => VexL.GitBadgeNew,
                WorkspaceGitStatus.Untracked => VexL.GitBadgeUntracked,
                WorkspaceGitStatus.Modified => VexL.GitBadgeModified,
                WorkspaceGitStatus.Renamed => VexL.GitBadgeRenamed,
                WorkspaceGitStatus.Deleted => VexL.GitBadgeDeleted,
                WorkspaceGitStatus.Conflicted => VexL.GitBadgeConflicted,
                _ => null,
            };
            return key is null ? null : Localizer?.Get(key);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
