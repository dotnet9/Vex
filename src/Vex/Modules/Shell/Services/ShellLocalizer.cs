using System.Globalization;
using Lang.Avalonia;

namespace Vex.Modules.Shell.Services;

public sealed class ShellLocalizer : IShellLocalizer
{
    public string Get(string key)
    {
        // Shell 层所有运行时文案统一走这里，避免 ViewModel 直接散落 I18nManager 调用。
        return I18nManager.Instance.GetResource(key);
    }

    public string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
