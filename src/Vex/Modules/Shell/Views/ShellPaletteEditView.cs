using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

/// <summary>自定义配色编辑浮层（对应原型 #palette-dialog）。</summary>
public sealed class ShellPaletteEditView : UserControl
{
    public ShellPaletteEditView(ShellPaletteEditModel model)
    {
        DataContext = model;
        this.Bind(ForegroundProperty, this.GetResourceObservable("VexValueBrush"));

        var title = new TextBlock { Text = model.Title, FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold };

        var nameBox = Field("名称", model.Name, 12, value => model.Name = value);
        var noteBox = Field("描述", model.Note, 20, value => model.Note = value);

        var groups = new StackPanel { Spacing = 10 };
        for (var group = 0; group < 2; group++)
        {
            var groupTitle = new TextBlock { Text = ShellPaletteEditModel.GroupLabels[group], FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 12.5, Margin = new Thickness(0, 4, 0, 0) };
            var rows = new StackPanel { Spacing = 6 };
            for (var slot = 0; slot < 4; slot++)
            {
                var index = group * 4 + slot;
                var color = model.Colors[index];
                var swatch = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center };
                TrySetSwatch(swatch, color);
                var input = new TextBox { Text = color, Width = 128, Height = 30, FontSize = 13, FontFamily = VexFonts.Code, CornerRadius = new CornerRadius(5) };
                var preview = swatch;
                input.PropertyChanged += (_, args) =>
                {
                    if (args.Property == TextBox.TextProperty) TrySetSwatch(preview, input.Text ?? "");
                };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(preview);
                row.Children.Add(new TextBlock { Text = ShellPaletteEditModel.ColorLabels[index], VerticalAlignment = VerticalAlignment.Center, FontSize = 13, MinWidth = 36 });
                row.Children.Add(input);
                rows.Children.Add(row);
            }
            var column = new StackPanel { Spacing = 6 };
            column.Children.Add(groupTitle); column.Children.Add(rows);
            groups.Children.Add(column);
        }

        var report = new TextBlock { Text = model.Report, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        report.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexMutedBrush"));
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ShellPaletteEditModel.Report)) report.Text = model.Report; };

        var cancel = new Button { Content = "取消", Classes = { "wb-quiet" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        var apply = new Button { Content = "保存并应用", Classes = { "primary" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        cancel.Click += (_, _) => model.Cancel();
        apply.Click += (_, _) => model.Apply();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(apply);

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(title); stack.Children.Add(nameBox); stack.Children.Add(noteBox);
        stack.Children.Add(groups); stack.Children.Add(report); stack.Children.Add(actions);
        Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Width = 320;
    }

    private static TextBox Field(string label, string value, int maxLength, Action<string> write)
    {
        var box = new TextBox { Watermark = label, MaxLength = maxLength, Height = 32, FontSize = 13, CornerRadius = new CornerRadius(5), Text = value };
        box.PropertyChanged += (_, args) => { if (args.Property == TextBox.TextProperty) write(box.Text ?? ""); };
        return box;
    }

    private static void TrySetSwatch(Border swatch, string hex)
    {
        try { if (ShellPaletteEditModel.TryParseHex(hex)) swatch.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex)); }
        catch { /* 非法输入保持原色 */ }
    }
}
