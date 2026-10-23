using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>
/// 未保存确认（对应原型截图）：圆形图标 + 标题 + 右上角关闭 + 一句提示 + 右下按钮组。
/// 退出流 = 继续编辑 / 放弃修改并退出（红描边）；切换流 = 取消 / 不保存直接切换 / 保存并切换（主按钮）。
/// </summary>
public partial class ShellUnsavedDialogView : UserControl
{
    private readonly TextBlock _title = new() { FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _hint = new() { FontSize = 13.5, LineHeight = 21, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };

    public ShellUnsavedDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        if (DataContext is not ShellUnsavedDialogViewModel model) return;
        var exit = model.Flow == ShellUnsavedFlow.Exit;

        var icon = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), VerticalAlignment = VerticalAlignment.Center };
        icon.Bind(BackgroundProperty, this.GetResourceObservable("VexWarningSoftBrush"));
        var glyph = new Avalonia.Controls.Shapes.Path
        {
            Width = 18, Height = 18, Stretch = Stretch.Uniform,
            StrokeThickness = 1.6, Stroke = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Data = Geometry.Parse(exit
                ? "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01"
                : "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M8 13h8 M8 17h5"),
        };
        icon.Child = glyph;

        var close = new Button { Classes = { "vex-dismiss" }, Width = 40, Height = 30 };
        Avalonia.Automation.AutomationProperties.SetName(close, CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(VexL.Close));
        close.Click += (_, _) => model.Cancel();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        header.Children.Add(icon);
        _title.Text = Localized(exit ? VexL.DialogExitTitle : VexL.DialogSwitchTitle);
        Grid.SetColumn(_title, 1); header.Children.Add(_title);
        Grid.SetColumn(close, 2); header.Children.Add(close);

        _hint.Text = Localized(exit ? VexL.DialogExitHint : VexL.DialogSwitchHint);

        _actions.Children.Clear();
        if (exit)
        {
            _actions.Children.Add(Quiet(VexL.DialogContinueEditing, model.Cancel));
            _actions.Children.Add(Danger(VexL.DialogDiscardAndExit, model.Discard));
        }
        else
        {
            _actions.Children.Add(Quiet(VexL.Cancel, model.Cancel));
            _actions.Children.Add(Quiet(VexL.DialogDiscardAndSwitch, model.Discard));
            _actions.Children.Add(Primary(VexL.DialogSaveAndSwitch, model.Save));
        }

        var root = new StackPanel { Spacing = 14 };
        root.Children.Add(header);
        root.Children.Add(_hint);
        root.Children.Add(_actions);

        Content = new Border
        {
            Classes = { "dialog-card" },
            Width = exit ? 560 : 620,
            MaxWidth = 700,
            Padding = new Thickness(24, 20),
            CornerRadius = new CornerRadius(10),
            Child = root,
        };
    }

    private Button Quiet(string label, Action action)
    {
        var button = new Button { Content = label, MinHeight = 34, Padding = new Thickness(14, 5), CornerRadius = new CornerRadius(6) };
        button.Click += (_, _) => action();
        return button;
    }

    private Button Danger(string label, Action action)
    {
        var button = Quiet(label, action);
        button.MinWidth = 120;
        button.Foreground = Brush("VexDangerBrush");
        button.BorderBrush = Brush("VexDangerBrush");
        button.BorderThickness = new Thickness(1);
        button.Background = Brushes.Transparent;
        return button;
    }

    private Button Primary(string label, Action action)
    {
        var button = Quiet(label, action);
        button.Classes.Add("primary");
        return button;
    }

    private IBrush Brush(string key) => TryGetResource(key, null, out var value) && value is IBrush found
        ? found : Brushes.Transparent;

    private string Localized(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(key);
}
