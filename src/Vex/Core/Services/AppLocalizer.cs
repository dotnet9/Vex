using System.Globalization;
using Lang.Avalonia;

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
}
