using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class ShellExternalPathResolver : IShellExternalPathResolver
{
    private readonly IDocumentService _documentService;

    public ShellExternalPathResolver(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public ShellExternalPathResult ResolveStartupArgument(IEnumerable<string> arguments)
    {
        var path = arguments.FirstOrDefault(argument => File.Exists(argument) || Directory.Exists(argument));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ShellExternalPathResult.None;
        }

        return Directory.Exists(path)
            ? ShellExternalPathResult.Folder(path)
            : ShellExternalPathResult.File(path);
    }

    public ShellExternalPathResult ResolveDroppedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ShellExternalPathResult.Missing;
        }

        if (Directory.Exists(path))
        {
            return ShellExternalPathResult.Folder(path);
        }

        if (!File.Exists(path))
        {
            return ShellExternalPathResult.Missing;
        }

        // 拖放入口比启动参数更接近用户即时操作，先过滤不支持的文件，避免误读二进制内容。
        return _documentService.IsSupportedDocumentPath(path)
            ? ShellExternalPathResult.File(path)
            : ShellExternalPathResult.Unsupported(path);
    }

    public ShellExternalPathResult ResolveRecentPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return ShellExternalPathResult.Missing;
        }

        return ShellExternalPathResult.File(path);
    }
}
