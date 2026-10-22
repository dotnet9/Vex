using SkiaSharp;

/// <summary>
/// 逐场景像素对比：native 目录与 prototype 目录同名的 PNG 两两比较，
/// 输出每场景得分（颜色距离 ≤ 阈值的像素占比）与差异热图。目标 ≥98%。
/// </summary>
internal static class VisualParityScore
{
    private const int ChannelTolerance = 24;

    private static int DeltaAt(SKBitmap bitmap, int x, int y, SKColor reference)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height) return 255;
        var pixel = bitmap.GetPixel(x, y);
        return Math.Max(Math.Max(Math.Abs(pixel.Red - reference.Red), Math.Abs(pixel.Green - reference.Green)), Math.Abs(pixel.Blue - reference.Blue));
    }

    private static SKColor Average(SKBitmap bitmap, int x, int y)
    {
        int r = 0, g = 0, b = 0;
        for (var dy = 0; dy < 2; dy++)
        for (var dx = 0; dx < 2; dx++)
        {
            var pixel = bitmap.GetPixel(x + dx, y + dy);
            r += pixel.Red; g += pixel.Green; b += pixel.Blue;
        }
        return new SKColor((byte)(r / 4), (byte)(g / 4), (byte)(b / 4));
    }

    public static void Run(string[] args)
    {
        var nativeDir = args.ElementAtOrDefault(0) ?? throw new InvalidOperationException("usage: parity-score <nativeDir> <prototypeDir> [report.json]");
        var prototypeDir = args.ElementAtOrDefault(1) ?? throw new InvalidOperationException("usage: parity-score <nativeDir> <prototypeDir> [report.json]");
        var reportPath = args.ElementAtOrDefault(2);
        var results = new List<(string Name, double Score, int Width, int Height)>();
        string? first = null;
        var histogram = new int[16];
        foreach (var nativePath in Directory.GetFiles(nativeDir, "*.png").OrderBy(file => Path.GetFileName(file)))
        {
            var name = Path.GetFileName(nativePath);
            if (name.StartsWith("diff-", StringComparison.Ordinal) || name.StartsWith("fail-", StringComparison.Ordinal)) continue;
            var prototypePath = Path.Combine(prototypeDir, name);
            if (!File.Exists(prototypePath))
            {
                Console.WriteLine($"MISS  {name}  (prototype capture not found)");
                continue;
            }
            using var native = SKBitmap.Decode(nativePath);
            using var prototype = SKBitmap.Decode(prototypePath);
            if (native is null || prototype is null)
            {
                Console.WriteLine($"ERROR {name}: decode failed");
                continue;
            }
            if (first is null) first = name;
            var width = Math.Min(native.Width, prototype.Width);
            var height = Math.Min(native.Height, prototype.Height);
            if (native.Width != prototype.Width || native.Height != prototype.Height)
                Console.WriteLine($"WARN  {name}: size differs native={native.Width}x{native.Height} prototype={prototype.Width}x{prototype.Height}");
            // 字体容差：2x2 均值降采样后再比较，抑制两侧抗锯齿差异；结构与色块差异仍然计入。
            int matched = 0, total = (width / 2) * (height / 2);
            using var heat = new SKBitmap(width, height);
            using var overlay = native.Copy();
            for (var y = 0; y + 1 < height; y += 2)
            {
                for (var x = 0; x + 1 < width; x += 2)
                {
                    var a = Average(native, x, y);
                    var b = Average(prototype, x, y);
                    var delta = Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)), Math.Abs(a.Blue - b.Blue));
                    if (name == first) histogram[Math.Min(delta / 16, 15)]++;
                    if (delta <= ChannelTolerance)
                    {
                        matched++;
                        var w = new SKColor(255, 255, 255);
                        heat.SetPixel(x, y, w); heat.SetPixel(x + 1, y, w); heat.SetPixel(x, y + 1, w); heat.SetPixel(x + 1, y + 1, w);
                    }
                    else
                    {
                        var h = new SKColor(255, (byte)Math.Clamp(255 - delta, 0, 255), 0);
                        heat.SetPixel(x, y, h); heat.SetPixel(x + 1, y, h); heat.SetPixel(x, y + 1, h); heat.SetPixel(x + 1, y + 1, h);
                        var f = new SKColor(255, 0, 255);
                        overlay.SetPixel(x, y, f); overlay.SetPixel(x + 1, y, f); overlay.SetPixel(x, y + 1, f); overlay.SetPixel(x + 1, y + 1, f);
                    }
                        }
            }
            var score = total == 0 ? 0 : matched * 100.0 / total;
            results.Add((name, score, width, height));
            using (var stream = File.OpenWrite(Path.Combine(nativeDir, "diff-" + name)))
                heat.Encode(stream, SKEncodedImageFormat.Png, 100);
            using (var stream = File.OpenWrite(Path.Combine(nativeDir, "fail-" + name)))
                overlay.Encode(stream, SKEncodedImageFormat.Png, 100);
            Console.WriteLine($"{score,6:0.00}%  {name}");
        }
        Console.WriteLine("DELTA-HISTOGRAM(first scene, buckets of 16): " + string.Join(",", histogram));
        if (results.Count > 0)
        {
            var average = results.Average(result => result.Score);
            Console.WriteLine($"AVERAGE {average:0.00}% over {results.Count} scenes");
            if (reportPath is not null)
            {
                var json = "[" + string.Join(",", results.Select(result =>
                    $"{{\"name\":\"{result.Name}\",\"score\":{result.Score:0.00}}}")) + "]";
                File.WriteAllText(reportPath, json);
            }
        }
    }
}
