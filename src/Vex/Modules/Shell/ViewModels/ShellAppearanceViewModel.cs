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
    private static readonly string[] TypographyKeys =
    [
        MarkdownTypographyThemes.Basic,
        MarkdownTypographyThemes.Simple,
        MarkdownTypographyThemes.OrangeHeart,
        MarkdownTypographyThemes.InkBlack,
        MarkdownTypographyThemes.ColorfulPurple,
        MarkdownTypographyThemes.TenderGreen,
        MarkdownTypographyThemes.Verdant,
        MarkdownTypographyThemes.RedScarlet,
        MarkdownTypographyThemes.BlueGlow,
        MarkdownTypographyThemes.TechnologyBlue,
        MarkdownTypographyThemes.LanQing,
        MarkdownTypographyThemes.Yamabuki,
        MarkdownTypographyThemes.FrontendPeak,
        MarkdownTypographyThemes.GeekBlack,
        MarkdownTypographyThemes.RosePurple,
        MarkdownTypographyThemes.CuteGreen,
        MarkdownTypographyThemes.FullStackBlue,
        MarkdownTypographyThemes.WeChatFormat
    ];

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
            TypographyKeys.Select(theme => new TypographyOption(theme, theme)));
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
        _selectedPalette = PaletteOptions.FirstOrDefault(item =>
            item.Key.Equals(settings.PaletteKey ?? settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
            ?? PaletteOptions[0];
        _selectedTypography = FindTypography(settings.TypographyKey)
                              ?? FindTypography(MarkdownTypographyThemes.Simple)
                              ?? TypographyOptions.FirstOrDefault();
        _isCompactLayout = settings.IsCompactLayout ?? false;
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

    public void SelectPaletteByKey(string? key)
    {
        if (PaletteOptions.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } option)
            SelectedPalette = option;
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

    public string CurrentTypographySize => IsCompactLayout
        ? MarkdownTypographySizes.Small
        : MarkdownTypographySizes.Normal;

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
