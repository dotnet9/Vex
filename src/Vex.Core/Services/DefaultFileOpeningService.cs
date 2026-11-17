using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Vex.Core.Services;

public static class DefaultFileOpeningService
{
    private const string ApplicationName = "Vex";
    public static void Configure()
    {
        // Windows 关联由安装选项或用户主动执行的便携脚本管理，启动时尊重未勾选的选择。
        if (OperatingSystem.IsWindows()) return;

        // 离屏测试和其他宿主引用 Vex 程序集时，不注册宿主程序的路径。
        if (Assembly.GetEntryAssembly() != typeof(DefaultFileOpeningService).Assembly) return;

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            GetPlatformIntegration().Configure(executablePath);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Configuring default file opening failed. {exception}");
        }
    }

    private static IDefaultFileOpeningIntegration GetPlatformIntegration()
    {
        if (OperatingSystem.IsLinux())
        {
            return new LinuxDefaultFileOpeningIntegration();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOSDefaultFileOpeningIntegration();
        }

        return NoOpDefaultFileOpeningIntegration.Instance;
    }

    private interface IDefaultFileOpeningIntegration
    {
        void Configure(string executablePath);
    }

    private sealed class NoOpDefaultFileOpeningIntegration : IDefaultFileOpeningIntegration
    {
        public static readonly NoOpDefaultFileOpeningIntegration Instance = new();

        public void Configure(string executablePath)
        {
        }
    }

    private sealed class LinuxDefaultFileOpeningIntegration : IDefaultFileOpeningIntegration
    {
        private const string DesktopFileName = "vex.desktop";
        private static readonly string[] MimeTypes = ["text/markdown", "text/x-markdown"];

        public void Configure(string executablePath)
        {
            var localShare = GetLocalShareDirectory();
            if (string.IsNullOrWhiteSpace(localShare))
            {
                return;
            }

            var applicationsDirectory = Path.Combine(localShare, "applications");
            Directory.CreateDirectory(applicationsDirectory);
            RegisterMarkdownMimeType(localShare);

            var desktopFilePath = Path.Combine(applicationsDirectory, DesktopFileName);
            File.WriteAllText(desktopFilePath, CreateDesktopFile(executablePath), Encoding.UTF8);

            RunCommand("update-desktop-database", applicationsDirectory);
        }

        private static string? GetLocalShareDirectory()
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdgDataHome))
            {
                return xdgDataHome;
            }

            var home = Environment.GetEnvironmentVariable("HOME");
            return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, ".local", "share");
        }

        private static string CreateDesktopFile(string executablePath)
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            var iconLine = File.Exists(iconPath)
                ? $"Icon={EscapeDesktopEntryValue(iconPath)}"
                : string.Empty;

            return string.Join(
                Environment.NewLine,
                "[Desktop Entry]",
                "Type=Application",
                $"Name={ApplicationName}",
                "Comment=Markdown editor",
                $"Exec=\"{executablePath.Replace("\"", "\\\"", StringComparison.Ordinal)}\" %f",
                iconLine,
                "Terminal=false",
                "Categories=Office;TextEditor;",
                $"MimeType={string.Join(';', MimeTypes)};",
                string.Empty);
        }

        private static void RegisterMarkdownMimeType(string localShare)
        {
            var mimePackagesDirectory = Path.Combine(localShare, "mime", "packages");
            Directory.CreateDirectory(mimePackagesDirectory);

            File.WriteAllText(
                Path.Combine(mimePackagesDirectory, "vex-markdown.xml"),
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
                  <mime-type type="text/markdown">
                    <comment>Markdown document</comment>
                    <glob pattern="*.md"/>
                    <glob pattern="*.markdown"/>
                    <glob pattern="*.mdown"/>
                  </mime-type>
                </mime-info>
                """,
                Encoding.UTF8);

            RunCommand("update-mime-database", Path.Combine(localShare, "mime"));
        }

        private static string EscapeDesktopEntryValue(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal);
        }

        private static void RunCommand(string fileName, params string[] arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }.AddArguments(arguments));

                process?.WaitForExit(3000);
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"{fileName} default file opening integration failed. {exception}");
            }
        }
    }

    private sealed class MacOSDefaultFileOpeningIntegration : IDefaultFileOpeningIntegration
    {
        public void Configure(string executablePath)
        {
            var appBundlePath = FindAppBundlePath(executablePath);
            if (appBundlePath is null)
            {
                return;
            }

            RunLsRegister(appBundlePath);
        }

        private static string? FindAppBundlePath(string executablePath)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (directory.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                {
                    return directory;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        private static void RunLsRegister(string appBundlePath)
        {
            RunCommand(
                "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister",
                "-f",
                appBundlePath);
        }

        private static void RunCommand(string fileName, params string[] arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }.AddArguments(arguments));

                process?.WaitForExit(3000);
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"{fileName} default file opening integration failed. {exception}");
            }
        }
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo AddArguments(this ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
