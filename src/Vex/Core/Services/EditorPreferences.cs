using System.Runtime.CompilerServices;
using Avalonia.Media;
using CodeWF.Avalonia.Markdown;
using ReactiveUI;
using Vex.Core.Models;

namespace Vex.Core.Services;

/// <summary>应用偏好下发为各视图的独立选项，不改写 Markdown 内容。</summary>
public sealed class EditorPreferences(IAppSettingsStore settings) : ReactiveObject
{
    public event EventHandler? PreferencesChanged;
    public bool RestoreWorkspace { get => settings.Current.RestoreWorkspace ?? false; set => Set(s => s with { RestoreWorkspace = value }); }
    public bool AutoSave { get => settings.Current.AutoSave ?? false; set => Set(s => s with { AutoSave = value }); }
    public int AutoSaveDelay { get => settings.Current.AutoSaveDelay ?? 2; set => Set(s => s with { AutoSaveDelay = Math.Clamp(value, 2, 60) }); }
    public bool RecoveryDrafts { get => settings.Current.RecoveryDrafts ?? true; set => Set(s => s with { RecoveryDrafts = value }); }
    public string DefaultEncoding { get => settings.Current.DefaultEncoding ?? "utf-8"; set => Set(s => s with { DefaultEncoding = value ?? "" }); }
    public string DefaultNewline { get => settings.Current.DefaultNewline ?? "system"; set => Set(s => s with { DefaultNewline = value ?? "" }); }
    public double BodyFontSize { get => settings.Current.BodyFontSize ?? 16; set => Set(s => s with { BodyFontSize = Math.Clamp(value, 16, 24) }); }
    public double CodeFontSize { get => settings.Current.CodeFontSize ?? 12.5; set => Set(s => s with { CodeFontSize = Math.Clamp(value, 11, 20) }); }
    public int IndentSize { get => settings.Current.IndentSize ?? 2; set => Set(s => s with { IndentSize = Math.Clamp(value, 2, 8) }); }
    public bool CodeWrap { get => settings.Current.CodeWrap ?? false; set => Set(s => s with { CodeWrap = value }); }
    public string BodyFont { get => settings.Current.BodyFont ?? "system"; set => Set(s => s with { BodyFont = value ?? "" }); }
    public bool SyntaxMath { get => settings.Current.SyntaxMath ?? true; set => Set(s => s with { SyntaxMath = value }); }
    public bool SyntaxChemistry { get => settings.Current.SyntaxChemistry ?? true; set => Set(s => s with { SyntaxChemistry = value }); }
    public bool SyntaxDiagrams { get => settings.Current.SyntaxDiagrams ?? true; set => Set(s => s with { SyntaxDiagrams = value }); }
    public bool SyntaxSubscript { get => settings.Current.SyntaxSubscript ?? true; set => Set(s => s with { SyntaxSubscript = value }); }
    public bool SyntaxHighlight { get => settings.Current.SyntaxHighlight ?? true; set => Set(s => s with { SyntaxHighlight = value }); }
    public bool SyntaxAutoLinks { get => settings.Current.SyntaxAutoLinks ?? true; set => Set(s => s with { SyntaxAutoLinks = value }); }
    public bool SyntaxSoftBreaks { get => settings.Current.SyntaxSoftBreaks ?? true; set => Set(s => s with { SyntaxSoftBreaks = value }); }
    public bool ImageCopy { get => settings.Current.ImageCopy ?? true; set => Set(s => s with { ImageCopy = value }); }
    public bool ImageRelative { get => settings.Current.ImageRelative ?? true; set => Set(s => s with { ImageRelative = value }); }
    public string ImageFolder { get => settings.Current.ImageFolder ?? "assets"; set => Set(s => s with { ImageFolder = value ?? "" }); }
    public bool WordCountVisible { get => settings.Current.WordCountVisible ?? true; set => Set(s => s with { WordCountVisible = value }); }
    public bool PinEditorToolbar { get => settings.Current.PinEditorToolbar ?? true; set => Set(s => s with { PinEditorToolbar = value }); }
    public bool PinReadingToolbar { get => settings.Current.PinReadingToolbar ?? true; set => Set(s => s with { PinReadingToolbar = value }); }
    public bool PinMapToolbar { get => settings.Current.PinMapToolbar ?? true; set => Set(s => s with { PinMapToolbar = value }); }
    public double ContentZoom { get => settings.Current.ContentZoom ?? 100; set => Set(s => s with { ContentZoom = Math.Clamp(value, 50, 200) }); }
    public string ExportFormat { get => settings.Current.ExportFormat ?? "HTML"; set => Set(s => s with { ExportFormat = value ?? "" }); }
    public string ExportFolder { get => settings.Current.ExportFolder ?? ""; set => Set(s => s with { ExportFolder = value ?? "" }); }
    public bool ExportOpen { get => settings.Current.ExportOpen ?? true; set => Set(s => s with { ExportOpen = value }); }
    public bool HtmlStyle { get => settings.Current.HtmlStyle ?? true; set => Set(s => s with { HtmlStyle = value }); }
    public bool WatermarkPreview { get => settings.Current.WatermarkPreview ?? false; set => Set(s => s with { WatermarkPreview = value }); }
    public bool WatermarkExport { get => settings.Current.WatermarkExport ?? false; set => Set(s => s with { WatermarkExport = value }); }
    public string WatermarkText { get => settings.Current.WatermarkText ?? "维刻 Vex"; set => Set(s => s with { WatermarkText = string.Concat((value ?? "").EnumerateRunes().Take(60).Select(r => r.ToString())) }); }
    public double WatermarkOpacity { get => settings.Current.WatermarkOpacity ?? 12; set => Set(s => s with { WatermarkOpacity = Math.Clamp(value, 0, 50) }); }
    public string PdfSize { get => settings.Current.PdfSize ?? "A4"; set => Set(s => s with { PdfSize = value is "A5" or "Letter" ? value : "A4" }); }
    public double PdfMargin { get => settings.Current.PdfMargin ?? 20; set => Set(s => s with { PdfMargin = Math.Clamp(value, 10, 30) }); }

    public MarkdownSyntaxOptions SyntaxOptions => new(SyntaxMath, SyntaxSubscript, SyntaxHighlight,
        SyntaxAutoLinks, SyntaxSoftBreaks, SyntaxDiagrams, SyntaxChemistry);
    public FontFamily ContentFontFamily => BodyFont switch
    {
        "serif" => new FontFamily("Georgia, SimSun, Songti SC, serif"),
        "mono" => VexFonts.Code,
        _ => new FontFamily("Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC, sans-serif")
    };
    public double EffectiveBodyFontSize => BodyFontSize * ContentZoom / 100;
    public double ResolveBodyFontSize(string? typography) => (settings.Current.BodyFontSize
        ?? CodeWF.Avalonia.Markdown.Themes.ArticleTypographyCatalog.Find(typography)?.BodySize ?? 16) * ContentZoom / 100;
    public double EffectiveCodeFontSize => CodeFontSize * ContentZoom / 100;
    private void Set(Func<AppSettings, AppSettings> update, [CallerMemberName] string? name = null)
    {
        var current = settings.Current;
        if (current == update(current)) return;
        settings.Update(update);
        this.RaisePropertyChanged(name);
        this.RaisePropertyChanged(nameof(SyntaxOptions));
        this.RaisePropertyChanged(nameof(ContentFontFamily));
        this.RaisePropertyChanged(nameof(EffectiveBodyFontSize));
        this.RaisePropertyChanged(nameof(EffectiveCodeFontSize));
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }
}
