using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vex.Core.Models;
using Vex.Core.Theme;

namespace Vex.Modules.Shell.Views;

/// <summary>配色卡展示真实的侧栏、纸面、文字与强调色，随当前明暗模式更新。</summary>
internal sealed class PaletteCardPreview : UserControl
{
    private readonly PaletteOption _palette;

    public PaletteCardPreview(PaletteOption palette)
    {
        _palette = palette;
        Classes.Add("palette-preview");
        Tag = palette.Key;
        IsHitTestVisible = false;
        Height = 38;
        ActualThemeVariantChanged += (_, _) => Build();
        Build();
    }

    private void Build()
    {
        var colors = ElevationResources.IsDarkVariant(ActualThemeVariant) ? _palette.Definition.Dark : _palette.Definition.Light;
        var ink = Color.Parse(colors.Ink);
        var sidebar = new Border
        {
            Background = Brush.Parse(colors.Surface), BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(41, ink.R, ink.G, ink.B))
        };
        var sample = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Aa", FontSize = 13, LineHeight = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(ink) },
                new Border { Width = 20, Height = 4, CornerRadius = new CornerRadius(2), Background = Brush.Parse(colors.Accent), VerticalAlignment = VerticalAlignment.Center }
            }
        };
        var grid = new Grid { ColumnDefinitions = new("22*,78*"), Children = { sidebar, sample } };
        Grid.SetColumn(sample, 1);
        var frame = new Border
        {
            Child = grid, Background = Brush.Parse(colors.Paper), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), ClipToBounds = true
        };
        frame.Bind(Border.BorderBrushProperty, this.GetResourceObservable("VexPanelBorderBrush"));
        Content = frame;
    }
}
