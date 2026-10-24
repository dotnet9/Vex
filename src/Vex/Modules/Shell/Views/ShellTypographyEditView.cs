using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

/// <summary>自定义排版编辑浮层（对应原型 #typography-dialog）。</summary>
public sealed class ShellTypographyEditView : UserControl
{
    public ShellTypographyEditView(ShellTypographyEditModel model)
    {
        DataContext = model;
        this.Bind(ForegroundProperty, this.GetResourceObservable("VexValueBrush"));

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = model.Title, FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        stack.Children.Add(Field(model, "名称", 12, nameof(ShellTypographyEditModel.Name)));

        var colors = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        colors.Children.Add(ColorField(model, "强调·浅", nameof(ShellTypographyEditModel.AccentLight)));
        colors.Children.Add(ColorField(model, "强调·深", nameof(ShellTypographyEditModel.AccentDark)));
        colors.Children.Add(ColorField(model, "装饰·浅", nameof(ShellTypographyEditModel.DecoLight)));
        colors.Children.Add(ColorField(model, "装饰·深", nameof(ShellTypographyEditModel.DecoDark)));
        stack.Children.Add(colors);

        var fontChoices = new Dictionary<string, string> { ["sans"] = "无衬线", ["serif"] = "衬线", ["mono"] = "等宽" };
        var font = new ComboBox { Width = 150, Height = 32, FontSize = 13, ItemsSource = fontChoices.Keys.ToArray() };
        font.SelectedItem = model.Font;
        font.SelectionChanged += (_, _) => { if (font.SelectedItem is string key) model.Font = key; };
        var leadingChoices = new Dictionary<double, string> { [1.7] = "紧凑 1.7", [1.9] = "正常 1.9", [2.1] = "舒展 2.1" };
        var leading = new ComboBox { Width = 150, Height = 32, FontSize = 13, ItemsSource = leadingChoices.Keys.ToArray() };
        leading.SelectedItem = leadingChoices.Keys.OrderBy(value => Math.Abs(value - model.Leading)).First();
        leading.SelectionChanged += (_, _) => { if (leading.SelectedItem is double value) model.Leading = value; };
        var selectors = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 14 };
        selectors.Children.Add(font); selectors.Children.Add(leading);
        stack.Children.Add(selectors);

        var report = new TextBlock { Text = model.Report, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ShellTypographyEditModel.Report)) report.Text = model.Report; };
        stack.Children.Add(report);

        var cancel = new Button { Content = "取消", Classes = { "wb-quiet" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        var apply = new Button { Content = "保存并应用", Classes = { "primary" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        cancel.Click += (_, _) => model.Cancel();
        apply.Click += (_, _) => model.Apply();
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(apply);
        stack.Children.Add(actions);

        Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Width = 340;
    }

    private static TextBox Field(ShellTypographyEditModel model, string label, int maxLength, string property)
    {
        var box = new TextBox { PlaceholderText = label, MaxLength = maxLength, Height = 32, FontSize = 13, CornerRadius = new CornerRadius(5), Text = label == "名称" ? model.Name : "" };
        box.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && property == nameof(ShellTypographyEditModel.Name)) model.Name = box.Text ?? "";
        };
        return box;
    }

    private static StackPanel ColorField(ShellTypographyEditModel model, string label, string property)
    {
        var input = new TextBox { Text = (string?)(typeof(ShellTypographyEditModel).GetProperty(property)?.GetValue(model)) ?? "", Width = 84, Height = 30, FontSize = 11.5, FontFamily = VexFonts.Code, CornerRadius = new CornerRadius(5) };
        input.PropertyChanged += (_, args) =>
        {
            if (args.Property != TextBox.TextProperty) return;
            typeof(ShellTypographyEditModel).GetProperty(property)?.SetValue(model, input.Text ?? "");
        };
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.8 });
        panel.Children.Add(input);
        return panel;
    }
}
