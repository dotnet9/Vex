using Avalonia.Media;
using CodeWF.Toolkit.Lang;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>自定义配色编辑（对应原型 palette-dialog）：名称/描述 + 双明暗各 4 色，其余语义键由引擎派生。</summary>
public sealed class ShellPaletteEditModel(string? editingKey, string name = "", string note = "",
    string[]? colors = null) : ReactiveObject, IDialogContext
{
    private static string L(string key) => I18nManager.Instance.GetResource(key) ?? string.Empty;

    // 四语解析放在取值时而非静态构造：界面语言可在会话内切换，冻结会让浮层残留旧语言。
    public static string[] ColorLabels =>
    [
        L(VexL.WBPaletteColorPaper), L(VexL.WBPaletteColorSurface), L(VexL.WBPaletteColorInk), L(VexL.WBPaletteColorAccent),
        L(VexL.WBPaletteColorPaper), L(VexL.WBPaletteColorSurface), L(VexL.WBPaletteColorInk), L(VexL.WBPaletteColorAccent),
    ];
    public static string[] GroupLabels => [L(VexL.WBPaletteGroupLight), L(VexL.WBPaletteGroupDark)];
    private static readonly string[] DefaultColors =
        ["#FFFFFF", "#F4F4F5", "#18181B", "#4F46E5", "#101014", "#1B1B20", "#E4E4E9", "#818CF8"];

    public string? EditingKey { get; } = editingKey;
    public string Title => L(EditingKey is null ? VexL.WBPaletteAdd : VexL.WBPaletteEdit);
    public string Report { get; private set; } = L(VexL.WBPaletteReport);
    public (string Name, string Note, string[] Colors)? Result { get; private set; }

    // 复制一份：编辑框按索引直接回写 Colors，共享调用方数组会污染列表里的内置配色。
    public string[] Colors { get; } = (colors is { Length: 8 } ? colors : DefaultColors).ToArray();
    public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }
    public string Note { get => _note; set => this.RaiseAndSetIfChanged(ref _note, value); }

    public event EventHandler<object?>? RequestClose;
    public void Cancel() => RequestClose?.Invoke(this, false);
    public void Close() => Cancel();

    public void Apply()
    {
        var colors = Colors.Select(value => value.Trim().ToUpperInvariant()).ToArray();
        if (colors.Any(value => !TryParseHex(value)))
        {
            Report = L(VexL.CustomColorInvalid);
            this.RaisePropertyChanged(nameof(Report));
            return;
        }
        Result = (Name.Trim() is { Length: > 0 } name ? name : L(VexL.CustomDefaultName), Note.Trim(), colors);
        RequestClose?.Invoke(this, true);
    }

    // Color.Parse 对非法十六进制是抛异常而非返回 false，不接住就会让「保存并应用」直接崩溃。
    public static bool TryParseHex(string value)
    {
        if (value.Length != 7 || value[0] != '#') return false;
        try { Color.Parse(value); return true; }
        catch (FormatException) { return false; }
    }

    private string _name = name;
    private string _note = note;
}
