using CodeWF.Toolkit.Files.Helpers;
using Vex.Core.Models;

namespace Vex.Core.Services;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string ThemeKey = nameof(AppSettings.ThemeKey);
    private const string PaletteKey = nameof(AppSettings.PaletteKey);
    private const string TypographyKey = nameof(AppSettings.TypographyKey);
    private const string UiDensity = nameof(AppSettings.UiDensity);
    private const string MdSize = nameof(AppSettings.MdSize);
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
    private const string IsOutlineVisible = nameof(AppSettings.IsOutlineVisible);
    private const string OutlineWidth = nameof(AppSettings.OutlineWidth);
    private const string FileListView = nameof(AppSettings.FileListView);
    private const string FileSortDescending = nameof(AppSettings.FileSortDescending);
    private const string CollapsedFolders = nameof(AppSettings.CollapsedFolders);
    private const string CollapsedOutlineSections = nameof(AppSettings.CollapsedOutlineSections);

    private const string RestoreWorkspace = nameof(AppSettings.RestoreWorkspace);
    private const string AutoSave = nameof(AppSettings.AutoSave);
    private const string AutoSaveDelay = nameof(AppSettings.AutoSaveDelay);
    private const string RecoveryDrafts = nameof(AppSettings.RecoveryDrafts);
    private const string DefaultEncoding = nameof(AppSettings.DefaultEncoding);
    private const string DefaultNewline = nameof(AppSettings.DefaultNewline);
    private const string BodyFontSize = nameof(AppSettings.BodyFontSize);
    private const string CodeFontSize = nameof(AppSettings.CodeFontSize);
    private const string IndentSize = nameof(AppSettings.IndentSize);
    private const string CodeWrap = nameof(AppSettings.CodeWrap);
    private const string BodyFont = nameof(AppSettings.BodyFont);
    private const string SyntaxMath = nameof(AppSettings.SyntaxMath);
    private const string SyntaxChemistry = nameof(AppSettings.SyntaxChemistry);
    private const string SyntaxDiagrams = nameof(AppSettings.SyntaxDiagrams);
    private const string SyntaxSubscript = nameof(AppSettings.SyntaxSubscript);
    private const string SyntaxHighlight = nameof(AppSettings.SyntaxHighlight);
    private const string SyntaxAutoLinks = nameof(AppSettings.SyntaxAutoLinks);
    private const string SyntaxSoftBreaks = nameof(AppSettings.SyntaxSoftBreaks);
    private const string ImageCopy = nameof(AppSettings.ImageCopy);
    private const string ImageRelative = nameof(AppSettings.ImageRelative);
    private const string ImageFolder = nameof(AppSettings.ImageFolder);
    private const string WordCountVisible = nameof(AppSettings.WordCountVisible);
    private const string ContentZoom = nameof(AppSettings.ContentZoom);
    private const string ExportFormat = nameof(AppSettings.ExportFormat);
    private const string ExportFolder = nameof(AppSettings.ExportFolder);
    private const string ExportOpen = nameof(AppSettings.ExportOpen);
    private const string HtmlStyle = nameof(AppSettings.HtmlStyle);
    private const string WatermarkPreview = nameof(AppSettings.WatermarkPreview);
    private const string WatermarkText = nameof(AppSettings.WatermarkText);
    private const string WatermarkOpacity = nameof(AppSettings.WatermarkOpacity);
    private const string WatermarkFontSize = nameof(AppSettings.WatermarkFontSize);
    private const string WatermarkColor = nameof(AppSettings.WatermarkColor);
    private const string WatermarkWeight = nameof(AppSettings.WatermarkWeight);
    private const string CustomPalettes = nameof(AppSettings.CustomPalettes);
    private const string MindMapLayout = nameof(AppSettings.MindMapLayout);
    private const string MindMapStyle = nameof(AppSettings.MindMapStyle);
    private const string CustomTypographies = nameof(AppSettings.CustomTypographies);
    private const string PdfSize = nameof(AppSettings.PdfSize);
    private const string PdfMargin = nameof(AppSettings.PdfMargin);

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
            IsOutlineVisible = Get<bool?>(configuration, IsOutlineVisible),
            OutlineWidth = Get<double?>(configuration, OutlineWidth),
            FileListView = Get<bool?>(configuration, FileListView),
            FileSortDescending = Get<bool?>(configuration, FileSortDescending),
            CollapsedFolders = Get<string>(configuration, CollapsedFolders),
            CollapsedOutlineSections = Get<string>(configuration, CollapsedOutlineSections),
            RestoreWorkspace = Get<bool?>(configuration, RestoreWorkspace),
            AutoSave = Get<bool?>(configuration, AutoSave),
            AutoSaveDelay = Get<int?>(configuration, AutoSaveDelay),
            RecoveryDrafts = Get<bool?>(configuration, RecoveryDrafts),
            DefaultEncoding = Choice(configuration, DefaultEncoding, "utf-8", "utf-8-bom", "GB18030"),
            DefaultNewline = Choice(configuration, DefaultNewline, "system", "LF", "CRLF"),
            BodyFontSize = Get<double?>(configuration, BodyFontSize),
            CodeFontSize = Get<double?>(configuration, CodeFontSize),
            IndentSize = Get<int?>(configuration, IndentSize),
            CodeWrap = Get<bool?>(configuration, CodeWrap),
            BodyFont = Choice(configuration, BodyFont, "system", "serif", "mono"),
            SyntaxMath = Get<bool?>(configuration, SyntaxMath),
            SyntaxChemistry = Get<bool?>(configuration, SyntaxChemistry),
            SyntaxDiagrams = Get<bool?>(configuration, SyntaxDiagrams),
            SyntaxSubscript = Get<bool?>(configuration, SyntaxSubscript),
            SyntaxHighlight = Get<bool?>(configuration, SyntaxHighlight),
            SyntaxAutoLinks = Get<bool?>(configuration, SyntaxAutoLinks),
            SyntaxSoftBreaks = Get<bool?>(configuration, SyntaxSoftBreaks),
            ImageCopy = Get<bool?>(configuration, ImageCopy),
            ImageRelative = Get<bool?>(configuration, ImageRelative),
            ImageFolder = Get<string>(configuration, ImageFolder) ?? "assets",
            WordCountVisible = Get<bool?>(configuration, WordCountVisible),
            ContentZoom = Get<double?>(configuration, ContentZoom),
            ExportFormat = Choice(configuration, ExportFormat, "HTML", "PDF", "Markdown", "PNG", "Word"),
            ExportFolder = Get<string>(configuration, ExportFolder),
            ExportOpen = Get<bool?>(configuration, ExportOpen),
            HtmlStyle = Get<bool?>(configuration, HtmlStyle),
            WatermarkPreview = Get<bool?>(configuration, WatermarkPreview),
            WatermarkText = Get<string?>(configuration, WatermarkText) ?? "维刻 Vex",
            WatermarkOpacity = Get<double?>(configuration, WatermarkOpacity),
            WatermarkFontSize = Get<double?>(configuration, WatermarkFontSize),
            WatermarkColor = Get<string?>(configuration, WatermarkColor),
            WatermarkWeight = Get<int?>(configuration, WatermarkWeight),
            CustomPalettes = Get<string?>(configuration, CustomPalettes),
            MindMapLayout = Get<string?>(configuration, MindMapLayout),
            MindMapStyle = Get<string?>(configuration, MindMapStyle),
            CustomTypographies = Get<string?>(configuration, CustomTypographies),
            PdfSize = Choice(configuration, PdfSize, "A4", "Letter", "A5"),
            PdfMargin = Get<double?>(configuration, PdfMargin),
            ThemeKey = Get<string>(configuration, ThemeKey),
            PaletteKey = Get<string>(configuration, PaletteKey),
            TypographyKey = Get<string>(configuration, TypographyKey),
            UiDensity = Choice(configuration, UiDensity, "normal", "compact", "normal", "relaxed"),
            MdSize = Choice(configuration, MdSize, "normal", "compact", "normal", "relaxed"),
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
                [IsOutlineVisible] = settings.IsOutlineVisible,
                [OutlineWidth] = settings.OutlineWidth,
                [FileListView] = settings.FileListView,
                [FileSortDescending] = settings.FileSortDescending,
                [CollapsedFolders] = settings.CollapsedFolders,
                [CollapsedOutlineSections] = settings.CollapsedOutlineSections,
                [RestoreWorkspace] = settings.RestoreWorkspace,
                [AutoSave] = settings.AutoSave,
                [AutoSaveDelay] = settings.AutoSaveDelay,
                [RecoveryDrafts] = settings.RecoveryDrafts,
                [DefaultEncoding] = settings.DefaultEncoding,
                [DefaultNewline] = settings.DefaultNewline,
                [BodyFontSize] = settings.BodyFontSize,
                [CodeFontSize] = settings.CodeFontSize,
                [IndentSize] = settings.IndentSize,
                [CodeWrap] = settings.CodeWrap,
                [BodyFont] = settings.BodyFont,
                [SyntaxMath] = settings.SyntaxMath,
                [SyntaxChemistry] = settings.SyntaxChemistry,
                [SyntaxDiagrams] = settings.SyntaxDiagrams,
                [SyntaxSubscript] = settings.SyntaxSubscript,
                [SyntaxHighlight] = settings.SyntaxHighlight,
                [SyntaxAutoLinks] = settings.SyntaxAutoLinks,
                [SyntaxSoftBreaks] = settings.SyntaxSoftBreaks,
                [ImageCopy] = settings.ImageCopy,
                [ImageRelative] = settings.ImageRelative,
                [ImageFolder] = settings.ImageFolder,
                [WordCountVisible] = settings.WordCountVisible,
                [ContentZoom] = settings.ContentZoom,
                [ExportFormat] = settings.ExportFormat,
                [ExportFolder] = settings.ExportFolder,
                [ExportOpen] = settings.ExportOpen,
                [HtmlStyle] = settings.HtmlStyle,
                [WatermarkPreview] = settings.WatermarkPreview,
                [WatermarkText] = settings.WatermarkText,
                [WatermarkOpacity] = settings.WatermarkOpacity,
                [WatermarkFontSize] = settings.WatermarkFontSize,
                [WatermarkColor] = settings.WatermarkColor,
                [WatermarkWeight] = settings.WatermarkWeight,
                [CustomPalettes] = settings.CustomPalettes,
                [MindMapLayout] = settings.MindMapLayout,
                [MindMapStyle] = settings.MindMapStyle,
                [CustomTypographies] = settings.CustomTypographies,
                [PdfSize] = settings.PdfSize,
                [PdfMargin] = settings.PdfMargin,
                [ThemeKey] = settings.ThemeKey,
                [PaletteKey] = settings.PaletteKey,
                [TypographyKey] = settings.TypographyKey,
                [UiDensity] = settings.UiDensity,
                [MdSize] = settings.MdSize,
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

    private static string Choice(IReadOnlyDictionary<string, string> configuration, string key, string fallback, params string[] values)
    {
        var value = Get<string>(configuration, key);
        return value is not null && values.Contains(value, StringComparer.Ordinal) ? value : fallback;
    }

    private static T? Get<T>(IReadOnlyDictionary<string, string> configuration, string key)
    {
        return configuration.TryGet<T>(key, out var value)
            ? value
            : default;
    }
}
