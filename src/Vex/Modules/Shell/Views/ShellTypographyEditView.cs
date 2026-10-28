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
        // 原型 .td-row 是 flex-wrap + label 在上的三件选择器，宽度按内容走，一排装得下三件。
        var fontOptions = new[] { ("sans", Text(VexL.WBTypographyFontSans)), ("serif", Text(VexL.WBTypographyFontSerif)), ("mono", Text(VexL.WBTypographyFontMono)) };
        var leadingOptions = new[] { (1.7d, Text(VexL.WBTypographyLeadingCompact)), (1.9d, Text(VexL.WBTypographyLeadingNormal)), (2.1d, Text(VexL.WBTypographyLeadingRelaxed)) };
        var h2Options = new[] { ("leftBar", Text(VexL.WBTypographyH2LeftBar)), ("underline", Text(VexL.WBTypographyH2Underline)), ("tint", Text(VexL.WBTypographyH2Tint)), ("none", Text(VexL.WBTypographyH2None)) };
        var selectors = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        selectors.Children.Add(Selector(Text(VexL.WBTypographyFontLabel), fontOptions.Select(o => o.Item2).ToArray(),
            Math.Max(0, Array.FindIndex(fontOptions, o => o.Item1 == model.Font)), index => model.Font = fontOptions[index].Item1));
        selectors.Children.Add(Selector(Text(VexL.WBTypographyLeadingLabel), leadingOptions.Select(o => o.Item2).ToArray(),
            Math.Max(0, Array.FindIndex(leadingOptions, o => Math.Abs(o.Item1 - model.Leading) < 0.05d)), index => model.Leading = leadingOptions[index].Item1));
        selectors.Children.Add(Selector(Text(VexL.WBTypographyH2Label), h2Options.Select(o => o.Item2).ToArray(),
            Math.Max(0, Array.FindIndex(h2Options, o => o.Item1 == model.HeadingDecoration)), index => model.HeadingDecoration = h2Options[index].Item1));
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
        // 原型 .typography-dialog-card 是 min(430px,100%)；三件选择器按内容排在一排才装得下。
        Width = 430;
    }

    /// <summary>带小标签的下拉（原型 .td-row label：文字在上、控件在下，标签取说明色）。</summary>
    private static StackPanel Selector(string label, string[] items, int selected, Action<int> write)
    {
        var box = new ComboBox { MinWidth = 104, Height = 32, FontSize = 13, ItemsSource = items, SelectedIndex = selected, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) write(box.SelectedIndex); };
        var panel = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 8, 0) };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.8 });
        panel.Children.Add(box);
        return panel;
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
