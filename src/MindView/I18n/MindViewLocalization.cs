using CodeWF.Toolkit.Lang.Json;

namespace CodeWF.Avalonia.MindView.I18n;

/// <summary>库内嵌翻译资源，自动应用宿主同键覆盖与文化回退。</summary>
public static class MindViewLocalization
{
    private static readonly JsonLocalizer Resources = new(typeof(MindViewLocalization).Assembly);
    public static string GetResource(string key, string? cultureName = null) => Resources.GetResource(key, cultureName);
}
