using CodeWF.Tools.Helpers;
using Vex.Core.Models;

namespace Vex.Core.Services;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string ThemeKey = nameof(AppSettings.ThemeKey);
    private const string TypographyKey = nameof(AppSettings.TypographyKey);
    private const string IsCompactLayout = nameof(AppSettings.IsCompactLayout);
    private const string CultureName = nameof(AppSettings.CultureName);
    private const string IsSidebarVisible = nameof(AppSettings.IsSidebarVisible);
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
            _settings = update(Current);
            Save(_settings);
            return _settings;
        }
    }

    private static AppSettings Load()
    {
        var configPath = AppConfigHelper.GetDefaultConfigPath();
        return new AppSettings
        {
            ThemeKey = Get<string>(configPath, ThemeKey),
            TypographyKey = Get<string>(configPath, TypographyKey),
            IsCompactLayout = Get<bool?>(configPath, IsCompactLayout),
            CultureName = Get<string>(configPath, CultureName),
            IsSidebarVisible = Get<bool?>(configPath, IsSidebarVisible),
            IsStatusBarVisible = Get<bool?>(configPath, IsStatusBarVisible),
            IsPreviewVisible = Get<bool?>(configPath, IsPreviewVisible),
            IsSourceMode = Get<bool?>(configPath, IsSourceMode),
            IsAlwaysOnTop = Get<bool?>(configPath, IsAlwaysOnTop),
            SelectedSidebarTabIndex = Get<int?>(configPath, SelectedSidebarTabIndex),
            EditorZoom = Get<double?>(configPath, EditorZoom),
            ShowLineNumbers = Get<bool?>(configPath, ShowLineNumbers),
            HasSeenOnboardingGuide = Get<bool?>(configPath, HasSeenOnboardingGuide),
            LastWorkspaceFolderPath = Get<string>(configPath, LastWorkspaceFolderPath),
            WindowWidth = Get<double?>(configPath, WindowWidth),
            WindowHeight = Get<double?>(configPath, WindowHeight),
            IsMcpServerEnabled = Get<bool?>(configPath, IsMcpServerEnabled),
            McpServerHost = Get<string>(configPath, McpServerHost),
            McpServerPort = Get<int?>(configPath, McpServerPort),
            McpAuthorizationToken = Get<string>(configPath, McpAuthorizationToken),
            McpAccessScope = Get<string>(configPath, McpAccessScope),
            McpAllowedWorkspacePath = Get<string>(configPath, McpAllowedWorkspacePath),
            McpRequireConfirmation = Get<bool?>(configPath, McpRequireConfirmation)
        };
    }

    private static void Save(AppSettings settings)
    {
        try
        {
            var configPath = AppConfigHelper.GetDefaultConfigPath();
            AppConfigHelper.Set(configPath, ThemeKey, settings.ThemeKey);
            AppConfigHelper.Set(configPath, TypographyKey, settings.TypographyKey);
            AppConfigHelper.Set(configPath, IsCompactLayout, settings.IsCompactLayout);
            AppConfigHelper.Set(configPath, CultureName, settings.CultureName);
            AppConfigHelper.Set(configPath, IsSidebarVisible, settings.IsSidebarVisible);
            AppConfigHelper.Set(configPath, IsStatusBarVisible, settings.IsStatusBarVisible);
            AppConfigHelper.Set(configPath, IsPreviewVisible, settings.IsPreviewVisible);
            AppConfigHelper.Set(configPath, IsSourceMode, settings.IsSourceMode);
            AppConfigHelper.Set(configPath, IsAlwaysOnTop, settings.IsAlwaysOnTop);
            AppConfigHelper.Set(configPath, SelectedSidebarTabIndex, settings.SelectedSidebarTabIndex);
            AppConfigHelper.Set(configPath, EditorZoom, settings.EditorZoom);
            AppConfigHelper.Set(configPath, ShowLineNumbers, settings.ShowLineNumbers);
            AppConfigHelper.Set(configPath, HasSeenOnboardingGuide, settings.HasSeenOnboardingGuide);
            AppConfigHelper.Set(configPath, LastWorkspaceFolderPath, settings.LastWorkspaceFolderPath);
            AppConfigHelper.Set(configPath, WindowWidth, settings.WindowWidth);
            AppConfigHelper.Set(configPath, WindowHeight, settings.WindowHeight);
            AppConfigHelper.Set(configPath, IsMcpServerEnabled, settings.IsMcpServerEnabled);
            AppConfigHelper.Set(configPath, McpServerHost, settings.McpServerHost);
            AppConfigHelper.Set(configPath, McpServerPort, settings.McpServerPort);
            AppConfigHelper.Set(configPath, McpAuthorizationToken, settings.McpAuthorizationToken);
            AppConfigHelper.Set(configPath, McpAccessScope, settings.McpAccessScope);
            AppConfigHelper.Set(configPath, McpAllowedWorkspacePath, settings.McpAllowedWorkspacePath);
            AppConfigHelper.Set(configPath, McpRequireConfirmation, settings.McpRequireConfirmation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // 用户设置保存失败不应打断当前编辑流程，后续可接入统一错误提示。
        }
    }

    private static T? Get<T>(string configPath, string key)
    {
        return AppConfigHelper.TryGet<T>(configPath, key, out var value)
            ? value
            : default;
    }
}
