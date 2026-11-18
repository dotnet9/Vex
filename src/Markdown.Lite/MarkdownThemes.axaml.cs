using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.LogicalTree;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.Styling;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Themes;
using System.Runtime.CompilerServices;

namespace CodeWF.Avalonia.Markdown.Themes;

/// <summary>
/// Markdown 控件的默认样式入口，并负责把指定排版主题资源合并到目标资源字典。
/// </summary>
public class MarkdownThemes : Styles
{
    private static readonly string[] TypographyResourceKeys =
    [
        MarkdownStyleKeys.CodeBlockFontSizeResource,
        MarkdownStyleKeys.ParagraphFontSizeResource,
        MarkdownStyleKeys.ParagraphLineHeightResource,
        MarkdownStyleKeys.Heading1FontSizeResource,
        MarkdownStyleKeys.Heading2FontSizeResource,
        MarkdownStyleKeys.Heading3FontSizeResource,
        MarkdownStyleKeys.Heading4FontSizeResource,
        MarkdownStyleKeys.Heading5FontSizeResource,
        MarkdownStyleKeys.Heading6FontSizeResource,
        MarkdownStyleKeys.TypographyThemeResource,
        MarkdownStyleKeys.TypographySizeResource,
        MarkdownStyleKeys.TypographyBaseResourcesResource,
    ];

    private static readonly HashSet<string> PaletteResourceKeys =
    [
        MarkdownStyleKeys.TextBrushResource,
        MarkdownStyleKeys.MutedTextBrushResource,
        MarkdownStyleKeys.BorderBrushResource,
        MarkdownStyleKeys.AccentBrushResource,
        MarkdownStyleKeys.AccentForegroundBrushResource,
        MarkdownStyleKeys.QuoteBackgroundBrushResource,
        MarkdownStyleKeys.InlineCodeBackgroundBrushResource,
        MarkdownStyleKeys.TableHeaderBackgroundBrushResource,
        MarkdownStyleKeys.CodeBackgroundBrushResource,
        MarkdownStyleKeys.SelectedBlockBrushResource
    ];

    private static readonly ConditionalWeakTable<IResourceDictionary, AppliedTypographyResources> AppliedResources = new();

    private string? _typographyTheme = MarkdownTypographyThemes.Basic;
    private string? _typographySize = MarkdownTypographySizes.Normal;

    static MarkdownThemes()
    {
        MarkdownViewer.TypographyThemeProperty.Changed.AddClassHandler<MarkdownViewer>((viewer, _) => ApplyViewerTypographyResources(viewer));
        MarkdownViewer.TypographySizeProperty.Changed.AddClassHandler<MarkdownViewer>((viewer, _) => ApplyViewerTypographyResources(viewer));
    }

