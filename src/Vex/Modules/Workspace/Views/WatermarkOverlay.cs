using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vex.Modules.Workspace.Views;

/// <summary>独立于正文的水印层，不参与输入、选区和文档导图。</summary>
public sealed class WatermarkOverlay : Control
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<WatermarkOverlay, string>(nameof(Text), "维刻 Vex");
    public static readonly StyledProperty<double> StrengthProperty = AvaloniaProperty.Register<WatermarkOverlay, double>(nameof(Strength), 12);
    public static readonly StyledProperty<IBrush?> InkProperty = AvaloniaProperty.Register<WatermarkOverlay, IBrush?>(nameof(Ink), Brushes.Teal);
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Strength { get => GetValue(StrengthProperty); set => SetValue(StrengthProperty, value); }
    public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }

    static WatermarkOverlay() => AffectsRender<WatermarkOverlay>(TextProperty, StrengthProperty, InkProperty);
    public WatermarkOverlay()
    {
        IsHitTestVisible = false; ClipToBounds = true;
        this.Bind(InkProperty, this.GetResourceObservable("VexAccentBrush"));
    }
    public override void Render(DrawingContext context)
    {
        if (string.IsNullOrWhiteSpace(Text) || !double.IsFinite(Strength) || Strength <= 0) return;
        var text = new FormattedText(Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 19, Ink);
        using var clip = context.PushClip(new Rect(Bounds.Size));
        using var opacity = context.PushOpacity(Math.Clamp(Strength / 100, 0, 0.5));
        for (var y = 100d; y < Bounds.Height; y += 180)
        for (var x = 40d; x < Bounds.Width; x += 280)
        {
            using var transform = context.PushTransform(Matrix.CreateRotation(-25 * Math.PI / 180) * Matrix.CreateTranslation(x, y));
            context.DrawText(text, default);
        }
    }
}
