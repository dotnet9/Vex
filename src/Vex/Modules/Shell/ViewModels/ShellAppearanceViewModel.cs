using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Themes;
using CodeWF.Avalonia.Controls.Themes;
using ReactiveUI;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellAppearanceViewModel : ReactiveObject
{
    private static string[] TypographyKeys() => CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.All.Select(item => item.Key!).ToArray();

    private readonly IAppLocalizer _localizer;
    private readonly IEditorAppearanceState _editorAppearanceState;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IShellStatusPublisher _statusPublisher;
    private bool _isCompactLayout;
    private bool _isInitializing = true;
    private ThemeOption? _selectedTheme;
    private PaletteOption? _selectedPalette;
    private TypographyOption? _selectedTypography;
    private LanguageOption? _selectedLanguage;
    private string _selectedDensity;
    private string _radiusMode = "standard";
    private double _radiusScale = 1;
    private string _markdownDensity;

    public ShellAppearanceViewModel(
        IAppLocalizer localizer,
        IEditorAppearanceState editorAppearanceState,
        IAppSettingsStore settingsStore,
        IThemeService themeService,
        IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
        _editorAppearanceState = editorAppearanceState;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _statusPublisher = statusPublisher;

        ThemeOptions = new ObservableCollection<ThemeOption>(_themeService.GetThemeOptions());
        PaletteOptions = new ObservableCollection<PaletteOption>(_themeService.GetPaletteOptions());
        TypographyOptions = new ObservableCollection<TypographyOption>(
            TypographyKeys().Select(theme => new TypographyOption(theme, theme)));
        LanguageOptions = new ObservableCollection<LanguageOption>
        {
            new("zh-CN", _localizer.Get(VexL.LanguageSimplifiedChinese), _localizer.Get(VexL.LanguageSimplifiedChinese)),
            new("zh-Hant", _localizer.Get(VexL.LanguageTraditionalChinese), _localizer.Get(VexL.LanguageTraditionalChinese)),
            new("en-US", _localizer.Get(VexL.LanguageEnglish), _localizer.Get(VexL.LanguageEnglish)),
            new("ja-JP", _localizer.Get(VexL.LanguageJapanese), _localizer.Get(VexL.LanguageJapanese))
        };

        var settings = _settingsStore.Current;
        _selectedTheme = FindTheme(settings.ThemeKey)
                         ?? FindTheme("system")
                         ?? ThemeOptions.FirstOrDefault();
        // 旧设置里可能残留已下线的配色键；此时与 App.ApplyInitialTheme 一致回落到默认靛蓝，
        // 而不是列表首项（石墨），否则启动期会先渲染靛蓝再被覆盖成石墨。
        _selectedPalette = PaletteOptions.FirstOrDefault(item =>
            item.Key.Equals(settings.PaletteKey ?? settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
            ?? PaletteOptions.FirstOrDefault(item => item.Key.Equals("indigo", StringComparison.OrdinalIgnoreCase))
            ?? PaletteOptions[0];
        _selectedTypography = FindTypography(settings.TypographyKey)
                              ?? FindTypography(MarkdownTypographyThemes.Simple)
                              ?? TypographyOptions.FirstOrDefault();
        _isCompactLayout = settings.IsCompactLayout ?? false;
        _selectedDensity = settings.UiDensity is "compact" or "relaxed" ? settings.UiDensity : "normal";
        _radiusMode = settings.RadiusMode is "compact" or "square" ? settings.RadiusMode : "standard";
        _radiusScale = RadiusScale(_radiusMode);
        ApplyUiDensity(_selectedDensity);
        // 未落盘的旧设置用 IsCompactLayout 迁移出初始档，保持升级前后正文大小一致。
        _markdownDensity = settings.MdSize is "compact" or "normal" or "relaxed"
            ? settings.MdSize
            : (settings.IsCompactLayout == true ? "compact" : "normal");
        ApplyDiagramDensity(_markdownDensity);
        _selectedLanguage = FindLanguage(GetInitialCulture(settings.CultureName));
        if (_selectedTheme is not null)
        {
            _themeService.ApplyTheme(_selectedTheme, _selectedPalette);
        }

        if (_selectedLanguage is not null)
        {
            _localizer.SetCulture(_selectedLanguage.CultureName);
        }

        PublishTypographyState();
        _isInitializing = false;
        PersistAppearanceSettings();
        _localizer.CultureChanged += OnCultureChanged;
    }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }

    public ObservableCollection<PaletteOption> PaletteOptions { get; }

    public ObservableCollection<TypographyOption> TypographyOptions { get; }

    public ObservableCollection<LanguageOption> LanguageOptions { get; }

    public bool IsCompactLayout
    {
        get => _isCompactLayout;
        set
        {
            if (SetProperty(ref _isCompactLayout, value))
            {
                OnPropertyChanged(nameof(CurrentTypographySize));
                PublishTypographyState();
                PersistAppearanceSettings();
            }
        }
    }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value) && value is not null)
            {
                OnPropertyChanged(nameof(ThemeMode));
                _themeService.ApplyTheme(value, SelectedPalette!);
                PersistAppearanceSettings();
            }
        }
    }

    /// <summary>标题栏三态按钮与视图菜单共用同一份主题设置。</summary>
    public StudioThemeMode ThemeMode
    {
        get => SelectedTheme?.Mode ?? StudioThemeMode.System;
        set => SelectedTheme = ThemeOptions.First(option => option.Mode == value);
    }

    public PaletteOption? SelectedPalette
    {
        get => _selectedPalette;
        set
        {
            if (SetProperty(ref _selectedPalette, value) && value is not null)
            {
                _themeService.ApplyTheme(SelectedTheme!, value);
                PersistAppearanceSettings();
            }
        }
    }

    /// <summary>界面密度三档（对应原型 data-density）：切档即把 density.css 的显式值表写入应用资源。</summary>
    public string SelectedDensity
    {
        get => _selectedDensity;
        set
        {
            if (value is not ("compact" or "normal" or "relaxed")) return;
            if (SetProperty(ref _selectedDensity, value))
            {
                ApplyUiDensity(value);
                PersistAppearanceSettings();
            }
        }
    }

    /// <summary>
    /// 界面圆角三档（原型 data-radius，prototype.css:19-20）：标准 ×1、紧凑 ×0.5、直角 ×0。
    /// 只缩放圆角令牌，控件高宽一律不动。
    /// </summary>
    public string RadiusMode
    {
        get => _radiusMode;
        set
        {
            if (value is not ("standard" or "compact" or "square")) return;
            if (SetProperty(ref _radiusMode, value))
            {
                _radiusScale = RadiusScale(value);
                ApplyUiDensity(SelectedDensity);
                PersistAppearanceSettings();
            }
        }
    }

    private static double RadiusScale(string mode) => mode switch { "compact" => .5, "square" => 0, _ => 1 };

    /// <summary>UI 密度令牌（规则 §6 显式值表）：字号/控件高/间距/圆角写入应用资源供全控件消费。</summary>
    private void ApplyUiDensity(string density)
    {
        if (global::Avalonia.Application.Current is not { } app) return;
        var (caption, body, subtitle, title, display, hSm, hMd, hLg, padSm, padMd, padLg, radSm, radMd, radLg, rowH, icon) = density switch
        {
            "compact" => (11.0, 12.0, 14.0, 17.0, 22.0, 24.0, 28.0, 34.0, 4.0, 8.0, 12.0, 4.0, 6.0, 8.0, 26.0, 14.0),
            "relaxed" => (12.0, 15.0, 17.0, 21.0, 28.0, 32.0, 36.0, 44.0, 8.0, 12.0, 18.0, 8.0, 10.0, 12.0, 36.0, 18.0),
            _ => (12.0, 14.0, 16.0, 20.0, 26.0, 28.0, 32.0, 38.0, 6.0, 10.0, 14.0, 6.0, 8.0, 10.0, 30.0, 16.0),
        };
        app.Resources["VexDensityFontSizeCaption"] = caption;
        app.Resources["VexDensityFontSizeBody"] = body;
        // 原型多处取 calc(var(--fs-body) - 1px)（MCP 二级页签、设置里的分段行），单列一档令牌免得各处写死 13。
        app.Resources["VexDensityFontSizeBodySm"] = body - 1;
        app.Resources["VexDensityFontSizeSubtitle"] = subtitle;
        app.Resources["VexDensityFontSizeTitle"] = title;
        app.Resources["VexDensityFontSizeDisplay"] = display;
        app.Resources["VexDensityControlHeightSm"] = hSm;
        app.Resources["VexDensityControlHeightMd"] = hMd;
        app.Resources["VexDensityControlHeightLg"] = hLg;
        app.Resources["VexDensityPaddingSm"] = padSm;
        app.Resources["VexDensityPaddingMd"] = padMd;
        app.Resources["VexDensityPaddingLg"] = padLg;
        // 圆角令牌必须以 CornerRadius 装箱：样式里的 DynamicResource 直传属性存储，不做 double 隐式转换，
        // 否则 VexZoomDock 模板的气球圆角会在装载时报 Unable to cast Double to CornerRadius。
        // 三档圆角（原型 --radius-scale）只乘圆角，不碰控件高宽；胶囊在直角档同样归零（原型 --vex-radius-pill 也乘 scale）。
        app.Resources["VexDensityCornerRadiusSm"] = new Avalonia.CornerRadius(radSm * _radiusScale);
        app.Resources["VexDensityCornerRadiusMd"] = new Avalonia.CornerRadius(radMd * _radiusScale);
        app.Resources["VexDensityCornerRadiusLg"] = new Avalonia.CornerRadius(radLg * _radiusScale);
        app.Resources["VexDensityCornerRadiusPill"] = new Avalonia.CornerRadius(999 * _radiusScale);
        // 原型另有两档不吃密度、只吃 --radius-scale：--vex-radius-xs 基线 4（代码块、图标按钮、设置左栏），
        // --vex-radius-popover 基线 16（偏好浮层这类整窗弹层）。
        app.Resources["VexDensityCornerRadiusXs"] = new Avalonia.CornerRadius(4 * _radiusScale);
        app.Resources["VexDensityCornerRadiusPopover"] = new Avalonia.CornerRadius(16 * _radiusScale);
        app.Resources["VexDensityRowHeight"] = rowH;
        app.Resources["VexDensityIconSize"] = icon;
        // 缩放坞（原型 .vex-zoomdock）几何：坞高=控件中档、内缩=pad-sm 减 1px 描边、条宽=坞高×1.875、
        // 还原钮=坞高减 9、其左分隔线再减 8（原型 top/bottom 各 4）、图标=密度图标加 2。
        app.Resources["VexZoomDockHeight"] = hMd;
        app.Resources["VexZoomDockPadding"] = new Avalonia.Thickness(padSm - 1, 0);
        app.Resources["VexZoomDockGap"] = padSm;
        app.Resources["VexZoomDockSliderWidth"] = hMd * 1.875;
        app.Resources["VexZoomDockResetHeight"] = hMd - 9;
        app.Resources["VexZoomDockDividerHeight"] = hMd - 17;
        app.Resources["VexZoomDockIconSize"] = icon + 2;
        // 分段控件（原型 .vex-segmented）几何：--seg-scale 就是 control-h-sm ÷ 28（design/assets/prototype/segmented.css:4-6），
        // 各宿主的基准高与内距都乘它，切「界面密度」时轨道、指示块和文字一起缩，不需要逐处改数值。
        var segScale = hSm / 28;
        app.Resources["VexSegmentedScale"] = segScale;
        // 原型 .vex-segmented{--seg-inset:calc(var(--pad-sm)/2)}（segmented.css:7）：轨道内缩随密度三档走 2/3/4，
        // 指示块贴着同一内缩落位（thumb top/bottom），所以只改轨道 Padding 就两者一起缩。
        app.Resources["VexSegmentedInset"] = new Avalonia.Thickness(padSm / 2);
        app.Resources["VexSegmentedHeightView"] = 26 * segScale;
        app.Resources["VexSegmentedPaddingView"] = new Avalonia.Thickness(14, 3 * segScale);
        app.Resources["VexSegmentedPaddingMcp"] = new Avalonia.Thickness(10, 4 * segScale);
        app.Resources["VexSegmentedHeightDensity"] = 22 * segScale;
        app.Resources["VexSegmentedPaddingDensity"] = new Avalonia.Thickness(10, 2 * segScale);
        app.Resources["VexSegmentedHeightRenderDensity"] = 24 * segScale;
        // 原型 .vex-pref-segmented > button：4×密度/6 内距、正文减 1 号字（prototype.css:215）。
        app.Resources["VexSegmentedPaddingPref"] = new Avalonia.Thickness(6, 4 * segScale);
    }

    /// <summary>Markdown 渲染密度三档（对应原型 data-mdsize，基准正文 13/15/17）。</summary>
    public string MarkdownDensity
    {
        get => _markdownDensity;
        set
        {
            if (value is not ("compact" or "normal" or "relaxed")) return;
            if (SetProperty(ref _markdownDensity, value))
            {
                OnPropertyChanged(nameof(CurrentTypographySize));
                PublishTypographyState();
                PersistAppearanceSettings();
                ApplyDiagramDensity(value);
            }
        }
    }

    /// <summary>把渲染密度传导给 Mermaid 图表（Mermaider FontSize/间距，规则 §11 图表与正文同档）。</summary>
    private static void ApplyDiagramDensity(string density)
    {
        if (global::Avalonia.Application.Current is not { } app) return;
        var (fontSize, nodeSpacing, layerSpacing) = density switch
        {
            "compact" => (12.0, 22.0, 24.0),
            "relaxed" => (15.5, 34.0, 38.0),
            _ => (13.5, 28.0, 30.0),
        };
        app.Resources["CodeWFMarkdownMermaidFontSize"] = fontSize;
        app.Resources["CodeWFMarkdownMermaidNodeSpacing"] = nodeSpacing;
        app.Resources["CodeWFMarkdownMermaidLayerSpacing"] = layerSpacing;
    }

    /// <summary>新增（key 为空）或编辑用户排版：注册后刷新目录并选中，随设置持久化。</summary>
    public string RegisterCustomTypography(string key, string name, string accentLight, string accentDark,
        string decorationLight, string decorationDark, string font, double leading, string headingDecoration = "leftBar")
    {
        key = string.IsNullOrWhiteSpace(key) ? "user:" + Guid.NewGuid().ToString("N")[..8] : key;
        var typography = CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.CreateCustom(
            key, name, accentLight, accentDark, decorationLight, decorationDark, font, leading, headingDecoration);
        CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Register(typography);
        ReloadTypographyOptions(key);
        PersistCustomTypographies();
        return key;
    }

    public bool RemoveCustomTypography(string key)
    {
        if (!CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Remove(key)) return false;
        ReloadTypographyOptions("Simple");
        PersistCustomTypographies();
        return true;
    }

    private void ReloadTypographyOptions(string preferredKey)
    {
        var current = SelectedTypography?.Key;
        TypographyOptions.Clear();
        foreach (var option in CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.All
                     .Select(item => new TypographyOption(item.Key!, item.Key!)))
        {
            TypographyOptions.Add(option);
        }
        // 先广播目录变化再改选中项：卡片弹层按目录重建自己的条目表，若先改选中，弹层会拿着
        // 「刚被删掉的那张卡」重绘徽章，而那条排版已经查不回来了。
        OnPropertyChanged(nameof(TypographyOptions));
        SelectedTypography = TypographyOptions.FirstOrDefault(item => item.Key == current)
            ?? TypographyOptions.FirstOrDefault(item => item.Key == preferredKey)
            ?? TypographyOptions.FirstOrDefault();
    }

    private void PersistCustomTypographies()
    {
        // 装饰档位不回读 Headings（边框/内距折算后无法唯一还原成档名），所以随记录一起存档位键本身。
        var records = CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.All
            .Where(item => item.Key != null && item.Key.StartsWith("user:", StringComparison.Ordinal))
            .Select(item => string.Join('|', item.Key, item.Name,
                item.LightText, item.DarkText, item.LightDecoration, item.DarkDecoration, item.Font,
                item.Leading.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HeadingDecorationOf(item.Headings[1])));
        _settingsStore.Update(settings => settings with { CustomTypographies = string.Join(';', records) });
    }

    /// <summary>取用户排版的 H2 装饰档名，供编辑浮层回填当前选择。</summary>
    public string HeadingDecoration(string key) =>
        CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Find(key) is { } typography
            ? HeadingDecorationOf(typography.Headings[1]) : "leftBar";

    /// <summary>把 H2 的边框/内距/色块/圆角折回原型那四档档名；库里 <c>CreateCustom</c> 只按这四档写入。</summary>
    private static string HeadingDecorationOf(CodeWF.Avalonia.Markdown.Themes.ArticleHeadingStyle h2) =>
        h2.Border switch
        {
            "0,0,0,2" => "underline",
            "4,0,0,0" => "leftBar",
            _ => h2.Tint > 0 ? "tint" : "none",
        };

    /// <summary>启动时恢复用户排版。</summary>
    public void LoadCustomTypographies(string? persisted)
    {
        if (string.IsNullOrWhiteSpace(persisted)) return;
        foreach (var record in persisted.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = record.Split('|');
            // 8 段是 H2 装饰选择器落地前的记录：那时的渲染等价于「无装饰」，按此补齐而不是重新派生一档。
            if ((fields.Length != 8 && fields.Length != 9) || !fields[0].StartsWith("user:", StringComparison.Ordinal)) continue;
            try
            {
                var typography = CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.CreateCustom(
                    fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6],
                    double.Parse(fields[7], System.Globalization.CultureInfo.InvariantCulture),
                    fields.Length == 9 ? fields[8] : "none");
                CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Register(typography);
            }
            catch { /* 单条损坏不阻塞其余排版 */ }
        }
        ReloadTypographyOptions(SelectedTypography?.Key ?? "Simple");
    }

    public void SelectPaletteByKey(string? key)
    {
        if (PaletteOptions.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } option)
            SelectedPalette = option;
    }

    /// <summary>新增（key 为空）或编辑用户配色：注册后刷新目录并选中。</summary>
    public string RegisterCustomPalette(string key, string name, string note,
        string lightPaper, string lightSurface, string lightInk, string lightAccent,
        string darkPaper, string darkSurface, string darkInk, string darkAccent)
    {
        var saved = _themeService.RegisterCustomPalette(key, name, note,
            lightPaper, lightSurface, lightInk, lightAccent, darkPaper, darkSurface, darkInk, darkAccent);
        ReloadPaletteOptions();
        SelectPaletteByKey(saved);
        return saved;
    }

    /// <summary>删除用户配色；删除当前选中项时回退默认配色。</summary>
    public bool RemoveCustomPalette(string key)
    {
        if (!_themeService.RemoveCustomPalette(key)) return false;
        ReloadPaletteOptions();
        if (SelectedPalette?.Key == key) SelectPaletteByKey("indigo");
        return true;
    }

    private void ReloadPaletteOptions()
    {
        _themeService.InvalidatePaletteOptions();
        var current = SelectedPalette?.Key;
        PaletteOptions.Clear();
        foreach (var option in _themeService.GetPaletteOptions()) PaletteOptions.Add(option);
        SelectedPalette = PaletteOptions.FirstOrDefault(item => item.Key == current)
            ?? PaletteOptions.FirstOrDefault(item => item.Key == "indigo")
            ?? PaletteOptions.FirstOrDefault();
        OnPropertyChanged(nameof(PaletteOptions));
    }

    public TypographyOption? SelectedTypography
    {
        get => _selectedTypography;
        set
        {
            if (SetProperty(ref _selectedTypography, value))
            {
                OnPropertyChanged(nameof(CurrentTypographyTheme));
                PublishTypographyState();
                PersistAppearanceSettings();
            }
        }
    }

    public string? CurrentTypographyTheme => SelectedTypography?.Key;

    public string CurrentTypographySize => MarkdownDensity switch
    {
        "compact" => MarkdownTypographySizes.Compact,
        "relaxed" => MarkdownTypographySizes.Relaxed,
        _ => MarkdownTypographySizes.Normal,
    };

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value is not null)
            {
                _localizer.SetCulture(value.CultureName);
                PersistAppearanceSettings();
                _statusPublisher.PublishResourceFormat(VexL.StatusLanguageSwitched, value.DisplayName);
            }
        }
    }

    /// <summary>全局切语时同步语言选择，避免同项重选无法切回。</summary>
    private void OnCultureChanged(object? sender, EventArgs args)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnCultureChanged(sender, args));
            return;
        }

        // 直接同步选中项，避免进入业务设置器再次切语或写入偏好。
        SetProperty(ref _selectedLanguage, FindLanguage(_localizer.Culture.Name), nameof(SelectedLanguage));
    }

    public void ToggleCompactLayout()
    {
        IsCompactLayout = !IsCompactLayout;
    }

    public void SelectTheme(ThemeOption? theme)
    {
        if (theme is not null)
        {
            SelectedTheme = theme;
        }
    }

    public void SelectThemeByKey(string? key)
    {
        var theme = ThemeOptions.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        SelectTheme(theme);
    }

    public void SelectTypography(TypographyOption? typography)
    {
        if (typography is not null)
        {
            SelectedTypography = typography;
        }
    }

    public void SelectTypographyByKey(string? key)
    {
        var typography = TypographyOptions.FirstOrDefault(item => item.Key?.Equals(key, StringComparison.OrdinalIgnoreCase) == true);
        SelectTypography(typography);
    }

    public void SelectLanguage(LanguageOption? language)
    {
        if (language is not null)
        {
            SelectedLanguage = language;
        }
    }

    public void SelectLanguageByCulture(string? cultureName)
    {
        var language = LanguageOptions.FirstOrDefault(item => item.CultureName.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
        SelectLanguage(language);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }

    private void PublishTypographyState()
    {
        _editorAppearanceState.UpdateTypography(CurrentTypographyTheme, CurrentTypographySize);
    }

    private void PersistAppearanceSettings()
    {
        if (_isInitializing)
        {
            return;
        }

        _settingsStore.Update(settings => settings with
        {
            ThemeKey = SelectedTheme?.Key,
            PaletteKey = SelectedPalette?.Key,
            TypographyKey = SelectedTypography?.Key,
            UiDensity = SelectedDensity,
            RadiusMode = RadiusMode,
            MdSize = MarkdownDensity,
            IsCompactLayout = IsCompactLayout,
            CultureName = SelectedLanguage?.CultureName
        });
    }

    private ThemeOption? FindTheme(string? key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? null
            : ThemeOptions.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private TypographyOption? FindTypography(string? key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? null
            : TypographyOptions.FirstOrDefault(item => item.Key?.Equals(key, StringComparison.OrdinalIgnoreCase) == true);
    }

    private LanguageOption? FindLanguage(string? cultureName)
    {
        return string.IsNullOrWhiteSpace(cultureName)
            ? null
            : LanguageOptions.FirstOrDefault(item => item.CultureName.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
    }

    private string GetInitialCulture(string? savedCultureName)
    {
        if (FindLanguage(savedCultureName) is { } savedLanguage)
        {
            return savedLanguage.CultureName;
        }

        var osCulture = CultureInfo.CurrentUICulture;
        if (FindLanguage(osCulture.Name) is { } exactLanguage)
        {
            return exactLanguage.CultureName;
        }

        if (osCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return IsTraditionalChineseCulture(osCulture.Name) ? "zh-Hant" : "zh-CN";
        }

        return osCulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase)
            ? "ja-JP"
            : "en-US";
    }

    private static bool IsTraditionalChineseCulture(string cultureName)
    {
        return cultureName.Contains("Hant", StringComparison.OrdinalIgnoreCase)
               || cultureName.EndsWith("-TW", StringComparison.OrdinalIgnoreCase)
               || cultureName.EndsWith("-HK", StringComparison.OrdinalIgnoreCase)
               || cultureName.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
