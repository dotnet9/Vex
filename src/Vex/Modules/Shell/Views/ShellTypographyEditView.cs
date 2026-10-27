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
        stack.Children.Add(Field(VexL.WBTypographyNameHint, model.Name, 12, value => model.Name = value));

        var colors = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        colors.Children.Add(ColorField(VexL.WBTypographyColorAccentLight, model.AccentLight, value => model.AccentLight = value));
        colors.Children.Add(ColorField(VexL.WBTypographyColorAccentDark, model.AccentDark, value => model.AccentDark = value));
        colors.Children.Add(ColorField(VexL.WBTypographyColorDecoLight, model.DecoLight, value => model.DecoLight = value));
        colors.Children.Add(ColorField(VexL.WBTypographyColorDecoDark, model.DecoDark, value => model.DecoDark = value));
        stack.Children.Add(colors);

        // 下拉展示四语标签，选中项回写内部键（原样 ItemsSource=Keys 会把 sans/1.7 直接暴露给用户）。
        var fontOptions = new[] { ("sans", Text(VexL.WBTypographyFontSans)), ("serif", Text(VexL.WBTypographyFontSerif)), ("mono", Text(VexL.WBTypographyFontMono)) };
        var font = new ComboBox { Width = 150, Height = 32, FontSize = 13, ItemsSource = fontOptions.Select(o => o.Item2).ToArray() };
        font.SelectedIndex = Math.Max(0, Array.FindIndex(fontOptions, o => o.Item1 == model.Font));
        font.SelectionChanged += (_, _) => { if (font.SelectedIndex >= 0) model.Font = fontOptions[font.SelectedIndex].Item1; };
        var leadingOptions = new[] { (1.7d, Text(VexL.WBTypographyLeadingCompact)), (1.9d, Text(VexL.WBTypographyLeadingNormal)), (2.1d, Text(VexL.WBTypographyLeadingRelaxed)) };
        var leading = new ComboBox { Width = 150, Height = 32, FontSize = 13, ItemsSource = leadingOptions.Select(o => o.Item2).ToArray() };
        leading.SelectedIndex = Array.FindIndex(leadingOptions, o => Math.Abs(o.Item1 - model.Leading) < 0.05d);
        if (leading.SelectedIndex < 0) leading.SelectedIndex = 1;
        leading.SelectionChanged += (_, _) => { if (leading.SelectedIndex >= 0) model.Leading = leadingOptions[leading.SelectedIndex].Item1; };
        var selectors = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 14 };
        selectors.Children.Add(font); selectors.Children.Add(leading);
        stack.Children.Add(selectors);

        var report = new TextBlock { Text = model.Report, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ShellTypographyEditModel.Report)) report.Text = model.Report; };
        stack.Children.Add(report);

        var cancel = new Button { Content = Text(VexL.Cancel), Classes = { "wb-quiet" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        var apply = new Button { Content = Text(VexL.WBSaveAndApply), Classes = { "primary" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        cancel.Click += (_, _) => model.Cancel();
        apply.Click += (_, _) => model.Apply();
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(apply);
        stack.Children.Add(actions);

        Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Width = 340;
    }

    private static TextBox Field(string labelKey, string value, int maxLength, Action<string> write)
    {
        var box = new TextBox { PlaceholderText = Text(labelKey), MaxLength = maxLength, Height = 32, FontSize = 13, CornerRadius = new CornerRadius(5), Text = value };
        box.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty) write(box.Text ?? "");
        };
        return box;
    }

    private static StackPanel ColorField(string labelKey, string value, Action<string> write)
    {
        var input = new TextBox { Text = value, Width = 84, Height = 30, FontSize = 11.5, FontFamily = VexFonts.Code, CornerRadius = new CornerRadius(5) };
        input.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty) write(input.Text ?? "");
        };
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { Text = Text(labelKey), FontSize = 11, Opacity = 0.8 });
        panel.Children.Add(input);
        return panel;
    }
}
