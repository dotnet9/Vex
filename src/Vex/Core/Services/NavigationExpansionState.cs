namespace Vex.Core.Services;

/// <summary>配置值使用转义后的独立行，避免路径和标题中的分隔符互相干扰，支持 NativeAOT。</summary>
public static class NavigationExpansionState
{
    public static string PathKey(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    public static HashSet<string> Read(string? value) => new(
        (value ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    public static string Encode(string key) => Uri.EscapeDataString(key);

    public static string Set(string? value, string key, bool collapsed)
    {
        var entries = Read(value);
        if (collapsed) entries.Add(key); else entries.Remove(key);
        return string.Join('\n', entries.Order(StringComparer.Ordinal));
    }
}
