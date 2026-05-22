using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CodeWF.Markdown.Themes;
using ReactiveUI;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellAppearanceViewModel : ReactiveObject
{
    private readonly IAppLocalizer _localizer;
    private readonly IEditorAppearanceState _editorAppearanceState;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IShellStatusPublisher _statusPublisher;
    private bool _isCompactLayout;
    private bool _isInitializing = true;
    private ThemeOption? _selectedTheme;
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
        TypographyOptions = new ObservableCollection<TypographyOption>(
            MarkdownTypographyThemes.All.Select(theme => new TypographyOption(theme.Name, theme.Key)));
        LanguageOptions = new ObservableCollection<LanguageOption>
        {
            new("zh-CN", "简体中文", "中文（简体）"),
            new("zh-Hant", "繁體中文", "中文（繁體）"),
            new("en-US", "English", "English"),
            new("ja-JP", "日本語", "日本語")
        };

        var settings = _settingsStore.Current;
        _selectedTheme = FindTheme(settings.ThemeKey)
                         ?? FindTheme("system")
                         ?? ThemeOptions.FirstOrDefault();
        _selectedTypography = FindTypography(settings.TypographyKey)
                              ?? FindTypography(MarkdownTypographyThemes.Simple)
                              ?? TypographyOptions.FirstOrDefault();
        _isCompactLayout = settings.IsCompactLayout ?? false;
        _selectedLanguage = FindLanguage(GetInitialCulture(settings.CultureName));
        if (_selectedTheme is not null)
        {
            _themeService.ApplyTheme(_selectedTheme);
        }

        if (_selectedLanguage is not null)
        {
            _localizer.SetCulture(_selectedLanguage.CultureName);
        }

        PublishTypographyState();
        _isInitializing = false;
    }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }

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
                _themeService.ApplyTheme(value);
                PersistAppearanceSettings();
            }
        }
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
