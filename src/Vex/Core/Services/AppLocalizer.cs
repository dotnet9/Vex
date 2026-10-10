using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using CodeWF.Avalonia.Lang;

namespace Vex.Core.Services;

public sealed class AppLocalizer : IAppLocalizer
{
    public event EventHandler<EventArgs>? CultureChanged
    {
        add => I18nManager.Instance.CultureChanged += value;
        remove => I18nManager.Instance.CultureChanged -= value;
    }

    public CultureInfo Culture => I18nManager.Instance.Culture ?? CultureInfo.CurrentCulture;

    public void SetCulture(string cultureName)
    {
        I18nManager.Instance.Culture = new CultureInfo(cultureName);
        ApplyThirdPartyCulture(cultureName);
    }

    public string Get(string key)
    {
        // 应用级运行时文案统一走这里，Shell 与 Workspace 都不直接依赖 I18nManager。
        return I18nManager.Instance.GetResource(key);
    }

    public string Format(string key, params object?[] args)
    {
        return string.Format(Culture, Get(key), args);
    }

    private static readonly IReadOnlyDictionary<string, ResourceDictionary> SemiCultures =
        new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = new Semi.Avalonia.Locale.en_us(),
            ["ja-JP"] = new Semi.Avalonia.Locale.ja_jp(),
            ["zh-CN"] = new Semi.Avalonia.Locale.zh_cn(),
            ["zh-Hant"] = new Semi.Avalonia.Locale.zh_cn()
        };

    private static readonly IReadOnlyDictionary<string, ResourceDictionary> UrsaCultures =
        new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = new Ursa.Themes.Semi.Locale.en_us(),
            ["ja-JP"] = new Ursa.Themes.Semi.Locale.en_us(),
            ["zh-CN"] = new Ursa.Themes.Semi.Locale.zh_cn(),
            ["zh-Hant"] = new Ursa.Themes.Semi.Locale.zh_cn()
        };

    private static void ApplyThirdPartyCulture(string cultureName)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        if (SemiCultures.TryGetValue(cultureName, out var semiCulture))
        {
            MergeCultureResources(app.Resources, semiCulture);
        }

        if (UrsaCultures.TryGetValue(cultureName, out var ursaCulture))
        {
            MergeCultureResources(app.Resources, ursaCulture);
        }
    }

    private static void MergeCultureResources(IResourceDictionary appResources, ResourceDictionary cultureResources)
    {
        foreach (var item in cultureResources)
        {
            if (appResources.ContainsKey(item.Key))
            {
                appResources.Remove(item.Key);
            }

            appResources.Add(item);
        }
    }
}
