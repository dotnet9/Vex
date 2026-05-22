using System.Collections.ObjectModel;
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
    private readonly IThemeService _themeService;
    private readonly IShellStatusPublisher _statusPublisher;
    private bool _isCompactLayout;
    private ThemeOption? _selectedTheme;
    private TypographyOption? _selectedTypography;
    private LanguageOption? _selectedLanguage;

    public ShellAppearanceViewModel(IAppLocalizer localizer, IThemeService themeService, IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
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

        SelectedTheme = ThemeOptions.FirstOrDefault();
        SelectedTypography = TypographyOptions.FirstOrDefault(item => item.Key == MarkdownTypographyThemes.Simple)
                             ?? TypographyOptions.FirstOrDefault();
        _selectedLanguage = LanguageOptions.FirstOrDefault(item => item.CultureName == "zh-CN");
        if (_selectedLanguage is not null)
        {
            _localizer.SetCulture(_selectedLanguage.CultureName);
        }
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

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
