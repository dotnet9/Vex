using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace Vex.Core.Services;

public static class DefaultFileOpeningService
{
    private const string ApplicationName = "Vex";
    private static readonly string[] MarkdownExtensions = [".md", ".markdown", ".mdown"];

    public static void Configure()
    {
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
        if (OperatingSystem.IsWindows())
        {
            return new WindowsDefaultFileOpeningIntegration();
        }

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

    [SupportedOSPlatform("windows")]
    private sealed class WindowsDefaultFileOpeningIntegration : IDefaultFileOpeningIntegration
    {
        private const string ProgId = "Vex.Markdown";

        public void Configure(string executablePath)
        {
            RegisterProgId(executablePath);
            foreach (var extension in MarkdownExtensions)
            {
                RegisterExtension(extension);
            }
        }

        private static void RegisterProgId(string executablePath)
        {
            using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
            progIdKey?.SetValue(null, "Markdown Document");
            progIdKey?.SetValue("FriendlyTypeName", "Markdown Document");

            using var defaultIconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon");
            defaultIconKey?.SetValue(null, $"\"{executablePath}\",0");

            using var commandKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command");
            commandKey?.SetValue(null, $"\"{executablePath}\" \"%1\"");

            var executableName = Path.GetFileName(executablePath);
            using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{executableName}");
            appKey?.SetValue("FriendlyAppName", ApplicationName);

            using var appCommandKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\Applications\{executableName}\shell\open\command");
            appCommandKey?.SetValue(null, $"\"{executablePath}\" \"%1\"");
        }

        private static void RegisterExtension(string extension)
        {
            using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            extensionKey?.SetValue(null, ProgId);

            using var openWithKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");
            openWithKey?.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
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

            foreach (var mimeType in MimeTypes)
            {
                RunXdgMime("default", DesktopFileName, mimeType);
            }
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

        private static void RunXdgMime(params string[] arguments)
        {
            RunCommand("xdg-mime", arguments);
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
