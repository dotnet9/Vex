using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Vex.Modules.Shell.Services;

public sealed class ShellDroppedPathReader : IShellDroppedPathReader
{
    public string? GetFirstLocalFile(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return null;
        }

        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (IsAvailableLocalFile(path))
            {
                return path;
            }
        }

        return null;
    }

    private static bool IsAvailableLocalFile(string? path)
    {
        // 拖放事件可能包含虚拟文件或空路径，先只接收可由本地文件系统访问的文件。
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    /// <summary>拖入的路径是否为受支持的图片扩展名。</summary>
    public static bool IsSupportedImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif"
            or ".bmp" or ".webp" or ".svg" or ".ico" or ".tif" or ".tiff" or ".avif";
    }
}
