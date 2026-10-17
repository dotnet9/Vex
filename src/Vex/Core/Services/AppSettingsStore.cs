using CodeWF.Toolkit.Files.Helpers;
using Vex.Core.Models;

namespace Vex.Core.Services;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string ThemeKey = nameof(AppSettings.ThemeKey);
    private const string PaletteKey = nameof(AppSettings.PaletteKey);
    private const string TypographyKey = nameof(AppSettings.TypographyKey);
    private const string IsCompactLayout = nameof(AppSettings.IsCompactLayout);
    private const string CultureName = nameof(AppSettings.CultureName);
    private const string IsSidebarVisible = nameof(AppSettings.IsSidebarVisible);
    private const string SidebarWidth = nameof(AppSettings.SidebarWidth);
    private const string IsStatusBarVisible = nameof(AppSettings.IsStatusBarVisible);
    private const string IsPreviewVisible = nameof(AppSettings.IsPreviewVisible);
    private const string IsSourceMode = nameof(AppSettings.IsSourceMode);
    private const string IsAlwaysOnTop = nameof(AppSettings.IsAlwaysOnTop);
    private const string SelectedSidebarTabIndex = nameof(AppSettings.SelectedSidebarTabIndex);
    private const string EditorZoom = nameof(AppSettings.EditorZoom);
    private const string ShowLineNumbers = nameof(AppSettings.ShowLineNumbers);
    private const string HasSeenOnboardingGuide = nameof(AppSettings.HasSeenOnboardingGuide);
    private const string LastWorkspaceFolderPath = nameof(AppSettings.LastWorkspaceFolderPath);
    private const string WindowWidth = nameof(AppSettings.WindowWidth);
    private const string WindowHeight = nameof(AppSettings.WindowHeight);
    private const string IsMcpServerEnabled = nameof(AppSettings.IsMcpServerEnabled);
    private const string McpServerHost = nameof(AppSettings.McpServerHost);
    private const string McpServerPort = nameof(AppSettings.McpServerPort);
    private const string McpAuthorizationToken = nameof(AppSettings.McpAuthorizationToken);
    private const string McpAccessScope = nameof(AppSettings.McpAccessScope);
    private const string McpAllowedWorkspacePath = nameof(AppSettings.McpAllowedWorkspacePath);
    private const string McpRequireConfirmation = nameof(AppSettings.McpRequireConfirmation);
    private const string WorkbenchView = nameof(AppSettings.WorkbenchView);
    private const string WorkspaceLayout = nameof(AppSettings.WorkspaceLayout);
    private const string EditMode = nameof(AppSettings.EditMode);
    private const string MindMapSummaryLength = nameof(AppSettings.MindMapSummaryLength);
    private const string MindMapShowParagraphs = nameof(AppSettings.MindMapShowParagraphs);
    private const string MindMapShowCode = nameof(AppSettings.MindMapShowCode);
    private const string MindMapShowTables = nameof(AppSettings.MindMapShowTables);
    private const string MindMapShowLists = nameof(AppSettings.MindMapShowLists);
    private const string EnableAutoPair = nameof(AppSettings.EnableAutoPair);
    private const string EnableMotion = nameof(AppSettings.EnableMotion);
    private const string IsFocusMode = nameof(AppSettings.IsFocusMode);
    private const string IsTypewriterMode = nameof(AppSettings.IsTypewriterMode);

    private readonly object _syncRoot = new();
    private AppSettings? _settings;

    public AppSettings Current
    {
        get
        {
            lock (_syncRoot)
            {
                _settings ??= Load();
                return _settings;
            }
        }
    }

    public AppSettings Update(Func<AppSettings, AppSettings> update)
    {
        lock (_syncRoot)
        {
            // 以磁盘最新配置为基准做读-改-写合并：Update 调用点均为 with{指定键} 的部分更新，
            // 其余键保留磁盘现状。此前以内存快照全量覆盖保存，多实例并存或配置文件被外部
            // 修改后，任一实例退出都会用过期快照覆盖全部键（MCP 开关/Token 因此丢失）。
            var latest = Load();
            _settings = update(latest);
            Save(_settings);
            return _settings;
        }
    }

    private static AppSettings Load()
    {
        var configuration = AppConfigHelper.ReadAll();
        return new AppSettings
        {
            ThemeKey = Get<string>(configuration, ThemeKey),
            PaletteKey = Get<string>(configuration, PaletteKey),
            TypographyKey = Get<string>(configuration, TypographyKey),
            IsCompactLayout = Get<bool?>(configuration, IsCompactLayout),
            CultureName = Get<string>(configuration, CultureName),
            IsSidebarVisible = Get<bool?>(configuration, IsSidebarVisible),
            SidebarWidth = Get<double?>(configuration, SidebarWidth),
            IsStatusBarVisible = Get<bool?>(configuration, IsStatusBarVisible),
            IsPreviewVisible = Get<bool?>(configuration, IsPreviewVisible),
            IsSourceMode = Get<bool?>(configuration, IsSourceMode),
            IsAlwaysOnTop = Get<bool?>(configuration, IsAlwaysOnTop),
            SelectedSidebarTabIndex = Get<int?>(configuration, SelectedSidebarTabIndex),
            EditorZoom = Get<double?>(configuration, EditorZoom),
            ShowLineNumbers = Get<bool?>(configuration, ShowLineNumbers),
            HasSeenOnboardingGuide = Get<bool?>(configuration, HasSeenOnboardingGuide),
            LastWorkspaceFolderPath = Get<string>(configuration, LastWorkspaceFolderPath),
            WindowWidth = Get<double?>(configuration, WindowWidth),
            WindowHeight = Get<double?>(configuration, WindowHeight),
            IsMcpServerEnabled = Get<bool?>(configuration, IsMcpServerEnabled),
            McpServerHost = Get<string>(configuration, McpServerHost),
            McpServerPort = Get<int?>(configuration, McpServerPort),
            McpAuthorizationToken = Get<string>(configuration, McpAuthorizationToken),
            McpAccessScope = Get<string>(configuration, McpAccessScope),
            McpAllowedWorkspacePath = Get<string>(configuration, McpAllowedWorkspacePath),
            McpRequireConfirmation = Get<bool?>(configuration, McpRequireConfirmation),
            WorkbenchView = Get<string>(configuration, WorkbenchView),
            WorkspaceLayout = Get<string>(configuration, WorkspaceLayout),
            EditMode = Get<string>(configuration, EditMode),
            MindMapSummaryLength = Get<int?>(configuration, MindMapSummaryLength),
            MindMapShowParagraphs = Get<bool?>(configuration, MindMapShowParagraphs),
            MindMapShowCode = Get<bool?>(configuration, MindMapShowCode),
            MindMapShowTables = Get<bool?>(configuration, MindMapShowTables),
            MindMapShowLists = Get<bool?>(configuration, MindMapShowLists),
            EnableAutoPair = Get<bool?>(configuration, EnableAutoPair),
            EnableMotion = Get<bool?>(configuration, EnableMotion),
            IsFocusMode = Get<bool?>(configuration, IsFocusMode),
            IsTypewriterMode = Get<bool?>(configuration, IsTypewriterMode)
        };
    }

    private static void Save(AppSettings settings)
    {
        try
        {
            AppConfigHelper.SetMany(new Dictionary<string, object?>
            {
                [ThemeKey] = settings.ThemeKey,
                [PaletteKey] = settings.PaletteKey,
                [TypographyKey] = settings.TypographyKey,
                [IsCompactLayout] = settings.IsCompactLayout,
                [CultureName] = settings.CultureName,
                [IsSidebarVisible] = settings.IsSidebarVisible,
                [SidebarWidth] = settings.SidebarWidth,
                [IsStatusBarVisible] = settings.IsStatusBarVisible,
                [IsPreviewVisible] = settings.IsPreviewVisible,
                [IsSourceMode] = settings.IsSourceMode,
                [IsAlwaysOnTop] = settings.IsAlwaysOnTop,
                [SelectedSidebarTabIndex] = settings.SelectedSidebarTabIndex,
                [EditorZoom] = settings.EditorZoom,
                [ShowLineNumbers] = settings.ShowLineNumbers,
                [HasSeenOnboardingGuide] = settings.HasSeenOnboardingGuide,
                [LastWorkspaceFolderPath] = settings.LastWorkspaceFolderPath,
                [WindowWidth] = settings.WindowWidth,
                [WindowHeight] = settings.WindowHeight,
                [IsMcpServerEnabled] = settings.IsMcpServerEnabled,
                [McpServerHost] = settings.McpServerHost,
                [McpServerPort] = settings.McpServerPort,
                [McpAuthorizationToken] = settings.McpAuthorizationToken,
                [McpAccessScope] = settings.McpAccessScope,
                [McpAllowedWorkspacePath] = settings.McpAllowedWorkspacePath,
                [McpRequireConfirmation] = settings.McpRequireConfirmation,
                [WorkbenchView] = settings.WorkbenchView,
                [WorkspaceLayout] = settings.WorkspaceLayout,
                [EditMode] = settings.EditMode,
                [MindMapSummaryLength] = settings.MindMapSummaryLength,
                [MindMapShowParagraphs] = settings.MindMapShowParagraphs,
                [MindMapShowCode] = settings.MindMapShowCode,
                [MindMapShowTables] = settings.MindMapShowTables,
                [MindMapShowLists] = settings.MindMapShowLists,
                [EnableAutoPair] = settings.EnableAutoPair,
                [EnableMotion] = settings.EnableMotion,
                [IsFocusMode] = settings.IsFocusMode,
                [IsTypewriterMode] = settings.IsTypewriterMode
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // 用户设置保存失败不应打断当前编辑流程，后续可接入统一错误提示。
        }
    }

    private static T? Get<T>(IReadOnlyDictionary<string, string> configuration, string key)
    {
        return configuration.TryGet<T>(key, out var value)
            ? value
            : default;
    }
}
