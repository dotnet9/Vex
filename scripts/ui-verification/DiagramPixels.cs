using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

/// <summary>
/// 按像素读图：渲染器把没解析出来的颜色当纯黑这类缺陷，边框和「图已加载」断言都看不出来，
/// 只能统计真正画在位图上的颜色。
/// </summary>
internal static class DiagramPixels
{
    /// <summary>按分数坐标取色（0~1 相对控件包围盒），返回 #RRGGBB；透明处返回 null，表示该点没有落墨。</summary>
    public static string?[] Probe(Control control, params (double X, double Y)[] points)
    {
        using var bitmap = Raster(control);
        var colours = new string?[points.Length];
        for (var index = 0; index < points.Length; index++)
        {
            var pixel = bitmap.GetPixel(
                Math.Min(bitmap.Width - 1, Math.Max(0, (int)(points[index].X * bitmap.Width))),
                Math.Min(bitmap.Height - 1, Math.Max(0, (int)(points[index].Y * bitmap.Height))));
            colours[index] = pixel.Alpha <= 8 ? null : $"#{pixel.Red:X2}{pixel.Green:X2}{pixel.Blue:X2}";
        }
        return colours;
    }

    /// <summary>纯黑像素占落墨像素的比例：没展开的 var()/color-mix() 被 Skia 当黑色时这个值会逼近整块填色面积。</summary>
    public static double PureBlackShare(Control control)
    {
        using var bitmap = Raster(control);
        var painted = 0;
        var black = 0;
        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Alpha <= 8) continue;
            painted++;
            if (pixel.Red <= 4 && pixel.Green <= 4 && pixel.Blue <= 4) black++;
        }
        return painted == 0 ? 1 : black / (double)painted;
    }

    static SKBitmap Raster(Control control)
    {
        var width = (int)Math.Max(1, Math.Round(control.Bounds.Width));
        var height = (int)Math.Max(1, Math.Round(control.Bounds.Height));
        using var target = new RenderTargetBitmap(new PixelSize(width, height));
        target.Render(control);
        using var stream = new MemoryStream();
        target.Save(stream);
        return SKBitmap.Decode(stream.ToArray()) ?? throw new InvalidOperationException("图表位图无法解码");
    }
}
