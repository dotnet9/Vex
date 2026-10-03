using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using CodeWF.Markdown;
using CodeWF.Markdown.Controls;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// 修正预览代码块在浅色应用主题下的可读性：
/// CodeWF.Markdown 的代码高亮按 ActualThemeVariant 选配色，非 Dark 主题（含 Desert 等
/// 浅色暖色主题与 night-sky 等自定义暗色变体）会拿到浅底 token 色（暗红/深蓝/黑），
/// 而代码块背景由排版主题固定为深色，导致深色 token（大括号/键名/字符串）不可读。
/// 这里在渲染完成后把所有低亮度 token 前景按色相提亮为暗底可读色（对齐 VS Code dark+ 风格）。
/// </summary>
public static class MarkdownCodeContrastFixer
{
    // 近黑标点（大括号/引号/冒号等）统一提为浅灰，对齐 VS Code dark+ 的标点色。
    private static readonly Color PunctuationLight = Color.FromRgb(0xD4, 0xD4, 0xD4);

    public static void Attach(MarkdownViewer viewer)
    {
        viewer.CodeBlockToolRender += OnCodeBlockToolRender;
    }

    private static void OnCodeBlockToolRender(object? sender, CodeBlockToolRenderEventArgs e)
    {
        FixTokenForegrounds(e.ContentPanel);
    }

    private static void FixTokenForegrounds(Control control)
    {
        if (control is SelectableTextBlock textBlock)
        {
            foreach (var inline in textBlock.Inlines)
            {
                if (inline.Foreground is ISolidColorBrush solid
                    && IsTooDarkForDarkBackground(solid.Color))
                {
                    inline.Foreground = new SolidColorBrush(BrightenForDarkBackground(solid.Color));
                }
            }
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                FixTokenForegrounds(child);
            }
        }
        else if (control is Decorator decorator)
        {
            if (decorator.Child is { } child)
            {
                FixTokenForegrounds(child);
            }
        }
        else if (control is Border border)
        {
            if (border.Child is { } child)
            {
                FixTokenForegrounds(child);
            }
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            FixTokenForegrounds(contentChild);
        }
    }

    private static bool IsTooDarkForDarkBackground(Color color)
    {
        // 感知亮度（CCIR 601），低于 0.5 在深色代码块底上即为低对比。
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luminance < 0.5;
    }

    private static Color BrightenForDarkBackground(Color color)
    {
        RgbToHsv(color, out var hue, out var saturation, out var value);

        // 近黑无彩（标点/大括号/引号）→ 浅灰
        if (saturation < 0.15 && value < 0.35)
        {
            return PunctuationLight;
        }

        // 保留色相，亮度提到暗底可读水平，饱和度略降避免刺眼
        value = Math.Clamp(value, 0.80, 1.0);
        saturation = Math.Min(saturation, 0.65);
        return HsvToRgb(hue, saturation, value);
    }

    private static void RgbToHsv(Color color, out double h, out double s, out double v)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        v = max;
        s = max <= 0 ? 0 : delta / max;

        if (delta <= 0)
        {
            h = 0;
            return;
        }

        if (max == r)
        {
            h = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            h = 60 * ((b - r) / delta + 2);
        }
        else
        {
            h = 60 * ((r - g) / delta + 4);
        }

        if (h < 0)
        {
            h += 360;
        }
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        (var r, var g, var b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
