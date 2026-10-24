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

        // 对齐原型 #palette-dialog：名称/描述为紧凑行（无前置标签，Watermark 提示）；
        // 浅色态/深色态两组并排，每行 = 色块 + 中文标签 + #RRGGBB 输入。
        var nameBox = Field("名称", model.Name, 12, value => model.Name = value);
        var noteBox = Field("描述", model.Note, 20, value => model.Note = value);

        var groupGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*") };
        for (var group = 0; group < 2; group++)
        {
            var groupTitle = new TextBlock
            {
                Text = ShellPaletteEditModel.GroupLabels[group],
                FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 11,
                Opacity = 0.75, Margin = new Thickness(0, 2, 0, 8),
            };
            var rows = new StackPanel { Spacing = 7 };
            for (var slot = 0; slot < 4; slot++)
            {
                var index = group * 4 + slot;
                var color = model.Colors[index];
                var swatch = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
                TrySetSwatch(swatch, color);
                var input = new TextBox { Text = color, Width = 96, Height = 26, FontSize = 12, FontFamily = VexFonts.Code, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3) };
                var preview = swatch;
                input.PropertyChanged += (_, args) =>
                {
                    if (args.Property == TextBox.TextProperty) TrySetSwatch(preview, input.Text ?? "");
                };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
                row.Children.Add(preview);
                row.Children.Add(new TextBlock { Text = ShellPaletteEditModel.ColorLabels[index], VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Opacity = 0.8 });
                row.Children.Add(input);
                rows.Children.Add(row);
            }
            var column = new StackPanel { Spacing = 7 };
            column.Children.Add(groupTitle); column.Children.Add(rows);
            Grid.SetColumn(column, group);
            groupGrid.Children.Add(column);
        }

        var report = new TextBlock { Text = model.Report, FontSize = 11, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Opacity = 0.75 };
        report.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VexMutedBrush"));
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ShellPaletteEditModel.Report)) report.Text = model.Report; };

        var cancel = new Button { Content = "取消", Classes = { "wb-quiet" }, MinHeight = 28, Padding = new Thickness(12, 4), FontSize = 12 };
        var apply = new Button { Content = "保存并应用", Classes = { "primary" }, MinHeight = 28, Padding = new Thickness(12, 4), FontSize = 12 };
        cancel.Click += (_, _) => model.Cancel();
        apply.Click += (_, _) => model.Apply();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(apply);

        var stack = new StackPanel { Spacing = 9 };
        stack.Children.Add(title);
        stack.Children.Add(nameBox);
        stack.Children.Add(groupGrid);
        stack.Children.Add(noteBox);
        stack.Children.Add(report); stack.Children.Add(actions);
        Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Width = 340;
    }

    private static TextBox Field(string label, string value, int maxLength, Action<string> write)
    {
        var box = new TextBox { PlaceholderText = label, MaxLength = maxLength, Height = 32, FontSize = 13, CornerRadius = new CornerRadius(5), Text = value };
        box.PropertyChanged += (_, args) => { if (args.Property == TextBox.TextProperty) write(box.Text ?? ""); };
        return box;
    }

    private static void TrySetSwatch(Border swatch, string hex)
    {
        try { if (ShellPaletteEditModel.TryParseHex(hex)) swatch.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex)); }
        catch { /* 非法输入保持原色 */ }
    }
}
