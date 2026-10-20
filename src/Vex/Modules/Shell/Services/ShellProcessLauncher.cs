using System.Diagnostics;

namespace Vex.Modules.Shell.Services;

public static class ShellProcessLauncher
{
    public static ProcessStartInfo CreateStartInfo(string? path = null)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Application executable is unavailable.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(Vex.App).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(path)) start.ArgumentList.Add(path);
        return start;
    }
}
