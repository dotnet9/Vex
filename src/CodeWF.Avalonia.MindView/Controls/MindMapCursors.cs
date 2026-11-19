using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Input;

namespace CodeWF.Avalonia.MindView.Controls;

/// <summary>
/// 导图画布的抓手光标：Avalonia 的 StandardCursorType 没有 Grab/Grabbing，
/// Windows 用内嵌 .cur 资源；其他平台回退到原有 Hand/SizeAll（调用方按 null 判断）。
/// </summary>
internal static class MindMapCursors
{
    private static Cursor? _grab;
    private static Cursor? _grabbing;
    private static bool _initialized;

    // 热点：张手在指尖，握拳在掌心（图 32×32，为 16×16 底稿的两倍缩放）。
    private static readonly PixelPoint GrabHotspot = new(11, 2);
    private static readonly PixelPoint GrabbingHotspot = new(14, 18);

    /// <summary>常态抓手；null 表示当前平台回退 Hand。</summary>
    public static Cursor? Grab => Initialize();

    /// <summary>拖动中握拳；null 表示当前平台回退 SizeAll。</summary>
    public static Cursor? Grabbing => Initialize();

    private static Cursor? Initialize()
    {
        if (_initialized) return _grab;
        _initialized = true;
        if (!OperatingSystem.IsWindows()) return null;
        _grab = Load("grab.png", GrabHotspot);
        _grabbing = Load("grabbing.png", GrabbingHotspot);
        return _grab;
    }

    private static Cursor? Load(string name, PixelPoint hotspot)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://CodeWF.Avalonia.MindView/Assets/Cursors/{name}"));
            return new Cursor(new Bitmap(stream), hotspot);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
