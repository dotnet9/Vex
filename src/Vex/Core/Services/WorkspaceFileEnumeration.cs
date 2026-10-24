using System.IO.Enumeration;

namespace Vex.Core.Services;

/// <summary>工作区扫描跳过点目录及其后代，显式打开的工作区根目录不受影响。</summary>
internal static class WorkspaceFileEnumeration
{
    public static IEnumerable<string> EnumerateFiles(string folder)
    {
        return new FileSystemEnumerable<string>(folder,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !entry.FileName.StartsWith(".", StringComparison.Ordinal)
        };
    }
}
