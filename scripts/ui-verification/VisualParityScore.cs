using SkiaSharp;

/// <summary>
/// 逐场景像素对比：native 目录与 prototype 目录同名的 PNG 两两比较，
/// 输出每场景得分（颜色距离 ≤ 阈值的像素占比）与差异热图。目标 ≥98%。
/// </summary>
internal static class VisualParityScore
{
    private const int ChannelTolerance = 24;
    private static readonly string[] Scenes = ["01-instant", "02-source", "03-read", "04-map", "05-source-render", "06-three",
        "07-appearance-menu", "08-preferences", "09-typography-gallery", "10-presentation"];

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
        var diagnostic = args.Contains("--diagnostic", StringComparer.Ordinal);
        var results = new List<(string Name, double Score, int Width, int Height)>();
        string? first = null;
        var histogram = new int[16];
        foreach (var scene in Scenes)
        {
            var name = scene + ".png";
            var nativePath = Path.Combine(nativeDir, name);
            var prototypePath = Path.Combine(prototypeDir, name);
            if (!File.Exists(nativePath) || !File.Exists(prototypePath))
                throw new InvalidOperationException($"Required capture missing: {name}");
            using var native = SKBitmap.Decode(nativePath);
            using var prototype = SKBitmap.Decode(prototypePath);
            if (native is null || prototype is null)
                throw new InvalidOperationException($"Capture decode failed: {name}");
            if (native.Width != VisualParityCapture.Width || native.Height != VisualParityCapture.Height
                || prototype.Width != native.Width || prototype.Height != native.Height)
                throw new InvalidOperationException($"Invalid capture size for {name}: native={native.Width}x{native.Height}, prototype={prototype.Width}x{prototype.Height}; expected 1440x900");
            if (first is null) first = name;
            var width = native.Width;
            var height = native.Height;
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
            using (var stream = File.Create(Path.Combine(nativeDir, "diff-" + name)))
                heat.Encode(stream, SKEncodedImageFormat.Png, 100);
            using (var stream = File.Create(Path.Combine(nativeDir, "fail-" + name.Replace(".png", ".jpg"))))
                overlay.Encode(stream, SKEncodedImageFormat.Jpeg, 90);
            Console.WriteLine($"{score,6:0.00}%  {name}");
        }
        Console.WriteLine("DELTA-HISTOGRAM(first scene, buckets of 16): " + string.Join(",", histogram));
        var average = results.Average(result => result.Score);
        var passed = results.All(result => result.Score >= 98);
        Console.WriteLine($"{(diagnostic ? "DIAGNOSTIC" : passed ? "PASS" : "FAIL")} average {average:0.00}% over {results.Count} required scenes; minimum {results.Min(result => result.Score):0.00}%");
        if (reportPath is not null)
            File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(new
            {
                average, passed, minimumRequired = 98, channelTolerance = ChannelTolerance, sampleSize = 2,
                scenes = results.Select(result => new { name = result.Name, score = result.Score, width = result.Width, height = result.Height })
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        if (!diagnostic && !passed)
            throw new InvalidOperationException("Pixel parity requires every complete scene to reach 98%; see the report and difference images.");
    }

    public static void SelfTest(string output)
    {
        var root = Path.Combine(output, "scorer-" + Guid.NewGuid().ToString("N"));
        var native = Directory.CreateDirectory(Path.Combine(root, "native")).FullName;
        var prototype = Directory.CreateDirectory(Path.Combine(root, "prototype")).FullName;
        var report = Path.Combine(root, "report.json");
        void Write(string path, int width, int height, SKColor color)
        {
            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(color);
            using var stream = File.Create(path);
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        }
        void Reject(string reason)
        {
            try { Run([native, prototype, report]); }
            catch (InvalidOperationException error) when (error.Message.Contains(reason, StringComparison.Ordinal))
            { Console.WriteLine("PASS scorer rejects " + reason); return; }
            throw new InvalidOperationException("Scorer self-test did not reject " + reason);
        }
        foreach (var scene in Scenes)
        {
            Write(Path.Combine(native, scene + ".png"), 1440, 900, SKColors.White);
            Write(Path.Combine(prototype, scene + ".png"), 1440, 900, SKColors.White);
        }
        Run([native, prototype, report]);
        using (var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(report)))
            if (!json.RootElement.GetProperty("passed").GetBoolean()
                || json.RootElement.GetProperty("average").GetDouble() != 100)
                throw new InvalidOperationException("Identical captures must score 100%");
        Console.WriteLine("PASS identical captures score 100%");
        var last = Path.Combine(native, Scenes[^1] + ".png");
        File.Delete(last); Reject("Required capture missing");
        File.WriteAllText(last, "invalid PNG"); Reject("Capture decode failed");
        Write(last, 1440, 898, SKColors.White); Reject("Invalid capture size");
        Write(last, 1440, 900, SKColors.Black); Reject("Pixel parity requires");
        using (var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(report)))
            if (json.RootElement.GetProperty("passed").GetBoolean())
                throw new InvalidOperationException("A failed scene must not report passed");
        Console.WriteLine("PASS scorer self-test complete");
    }
}
