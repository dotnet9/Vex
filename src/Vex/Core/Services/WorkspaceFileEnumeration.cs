using System.IO.Enumeration;

namespace Vex.Core.Services;

/// <summary>工作区扫描跳过点目录及其后代，显式打开的工作区根目录不受影响。</summary>
internal static class WorkspaceFileEnumeration
{
    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool IsSupportedDocumentPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<(string Path, bool IsDirectory)> EnumerateDirectory(string folder)
    {
        return new FileSystemEnumerable<(string, bool)>(folder,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory),
            CreateOptions(recurse: false))
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                (entry.Attributes & FileAttributes.ReparsePoint) == 0
                && (entry.IsDirectory
                    ? !entry.FileName.StartsWith(".", StringComparison.Ordinal)
                    : IsSupportedDocumentPath(entry.ToFullPath()))
        };
    }

    public static IEnumerable<string> EnumerateFiles(string folder, CancellationToken cancellationToken = default,
        Action<string>? directoryFound = null)
    {
        var paths = new FileSystemEnumerable<string>(folder,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            CreateOptions(recurse: true))
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory
                && (entry.Attributes & FileAttributes.ReparsePoint) == 0,
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
            {
                var include = !entry.FileName.StartsWith(".", StringComparison.Ordinal)
                    && (entry.Attributes & FileAttributes.ReparsePoint) == 0;
                if (include) directoryFound?.Invoke(entry.ToFullPath());
                return include;
            }
        };

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return path;
        }
    }

    public static bool IsVisiblePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".") return true;
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;
        var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        // 最后一段允许普通点文件；目录事件的最后一段由调用方判断。
        return !parts.Take(parts.Length - 1).Any(part => part.StartsWith(".", StringComparison.Ordinal));
    }

    private static EnumerationOptions CreateOptions(bool recurse) => new()
    {
        RecurseSubdirectories = recurse,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}