    public MarkdownThemes()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyDefaultTypographyResources();
    }

    /// <summary>
    /// 全局默认排版主题。默认 Basic；单个 MarkdownViewer 可通过同名属性覆盖。
    /// </summary>
    public virtual string? TypographyTheme
    {
        get => _typographyTheme;
        set
        {
            _typographyTheme = value;
            ApplyDefaultTypographyResources();
        }
    }

    /// <summary>
    /// 排版尺寸。默认 Normal；设置为 Small 时会在当前排版主题上叠加紧凑字号、行高和间距资源。
    /// </summary>
    public virtual string? TypographySize
    {
        get => _typographySize;
        set
        {
            _typographySize = value;
            ApplyDefaultTypographyResources();
        }
    }

    /// <summary>
    /// 覆盖应用级 Markdown 排版资源，适合全局主题切换。
    /// </summary>
    public static void OverrideTypographyResources(Application application, string? typographyTheme)
    {
        OverrideTypographyResources(application, typographyTheme, MarkdownTypographySizes.Normal);
    }

    public static void OverrideTypographyResources(Application application, string? typographyTheme, string? typographySize)
    {
        ApplyTypographyResources(
            application.Resources,
            CreateTypographyResources(typographyTheme, typographySize, includePalette: false));
        RefreshMarkdownViewerTypographyResources(application);
    }

    /// <summary>
    /// 覆盖指定控件或窗口的 Markdown 排版资源，适合局部预览区切换主题。
    /// </summary>
    public static void OverrideTypographyResources(StyledElement element, string? typographyTheme)
    {
        OverrideTypographyResources(element, typographyTheme, MarkdownTypographySizes.Normal);
    }

    public static void OverrideTypographyResources(StyledElement element, string? typographyTheme, string? typographySize)
    {
        ApplyTypographyResources(
            element.Resources,
            CreateTypographyResources(typographyTheme, typographySize, includePalette: false));
        RefreshMarkdownViewerTypographyResources(element);
    }

    public static void OverrideTypographyResources(Application application, ResourceDictionary typographyResources)
    {
        OverrideTypographyResources(application, typographyResources, MarkdownTypographySizes.Normal);
    }

    public static void OverrideTypographyResources(Application application, ResourceDictionary typographyResources, string? typographySize)
    {
        ApplyTypographyResources(
            application.Resources,
            CreateSizedTypographyResources(typographyResources, typographySize, null, includePalette: false));
        RefreshMarkdownViewerTypographyResources(application);
    }

    public static void OverrideTypographyResources(StyledElement element, ResourceDictionary typographyResources)
    {
        OverrideTypographyResources(element, typographyResources, MarkdownTypographySizes.Normal);
    }

    public static void OverrideTypographyResources(StyledElement element, ResourceDictionary typographyResources, string? typographySize)
    {
        ApplyTypographyResources(
            element.Resources,
            CreateSizedTypographyResources(typographyResources, typographySize, null, includePalette: false));
        RefreshMarkdownViewerTypographyResources(element);
    }

    public static void ApplyTypographyResources(IResourceDictionary targetResources, string? typographyTheme)
    {
        ApplyTypographyResources(
            targetResources,
            CreateTypographyResources(typographyTheme, MarkdownTypographySizes.Normal, includePalette: false));
    }

    public static void ApplyTypographyResources(IResourceDictionary targetResources, string? typographyTheme, string? typographySize)
    {
        ApplyTypographyResources(
            targetResources,
            CreateTypographyResources(typographyTheme, typographySize, includePalette: false));
    }

    public static void ApplyTypographyResources(IResourceDictionary targetResources, ResourceDictionary typographyResources)
    {
        RemoveTypographyResources(targetResources);

        var applied = AppliedResources.GetOrCreateValue(targetResources);
        targetResources.MergedDictionaries.Add(typographyResources);
        applied.Resources = typographyResources;
    }

    public static ResourceDictionary CreateTypographyResources(string? typographyTheme)
    {
        return CreateTypographyResources(typographyTheme, MarkdownTypographySizes.Normal);
    }

    public static ResourceDictionary CreateTypographyResources(string? typographyTheme, string? typographySize)
    {
        return CreateTypographyResources(typographyTheme, typographySize, includePalette: true);
    }

    private static ResourceDictionary CreateTypographyResources(
        string? typographyTheme,
        string? typographySize,
        bool includePalette)
    {
        var normalizedTheme = NormalizeTypographyTheme(typographyTheme);
        var resources = LoadTypographyResources(normalizedTheme);
        return CreateSizedTypographyResources(resources, typographySize, normalizedTheme, includePalette);
    }

    public static MarkdownExportStyle CreateExportStyle(string? typographyTheme)
    {
        return CreateExportStyle(typographyTheme, MarkdownTypographySizes.Normal);
    }

    public static MarkdownExportStyle CreateExportStyle(
        string? typographyTheme,
        string? typographySize,
        ThemeVariant? themeVariant = null)
    {
        var exportResources = CreateExportResources();
        ApplyTypographyResources(
            exportResources.Resources,
            CreateTypographyResources(typographyTheme, typographySize, includePalette: true));
        return MarkdownExportStyle.FromResources(exportResources, themeVariant);
    }

    public static MarkdownExportStyle CreateExportStyle(
        ResourceDictionary typographyResources,
        ThemeVariant? themeVariant = null)
    {
        var exportResources = CreateExportResources();
        ApplyTypographyResources(
            exportResources.Resources,
            CreateSizedTypographyResources(
                typographyResources,
                MarkdownTypographySizes.Normal,
                null,
                includePalette: true));
        return MarkdownExportStyle.FromResources(exportResources, themeVariant);
    }

    private static MarkdownThemes CreateExportResources()
    {
        return new MarkdownThemes();
    }

    private static ResourceDictionary LoadTypographyResources(string? typographyTheme)
    {
        return MarkdownTypographyThemeRegistry.Create(typographyTheme);
    }

    private void ApplyDefaultTypographyResources()
    {
        ApplyTypographyResources(
            Resources,
            CreateTypographyResources(_typographyTheme, _typographySize, includePalette: false));
    }

    private static void ApplyViewerTypographyResources(MarkdownViewer viewer)
    {
        var typographyTheme = GetConfiguredTypographyTheme(viewer);
        var typographySize = GetConfiguredTypographySize(viewer);

        RemoveTypographyResources(viewer.Resources);

        var inheritedSize = GetInheritedTypographySize(viewer);
        var targetSize = typographySize ?? inheritedSize;
        var typographyResources = CreateViewerTypographyResources(viewer, typographyTheme, targetSize);

        if (typographyResources is null)
        {
            viewer.Rerender();
            return;
        }

        ApplyTypographyResources(viewer.Resources, typographyResources);
        viewer.Rerender();
    }

    private static ResourceDictionary? CreateViewerTypographyResources(
        MarkdownViewer viewer,
        string? typographyTheme,
        string typographySize)
    {
        if (typographyTheme is not null)
        {
            return CreateTypographyResources(typographyTheme, typographySize, includePalette: true);
        }

        if (TryGetInheritedTypographyBaseResources(viewer, out var baseResources))
        {
            if (GetInheritedTypographyTheme(viewer) is { } inheritedTheme)
            {
                return CreateTypographyResources(inheritedTheme, typographySize, includePalette: true);
            }

            return CreateSizedTypographyResources(
                CloneResourceDictionary(baseResources),
                typographySize,
                null,
                includePalette: true);
        }

        return IsCompactTypographySize(typographySize)
            ? CreateInheritedSizeResources(viewer, typographySize)
            : null;
    }

    private static string? GetConfiguredTypographyTheme(MarkdownViewer viewer)
    {
        var typographyTheme = viewer.GetValue(MarkdownViewer.TypographyThemeProperty);
        return string.IsNullOrWhiteSpace(typographyTheme) ? null : typographyTheme.Trim();
    }

    private static string? GetConfiguredTypographySize(MarkdownViewer viewer)
    {
        var typographySize = viewer.GetValue(MarkdownViewer.TypographySizeProperty);
        return string.IsNullOrWhiteSpace(typographySize) ? null : typographySize.Trim();
    }

    private static void RemoveTypographyResources(IResourceDictionary targetResources)
    {
        if (AppliedResources.TryGetValue(targetResources, out var applied)
            && applied.Resources is not null)
        {
            targetResources.MergedDictionaries.Remove(applied.Resources);
            applied.Resources = null;
        }

        foreach (var key in TypographyResourceKeys)
        {
            targetResources.Remove(key);
        }
    }

    private static ResourceDictionary CreateInheritedSizeResources(MarkdownViewer viewer, string typographySize)
    {
        var normalizedSize = NormalizeTypographySize(typographySize);
        var sizeResources = new ResourceDictionary
        {
            [MarkdownStyleKeys.TypographySizeResource] = normalizedSize
        };

        if (IsCompactTypographySize(normalizedSize))
        {
            foreach (var compactResource in new CompactTypographyResources())
            {
                sizeResources[compactResource.Key] = compactResource.Value;
            }

            return sizeResources;
        }

        if (string.Equals(normalizedSize, MarkdownTypographySizes.Relaxed, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var relaxedResource in new RelaxedTypographyResources())
            {
                sizeResources[relaxedResource.Key] = relaxedResource.Value;
            }

            return sizeResources;
        }

        CopyTypographySizeResources(sizeResources, new CommonTypographyResources(), viewer.ActualThemeVariant);

        if (TryGetInheritedTypographyBaseResources(viewer, out var baseResources))
        {
            CopyTypographySizeResources(sizeResources, baseResources, viewer.ActualThemeVariant);
        }

        return sizeResources;
    }

    private static string GetInheritedTypographySize(StyledElement element)
    {
        return TryFindStringResource(element, MarkdownStyleKeys.TypographySizeResource)
            ?? MarkdownTypographySizes.Normal;
    }

    private static string? GetInheritedTypographyTheme(StyledElement element)
    {
        return TryFindStringResource(element, MarkdownStyleKeys.TypographyThemeResource);
    }

    private static string? TryFindStringResource(StyledElement element, string key)
    {
        return element.TryFindResource(key, element.ActualThemeVariant, out var value)
            && value is string text
            && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
    }

    private static bool TryGetInheritedTypographyBaseResources(StyledElement element, out ResourceDictionary resources)
    {
        if (element.TryFindResource(
                MarkdownStyleKeys.TypographyBaseResourcesResource,
                element.ActualThemeVariant,
                out var value)
            && value is ResourceDictionary resourceDictionary)
        {
            resources = resourceDictionary;
            return true;
        }

        resources = null!;
        return false;
    }

    private static string NormalizeTypographyTheme(string? typographyTheme)
    {
        return MarkdownTypographyThemeRegistry.Normalize(typographyTheme);
    }

    private static string NormalizeTypographySize(string? typographySize)
    {
        // 三档尺寸（规则 §11）：紧凑 13 / 正常 15 / 宽松 17。
        return NormalizeTypographySize(typographySize, out _);
    }

    private static string NormalizeTypographySize(string? typographySize, out bool isCompact)
    {
        var value = typographySize?.Trim();
        if (string.Equals(value, MarkdownTypographySizes.Relaxed, StringComparison.OrdinalIgnoreCase))
        {
            isCompact = false;
            return MarkdownTypographySizes.Relaxed;
        }
        isCompact = !string.Equals(value, MarkdownTypographySizes.Normal, StringComparison.OrdinalIgnoreCase);
        return isCompact ? MarkdownTypographySizes.Compact : MarkdownTypographySizes.Normal;
    }

    private static bool IsCompactTypographySize(string? typographySize)
    {
        return string.Equals(typographySize?.Trim(), MarkdownTypographySizes.Compact, StringComparison.OrdinalIgnoreCase);
    }

    private static ResourceDictionary CreateSizedTypographyResources(ResourceDictionary typographyResources, string? typographySize)
    {
        return CreateSizedTypographyResources(typographyResources, typographySize, null, includePalette: false);
    }

    private static ResourceDictionary CreateSizedTypographyResources(
        ResourceDictionary typographyResources,
        string? typographySize,
        string? typographyTheme,
        bool includePalette)
    {
        var normalizedSize = NormalizeTypographySize(typographySize);
        var sizedResources = new ResourceDictionary();
        sizedResources.MergedDictionaries.Add(new CommonTypographyResources());
        sizedResources.MergedDictionaries.Add(includePalette
            ? typographyResources
            : CreateLayoutOnlyResources(typographyResources));

        if (IsCompactTypographySize(normalizedSize))
        {
            foreach (var compactResource in new CompactTypographyResources())
            {
                sizedResources[compactResource.Key] = compactResource.Value;
            }
        }
        if (string.Equals(normalizedSize, MarkdownTypographySizes.Relaxed, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var relaxedResource in new RelaxedTypographyResources())
            {
                sizedResources[relaxedResource.Key] = relaxedResource.Value;
            }
        }

        // Apply the registered factory once, then scale its layout values. Recreating the
        // catalog entry here discarded host overrides and replaced each article's body size.
        if (ArticleTypographyResolver.Find(typographyTheme) is { } article)
        {
            var scale = normalizedSize switch
            {
                MarkdownTypographySizes.Compact => .87d,
                MarkdownTypographySizes.Relaxed => 1.13d,
                _ => 1d
            };
            sizedResources["CodeWFMarkdownListSpacing"] = 0d;
            sizedResources["CodeWFMarkdownListMargin"] = new Thickness(0, 0, 0, 8 * scale);
            sizedResources["CodeWFMarkdownListItemMargin"] = new Thickness(0, 1.5 * scale, 0, 1.5 * scale);
            sizedResources["CodeWFMarkdownListFirstParagraphMargin"] = new Thickness(0);
            sizedResources["CodeWFMarkdownTableCellMinHeight"] = 24.75 * scale;
            // 单元格字号/行高不再单独缩小：渲染层直接绑定正文 ParagraphFontSize/LineHeight（与段落同源）。
            foreach (var entry in typographyResources)
            {
                sizedResources[entry.Key] = entry.Value switch
                {
                    double value => value * scale,
                    Thickness thickness => new Thickness(
                        thickness.Left * scale,
                        thickness.Top * scale,
                        thickness.Right * scale,
                        thickness.Bottom * scale),
                    _ => entry.Value,
                };
            }
        }

        sizedResources[MarkdownStyleKeys.TypographyThemeResource] = typographyTheme ?? string.Empty;
        sizedResources[MarkdownStyleKeys.TypographySizeResource] = normalizedSize;
        sizedResources[MarkdownStyleKeys.TypographyBaseResourcesResource] = typographyResources;

        return sizedResources;
    }

    private static ResourceDictionary CreateLayoutOnlyResources(ResourceDictionary source)
    {
        var target = new ResourceDictionary();

        foreach (var resource in source)
        {
            if (!IsPaletteResourceKey(resource.Key))
            {
                target[resource.Key] = resource.Value;
            }
        }

        foreach (var themeDictionary in source.ThemeDictionaries)
        {
            if (themeDictionary.Value is not ResourceDictionary resourceDictionary)
            {
                target.ThemeDictionaries[themeDictionary.Key] = themeDictionary.Value;
                continue;
            }

            var filtered = CreateLayoutOnlyResources(resourceDictionary);
            if (HasResources(filtered))
            {
                target.ThemeDictionaries[themeDictionary.Key] = filtered;
            }
        }

        foreach (var mergedDictionary in source.MergedDictionaries)
        {
            if (mergedDictionary is not ResourceDictionary resourceDictionary)
            {
                target.MergedDictionaries.Add(mergedDictionary);
                continue;
            }

            var filtered = CreateLayoutOnlyResources(resourceDictionary);
            if (HasResources(filtered))
            {
                target.MergedDictionaries.Add(filtered);
            }
        }

        return target;
    }

    private static ResourceDictionary CloneResourceDictionary(ResourceDictionary source)
    {
        var target = new ResourceDictionary();

        foreach (var resource in source)
        {
            target[resource.Key] = resource.Value;
        }

        foreach (var themeDictionary in source.ThemeDictionaries)
        {
            target.ThemeDictionaries[themeDictionary.Key] = themeDictionary.Value is ResourceDictionary resourceDictionary
                ? CloneResourceDictionary(resourceDictionary)
                : themeDictionary.Value;
        }

        foreach (var mergedDictionary in source.MergedDictionaries)
        {
            target.MergedDictionaries.Add(mergedDictionary is ResourceDictionary resourceDictionary
                ? CloneResourceDictionary(resourceDictionary)
                : mergedDictionary);
        }

        return target;
    }

    private static bool IsPaletteResourceKey(object? key)
    {
        return key is string resourceKey && PaletteResourceKeys.Contains(resourceKey);
    }

    private static bool HasResources(ResourceDictionary resources)
    {
        return resources.Any()
               || resources.ThemeDictionaries.Count > 0
               || resources.MergedDictionaries.Count > 0;
    }

    private static void CopyTypographySizeResources(
        ResourceDictionary targetResources,
        ResourceDictionary sourceResources,
        ThemeVariant themeVariant)
    {
        foreach (var compactResource in new CompactTypographyResources())
        {
            if (sourceResources.TryGetResource(compactResource.Key, themeVariant, out var value))
            {
                targetResources[compactResource.Key] = value;
            }
        }
    }

    private static void RefreshMarkdownViewerTypographyResources(Application application)
    {
        switch (application.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                foreach (var window in desktop.Windows)
                {
                    RefreshMarkdownViewerTypographyResources(window);
                }

                break;
            case ISingleViewApplicationLifetime singleView when singleView.MainView is StyledElement root:
                RefreshMarkdownViewerTypographyResources(root);
                break;
        }
    }

    private static void RefreshMarkdownViewerTypographyResources(StyledElement element)
    {
        var viewers = new HashSet<MarkdownViewer>();

        if (element is MarkdownViewer viewer)
        {
            viewers.Add(viewer);
        }

        if (element is ILogical logical)
        {
            foreach (var childViewer in logical.GetLogicalDescendants().OfType<MarkdownViewer>())
            {
                viewers.Add(childViewer);
            }
        }

        if (element is Visual visual)
        {
            foreach (var childViewer in visual.GetVisualDescendants().OfType<MarkdownViewer>())
            {
                viewers.Add(childViewer);
            }
        }

        foreach (var childViewer in viewers)
        {
            ApplyViewerTypographyResources(childViewer);
        }
    }

    private sealed class AppliedTypographyResources
    {
        public ResourceDictionary? Resources { get; set; }
    }
}
