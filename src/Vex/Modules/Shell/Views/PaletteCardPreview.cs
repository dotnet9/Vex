using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vex.Core.Models;

namespace Vex.Modules.Shell.Views;

/// <summary>配色卡展示真实的侧栏、纸面、文字与强调色，随当前明暗模式更新。</summary>
internal sealed class PaletteCardPreview : UserControl
{
    public PaletteCardPreview(PaletteOption palette)
    {
        Classes.Add("palette-preview");
        Tag = palette.Key;
        IsHitTestVisible = false;
        Height = 38;
        foreach (var (variant, colors) in new[] { (ThemeVariant.Light, palette.Definition.Light), (ThemeVariant.Dark, palette.Definition.Dark) })
        {
            var ink = Color.Parse(colors.Ink);
            Resources.ThemeDictionaries[variant] = new ResourceDictionary
            {
                ["PreviewPaper"] = Brush.Parse(colors.Paper),
                ["PreviewSurface"] = Brush.Parse(colors.Surface),
                ["PreviewInk"] = new SolidColorBrush(ink),
                ["PreviewAccent"] = Brush.Parse(colors.Accent),
                ["PreviewBorder"] = new SolidColorBrush(Color.FromArgb(41, ink.R, ink.G, ink.B))
            };
        }
        Build();
    }

    private void Build()
    {
        var sidebar = new Border { BorderThickness = new Thickness(0, 0, 1, 0) };
        sidebar.Bind(Border.BackgroundProperty, this.GetResourceObservable("PreviewSurface"));
        sidebar.Bind(Border.BorderBrushProperty, this.GetResourceObservable("PreviewBorder"));
        var label = new TextBlock { Text = "Aa", FontSize = 13, LineHeight = 13, FontWeight = FontWeight.Bold };
        label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("PreviewInk"));
        var accent = new Border { Width = 20, Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
        accent.Bind(Border.BackgroundProperty, this.GetResourceObservable("PreviewAccent"));
        var sample = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                label, accent
            }
        };
        var grid = new Grid { ColumnDefinitions = new("22*,78*"), Children = { sidebar, sample } };
        Grid.SetColumn(sample, 1);
        var frame = new Border
        {
            Child = grid, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), ClipToBounds = true
        };
        frame.Bind(Border.BorderBrushProperty, this.GetResourceObservable("VexPanelBorderBrush"));
        frame.Bind(Border.BackgroundProperty, this.GetResourceObservable("PreviewPaper"));
        Content = frame;
    }
}
