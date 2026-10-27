using CodeWF.Toolkit.Lang;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>自定义排版编辑（对应原型 typography-dialog）：名称 + 双明暗强调/装饰色 + 字体 + 行距。</summary>
public sealed class ShellTypographyEditModel(string? editingKey, string name = "") : ReactiveObject, IDialogContext
{
    private static string L(string key) => I18nManager.Instance.GetResource(key) ?? string.Empty;

    private string _name = name;
    private string _accentLight = "#24766B";
    private string _accentDark = "#4CBFA8";
    private string _decoLight = "#C4704A";
    private string _decoDark = "#D89A6E";
    private string _font = "sans";
    private double _leading = 1.9;
    private string _report = L(VexL.WBTypographyReport);

    public string? EditingKey { get; } = editingKey;
    public string Title => L(EditingKey is null ? VexL.WBTypographyAdd : VexL.WBTypographyEdit);
    public string Report { get => _report; private set => this.RaiseAndSetIfChanged(ref _report, value); }
    public (string Name, string AccentLight, string AccentDark, string DecoLight, string DecoDark, string Font, double Leading)? Result { get; private set; }

    public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }
    public string AccentLight { get => _accentLight; set => this.RaiseAndSetIfChanged(ref _accentLight, value); }
    public string AccentDark { get => _accentDark; set => this.RaiseAndSetIfChanged(ref _accentDark, value); }
    public string DecoLight { get => _decoLight; set => this.RaiseAndSetIfChanged(ref _decoLight, value); }
    public string DecoDark { get => _decoDark; set => this.RaiseAndSetIfChanged(ref _decoDark, value); }
    public string Font { get => _font; set => this.RaiseAndSetIfChanged(ref _font, value); }
    public double Leading { get => _leading; set => this.RaiseAndSetIfChanged(ref _leading, value); }

    public event EventHandler<object?>? RequestClose;
    public void Cancel() => RequestClose?.Invoke(this, false);
    public void Close() => Cancel();

    public void Apply()
    {
        var colors = new[] { AccentLight, AccentDark, DecoLight, DecoDark };
        // 只校验长度和井号会让 #GGGGGG 通过，之后库在烘焙画刷时才抛异常——错误点远离输入框。
        if (colors.Any(value => !ShellPaletteEditModel.TryParseHex(value.Trim().ToUpperInvariant())))
        {
            Report = L(VexL.CustomColorInvalid);
            this.RaisePropertyChanged(nameof(Report));
            return;
        }
        Result = (Name.Trim() is { Length: > 0 } name ? name : L(VexL.CustomDefaultName),
            AccentLight.Trim(), AccentDark.Trim(), DecoLight.Trim(), DecoDark.Trim(), Font, Leading);
        RequestClose?.Invoke(this, true);
    }
}
