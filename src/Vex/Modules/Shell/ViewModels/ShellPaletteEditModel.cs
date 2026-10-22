using Avalonia.Media;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>自定义配色编辑（对应原型 palette-dialog）：名称/描述 + 双明暗各 4 色，其余语义键由引擎派生。</summary>
public sealed class ShellPaletteEditModel(string? editingKey, string name = "", string note = "",
    string[]? colors = null) : ReactiveObject, IDialogContext
{
    public static readonly string[] ColorLabels = ["纸面", "面板", "墨色", "强调", "纸面", "面板", "墨色", "强调"];
    public static readonly string[] GroupLabels = ["浅色态", "深色态"];
    private static readonly string[] DefaultColors =
        ["#FFFFFF", "#F4F4F5", "#18181B", "#4F46E5", "#101014", "#1B1B20", "#E4E4E9", "#818CF8"];

    public string? EditingKey { get; } = editingKey;
    public string Title => EditingKey is null ? "新增配色" : "编辑配色";
    public string Report { get; private set; } = "4 色 × 双明暗编写，其余语义键自动派生。";
    public (string Name, string Note, string[] Colors)? Result { get; private set; }

    public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }
    public string Note { get => _note; set => this.RaiseAndSetIfChanged(ref _note, value); }
    public string[] Colors { get; init; } = colors is { Length: 8 } ? colors : DefaultColors;

    public event EventHandler<object?>? RequestClose;
    public void Cancel() => RequestClose?.Invoke(this, false);
    public void Close() => Cancel();

    public void Apply()
    {
        var colors = Colors.Select(value => value.Trim().ToUpperInvariant()).ToArray();
        if (colors.Any(value => !TryParseHex(value)))
        {
            Report = "存在无效颜色，请使用 #RRGGBB 格式。";
            this.RaisePropertyChanged(nameof(Report));
            return;
        }
        Result = (Name.Trim() is { Length: > 0 } name ? name : "自定义", Note.Trim(), colors);
        RequestClose?.Invoke(this, true);
    }

    public static bool TryParseHex(string value) =>
        value.Length == 7 && value[0] == '#' && Color.Parse(value) is { };

    private string _name = name;
    private string _note = note;
}
