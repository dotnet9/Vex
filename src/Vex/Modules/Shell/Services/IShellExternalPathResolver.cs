namespace Vex.Modules.Shell.Services;

public interface IShellExternalPathResolver
{
    ShellExternalPathResult ResolveStartupArgument(IEnumerable<string> arguments);

    ShellExternalPathResult ResolveDroppedPath(string? path);

    ShellExternalPathResult ResolveRecentPath(string? path);
}
