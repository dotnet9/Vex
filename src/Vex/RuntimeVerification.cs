using System.Globalization;
using System.Runtime.CompilerServices;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Vex.Core.Services;

namespace Vex;

/// <summary>显式验证正式原生发布产物，只读输出资源，并在指定目录验证偏好持久化。</summary>
internal static class RuntimeVerification
{
    public static void Run(string directory)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException("Runtime verification requires a NativeAOT executable.");
        // 走实际启动集成入口，确认 Windows 自检不会重新注册用户取消的打开方式。
        if (OperatingSystem.IsWindows()) DefaultFileOpeningService.Configure();
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var resources = Path.Combine(AppContext.BaseDirectory, "I18n");
        if (OperatingSystem.IsMacOS() && !Directory.Exists(resources))
            resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
        var manager = I18nManager.Instance;
        manager.Register(new JsonLangPlugin { ResourceFolder = resources }, CultureInfo.GetCultureInfo("en-US"));
        var labels = new HashSet<string>();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
        {
            if (!File.Exists(Path.Combine(resources, "Vex", culture + ".json")))
                throw new InvalidOperationException("Published language file is missing: " + culture);
            manager.Culture = CultureInfo.GetCultureInfo(culture);
            var text = manager.GetResource(VexL.MenuLanguage);
            if (string.IsNullOrWhiteSpace(text) || text == VexL.MenuLanguage)
                throw new InvalidOperationException("Published language resources are missing: " + culture);
            labels.Add(text);
            foreach (var key in new[] { VexL.InsertTable, VexL.TableRows, VexL.TableColumns, VexL.TableHeaderHint })
                if (manager.GetResource(key) is not { Length: > 0 } translated || translated == key)
                    throw new InvalidOperationException("Published table dialog translation is missing: " + culture + "/" + key);
        }
        if (labels.Count != 4) throw new InvalidOperationException("All four published languages must switch independently.");
        var config = Path.Combine(root, "runtime-" + Guid.NewGuid().ToString("N") + ".config");
        File.WriteAllText(config, "<configuration><appSettings /></configuration>");
        AppContext.SetData("APP_CONFIG_FILE", config);
        new AppSettingsStore().Update(settings => settings with { ThemeKey = "system", PaletteKey = "aurora", CultureName = "ja-JP" });
        var saved = new AppSettingsStore().Current;
        if (saved.ThemeKey != "system" || saved.PaletteKey != "aurora" || saved.CultureName != "ja-JP")
            throw new InvalidOperationException("Native appearance preferences did not round-trip.");
        var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; languages=4; tableDialog=localized; preferences=isolated";
        File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
        Console.WriteLine(result);
    }
}
