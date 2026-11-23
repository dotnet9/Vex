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

    private static string? GeometryKey(WorkspaceGitStatus status, bool lead) => (status, lead) switch
    {
        // 树内前导槽语言（REV2）：受管干净=锁；修改/重命名=红勾；新增=加号；未跟踪=空槽。
        (WorkspaceGitStatus.TrackedClean, _) => "WBGitLockIcon",
        (WorkspaceGitStatus.Modified or WorkspaceGitStatus.Renamed, true) => "WBGitCheckIcon",
        (WorkspaceGitStatus.New, true) => "WBGitPlusIcon",
        (WorkspaceGitStatus.Untracked, true) => null,
        (WorkspaceGitStatus.Deleted, _) => "WBGitRemovedIcon",
        (WorkspaceGitStatus.Conflicted, _) => "WBGitConflictedIcon",
        (WorkspaceGitStatus.New or WorkspaceGitStatus.Untracked, false) => "WBGitAddedIcon",
        (WorkspaceGitStatus.Modified, false) => "WBGitModifiedIcon",
        (WorkspaceGitStatus.Renamed, false) => "WBGitRenamedIcon",
        _ => null,
    };

    private static string? BrushKey(WorkspaceGitStatus status, bool lead) => (status, lead) switch
    {
        (WorkspaceGitStatus.TrackedClean, _) => "VexGitLockBrush",
        (WorkspaceGitStatus.Modified or WorkspaceGitStatus.Renamed, true) => "VexGitCheckBrush",
        (WorkspaceGitStatus.New, true) => "VexGitNewBrush",
        (WorkspaceGitStatus.Untracked, true) => null,
        (WorkspaceGitStatus.Deleted, _) => "VexGitDeletedBrush",
        (WorkspaceGitStatus.Conflicted, _) => "VexGitConflictedBrush",
        (WorkspaceGitStatus.New or WorkspaceGitStatus.Untracked, false) => "VexGitNewBrush",
        (WorkspaceGitStatus.Modified, false) => "VexGitModifiedBrush",
        (WorkspaceGitStatus.Renamed, false) => "VexGitRenamedBrush",
        _ => null,
    };

    private sealed class NotNoneConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not WorkspaceGitStatus status || status == WorkspaceGitStatus.None) return false;
            // 树内前导槽：未跟踪=空槽，整个字形元素隐藏（面板无此参数，未跟踪行照常显示）。
            return !(status == WorkspaceGitStatus.Untracked && parameter as string == "lead");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class GeometryConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not WorkspaceGitStatus status || status == WorkspaceGitStatus.None) return null;
            var lead = parameter as string == "lead";
            var key = GeometryKey(status, lead);
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

            var lead = parameter as string == "lead";
            var theme = values.Count > 1 && values[1] is ThemeVariant variant ? variant : ThemeVariant.Default;
            var key = BrushKey(status, lead);
            if (key is null) return null;
            return Application.Current?.TryGetResource(key, theme, out var brush) == true ? brush : null;
        }
    }

    private sealed class WordConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not WorkspaceGitStatus status || status == WorkspaceGitStatus.None) return null;
            var key = status switch
            {
                WorkspaceGitStatus.TrackedClean => VexL.GitBadgeTrackedClean,
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
