namespace Vex.Modules.Shell.Services;

public sealed record ShellExternalPathResult(ShellExternalPathKind Kind, string? Path = null, string? FileName = null)
{
    public static ShellExternalPathResult None { get; } = new(ShellExternalPathKind.None);

    public static ShellExternalPathResult Missing { get; } = new(ShellExternalPathKind.Missing);

    public static ShellExternalPathResult Unsupported(string path) =>
        new(ShellExternalPathKind.Unsupported, path, System.IO.Path.GetFileName(path));

    public static ShellExternalPathResult File(string path) =>
        new(ShellExternalPathKind.File, path, System.IO.Path.GetFileName(path));

    public static ShellExternalPathResult Folder(string path) =>
        new(ShellExternalPathKind.Folder, path, System.IO.Path.GetFileName(path));
}
