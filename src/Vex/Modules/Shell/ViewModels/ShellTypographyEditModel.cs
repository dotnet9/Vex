using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>自定义排版编辑（对应原型 typography-dialog）：名称 + 双明暗强调/装饰色 + 字体 + 行距。</summary>
public sealed class ShellTypographyEditModel(string? editingKey, string name = "") : ReactiveObject, IDialogContext
{
    private string _name = name;
    private string _accentLight = "#24766B";
    private string _accentDark = "#4CBFA8";
    private string _decoLight = "#C4704A";
    private string _decoDark = "#D89A6E";
    private string _font = "sans";
    private double _leading = 1.9;
    private string _report = "以内置「简约」为模板派生，仅覆盖名称、颜色、字体与行距。";

    public string? EditingKey { get; } = editingKey;
    public string Title => EditingKey is null ? "新增排版" : "编辑排版";
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
        if (colors.Any(value => value.Trim().Length != 7 || value.Trim()[0] != '#'))
        {
            Report = "存在无效颜色，请使用 #RRGGBB 格式。";
            this.RaisePropertyChanged(nameof(Report));
            return;
        }
        Result = (Name.Trim() is { Length: > 0 } name ? name : "自定义排版",
            AccentLight.Trim(), AccentDark.Trim(), DecoLight.Trim(), DecoDark.Trim(), Font, Leading);
        RequestClose?.Invoke(this, true);
    }
}
