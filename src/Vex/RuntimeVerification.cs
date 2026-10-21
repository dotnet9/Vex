using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Mermaider;
using Mermaider.Models;
using Vex.Core.Services;

namespace Vex;

/// <summary>显式验证正式原生发布产物，只读输出资源，并在指定目录验证偏好持久化。</summary>
internal static class RuntimeVerification
{
    public static void Run(string directory)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException("Runtime verification requires a NativeAOT executable.");
        if (!Uri.TryCreate(ProjectLinks.Website, UriKind.Absolute, out var website) || website.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Published website metadata is missing or invalid.");
        foreach (var file in new[] { "更新日志.md", "鸣谢.md", "快速开始.md" })
            _ = BundledDocuments.GetPath(file);
        var samples = Directory.GetFiles(BundledDocuments.SamplesFolder, "*.md");
        if (samples.Length != 6 || !File.Exists(Path.Combine(BundledDocuments.SamplesFolder, "图片", "山间湖畔.png")))
            throw new InvalidOperationException("Published sample documents or their offline image are missing.");
        var catalog = File.ReadAllText(Path.Combine(BundledDocuments.SamplesFolder, "Mermaid 图表图鉴.md"));
        var diagrams = Regex.Matches(catalog, @"(?m)^```mermaid\s*\r?\n([\s\S]*?)^```\s*$");
        if (diagrams.Count != 22)
            throw new InvalidOperationException("Published Mermaid catalog must contain all 22 diagram examples.");
        foreach (Match diagram in diagrams)
        {
            var svg = MermaidRenderer.RenderSvg(diagram.Groups[1].Value, new RenderOptions
            {
                Bg = "#FFFFFF", Fg = "#302C3C", Accent = "#8B7CF6", Transparent = true,
                DataPalette = ["#8B7CF6", "#D9AD53", "#70A98B", "#C583B5", "#62A5C2"]
            });
            if (XDocument.Parse(svg).Root?.Name.LocalName != "svg")
                throw new InvalidOperationException("Native Mermaid rendering did not produce an SVG diagram.");
        }
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
        var preferences = new EditorPreferences(new AppSettingsStore());
        if (preferences.DefaultEncoding != "utf-8" || preferences.DefaultNewline != "system" || preferences.BodyFont != "system"
            || preferences.ExportFormat != "HTML" || preferences.PdfSize != "A4")
            throw new InvalidOperationException("Native default preference choices did not survive real storage.");
        var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; website={ProjectLinks.Website}; languages=4; tableDialog=localized; samples={samples.Length}; mermaid={diagrams.Count}; preferences=isolated";
        File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
        Console.WriteLine(result);
    }
}
