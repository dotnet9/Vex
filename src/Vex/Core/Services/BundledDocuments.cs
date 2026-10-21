namespace Vex.Core.Services;

/// <summary>随应用发布的文档；macOS 文档放在应用包的 Resources 目录。</summary>
public static class BundledDocuments
{
    public static string Folder => OperatingSystem.IsMacOS()
        ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "docs"))
        : Path.Combine(AppContext.BaseDirectory, "docs");

    public static string GetPath(string fileName)
    {
        var path = Path.Combine(Folder, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException("Bundled document is missing.", path);
        return path;
    }
}
