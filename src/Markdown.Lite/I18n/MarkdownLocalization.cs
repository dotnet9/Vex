using CodeWF.Toolkit.Lang.Json;

namespace CodeWF.Avalonia.Markdown;

/// <summary>读取 Markdown 内嵌翻译资源，并自动应用宿主的同键覆盖。</summary>
public static class MarkdownLocalization
{
    private static readonly JsonLocalizer Resources = new(typeof(MarkdownLocalization).Assembly);

    /// <summary>按指定语言或当前界面语言查询资源；缺失时回退到库的默认语言。</summary>
    public static string GetResource(string key, string? cultureName = null) => Resources.GetResource(key, cultureName);
}
