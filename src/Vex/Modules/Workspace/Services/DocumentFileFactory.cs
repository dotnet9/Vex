using System.Text;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class DocumentFileFactory : IDocumentFileFactory
{
    private const int PreviewLineLimit = 8;
    private const int PreviewScanCharacterLimit = 4096;
    private const int PreviewStoredLineCharacterLimit = 512;
    private const int PreviewTextLength = 96;
    private readonly IAppLocalizer _localizer;

    public DocumentFileFactory(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public DocumentFile Create(string path, string? workspaceRoot = null)
    {
        // 文件列表展示文案集中在工厂内，避免 DocumentFile 反向依赖运行时本地化服务。
        return new DocumentFile(
            path,
            Path.GetFileName(path),
            ResolveFolderName(path, workspaceRoot),
            GetModifiedText(path),
            string.Empty);
    }

    public string GetModifiedText(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        DateTimeOffset lastWriteTime;
        try
        {
            lastWriteTime = new DateTimeOffset(File.GetLastWriteTime(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }

        var age = DateTimeOffset.Now - lastWriteTime;
        return age.TotalDays switch
        {
            < 1 => lastWriteTime.ToString("HH:mm", _localizer.Culture),
            < 2 => _localizer.Get(VexL.DocumentModifiedYesterday),
            < 8 => _localizer.Format(VexL.DocumentModifiedDaysAgoFormat, (int)age.TotalDays),
            _ => lastWriteTime.ToString("MMM dd", _localizer.Culture)
        };
    }

    private static string ResolveFolderName(string path, string? workspaceRoot)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "Vex";
        }

        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            var relative = Path.GetRelativePath(workspaceRoot, directory);
            if (!relative.StartsWith("..", StringComparison.Ordinal))
            {
                return relative == "." ? Path.GetFileName(workspaceRoot) : relative;
            }
        }

        return Path.GetFileName(directory);
    }

    public string ReadPreview(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return ReadPreview(reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return string.Empty;
        }
    }

    private static string ReadPreview(TextReader reader)
    {
        var linesRead = 0;
        var charactersRead = 0;

        while (linesRead < PreviewLineLimit && charactersRead < PreviewScanCharacterLimit)
        {
            var line = ReadBoundedLine(reader, ref charactersRead);
            if (line is null)
            {
                break;
            }

            linesRead++;
            var trimmed = line.Trim().TrimStart('#', '-', '*', '>', ' ');
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed.Length > PreviewTextLength ? $"{trimmed[..PreviewTextLength]}..." : trimmed;
            }
        }

        return string.Empty;
    }

    private static string? ReadBoundedLine(TextReader reader, ref int charactersRead)
    {
        var builder = new StringBuilder(Math.Min(PreviewStoredLineCharacterLimit, PreviewScanCharacterLimit - charactersRead));
        var sawCharacter = false;

        while (charactersRead < PreviewScanCharacterLimit)
        {
            var value = reader.Read();
            if (value < 0)
            {
                break;
            }

            sawCharacter = true;
            charactersRead++;
            var character = (char)value;
            if (character == '\n')
            {
                break;
            }

            if (character == '\r')
            {
                if (charactersRead < PreviewScanCharacterLimit && reader.Peek() == '\n')
                {
                    reader.Read();
                    charactersRead++;
                }

                break;
            }

            if (builder.Length < PreviewStoredLineCharacterLimit)
            {
                builder.Append(character);
            }
        }

        return sawCharacter || builder.Length > 0 ? builder.ToString() : null;
    }
}
