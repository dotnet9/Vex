using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class RecentDocumentStore : IRecentDocumentStore
{
    public IReadOnlyList<RecentDocument> Load(int maxCount)
    {
        if (maxCount <= 0 || !File.Exists(RecentDocumentsPath))
        {
            return [];
        }

        try
        {
            return File.ReadLines(RecentDocumentsPath)
                .Select(TryCreateRecentDocument)
                .OfType<RecentDocument>()
                .Where(document => File.Exists(document.Path))
                .DistinctBy(document => document.Path, StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<RecentDocument> documents)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecentDocumentsPath)!);
            File.WriteAllLines(RecentDocumentsPath, documents.Select(document => document.Path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 最近文件保存失败不应打断当前文档操作，后续可接入统一错误提示。
        }
    }

    private static RecentDocument? TryCreateRecentDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            // 最近文件是轻量状态，加载时只接受系统能规范化的本地路径，避免坏数据影响启动。
            return new RecentDocument(Path.GetFullPath(path));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string RecentDocumentsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeWF",
            "Vex",
            "recent-files.txt");
}
