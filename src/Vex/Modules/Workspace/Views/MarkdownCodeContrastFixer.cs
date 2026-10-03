using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using CodeWF.Markdown;
using CodeWF.Markdown.Controls;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// 修正预览代码块在浅色应用主题下的可读性：
/// CodeWF.Markdown 的代码高亮按 ActualThemeVariant 选配色，非 Dark 主题（含 Desert 等
/// 浅色暖色主题）使用浅底 token 色（暗红/深蓝），而代码块背景由排版主题固定为深色，
/// 导致键名/字符串/标点不可读。这里在渲染完成后把浅色主题 token 色映射为暗底可读色。
/// </summary>
public static class MarkdownCodeContrastFixer
{
    // 浅色主题 token 前景（VS 浅色系）→ 暗底可读色。键取整色比对。
    private static readonly Dictionary<Color, Color> LightTokenToDarkReadable = new()
    {
        [Color.FromRgb(0xA3, 0x15, 0x15)] = Color.FromRgb(0xE0, 0x6C, 0x75), // 字符串/键暗红
        [Color.FromRgb(0x00, 0x00, 0xFF)] = Color.FromRgb(0x56, 0x9C, 0xD6), // 键名蓝
        [Color.FromRgb(0x00, 0x7F, 0x00)] = Color.FromRgb(0x6A, 0x99, 0x55), // 注释绿
    };

    public static void Attach(MarkdownViewer viewer)
    {
        viewer.CodeBlockToolRender += OnCodeBlockToolRender;
        Log("attach ok");
    }

    private static void Log(string message)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vex_contrast_fix.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static void OnCodeBlockToolRender(object? sender, CodeBlockToolRenderEventArgs e)
    {
        Log("event fired");
        var replaced = FixTokenForegrounds(e.ContentPanel);
        Log("replaced: " + replaced);
    }

    private static int FixTokenForegrounds(Control control)
    {
        var count = 0;
        if (control is SelectableTextBlock textBlock)
        {
            Log("found SelectableTextBlock, inlines=" + textBlock.Inlines.Count);
            foreach (var inline in textBlock.Inlines)
            {
                if (inline.Foreground is ISolidColorBrush solid)
                {
                    Log("  token color: " + solid.Color);
                    if (LightTokenToDarkReadable.TryGetValue(solid.Color, out var readable))
                    {
                        inline.Foreground = new SolidColorBrush(readable);
                        count++;
                    }
                }
            }
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                count += FixTokenForegrounds(child);
            }
        }
        else if (control is Decorator decorator)
        {
            if (decorator.Child is { } child)
            {
                count += FixTokenForegrounds(child);
            }
        }
        else if (control is Border border)
        {
            count += FixTokenForegrounds(border.Child);
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            count += FixTokenForegrounds(contentChild);
        }

        return count;
    }
}
