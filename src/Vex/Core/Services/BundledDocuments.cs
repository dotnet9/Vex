namespace Vex.Core.Services;

/// <summary>随应用发布的文档；macOS 文档放在应用包的 Resources 目录。</summary>
public static class BundledDocuments
{
    public static string SamplesFolder => Path.Combine(Folder, "演示文档");

    public static string Folder
    {
        get
        {
            var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "docs"));
            return OperatingSystem.IsMacOS() && Directory.Exists(resources)
                ? resources : Path.Combine(AppContext.BaseDirectory, "docs");
        }
    }

    public static string GetPath(string fileName)
    {
        var path = Path.Combine(Folder, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException("Bundled document is missing.", path);
        return path;
    }
}
