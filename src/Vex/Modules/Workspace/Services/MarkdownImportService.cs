using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// 导入 HTML / Word (.docx) / PDF 为 Markdown（对应原型文件菜单「导入」）。
/// HTML 走共享转换器；docx 解包 OOXML 取段落与标题样式；PDF 尽力解压内容流提取文本行。
/// </summary>
public static partial class MarkdownImportService
{
    public static string ConvertHtml(string html) =>
        CodeWF.Avalonia.Markdown.MarkdownHtmlClipboard.Html2Markdown(html) ?? string.Empty;

    public static string ConvertDocx(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var document = archive.GetEntry("word/document.xml");
        if (document is null) return string.Empty;
        using var stream = document.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xml = reader.ReadToEnd();
        var builder = new StringBuilder();
        foreach (Match paragraph in ParagraphXml().Matches(xml))
        {
            var style = StyleXml().Match(paragraph.Value) is { Success: true } match ? match.Groups[1].Value : string.Empty;
            var text = string.Concat(TextXml().Matches(paragraph.Value).Select(match => match.Groups[1].Value));
            if (string.IsNullOrWhiteSpace(text)) continue;
            builder.AppendLine(HeadingPrefix(style) + text.Trim());
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd() + "\n";
    }

    /// <summary>尽力而为的 PDF 文本提取：解压内容流并收集文本显示算子；无依赖、纯文本级。</summary>
    public static string ConvertPdf(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var builder = new StringBuilder();
        foreach (Match stream in StreamXml().Matches(raw))
        {
            string content;
            try
            {
                var compressed = Convert.FromBase64CharArray(
                    stream.Groups[1].Value.ToCharArray(), 0, stream.Groups[1].Value.Length);
                using var input = new MemoryStream(CompressedBytes(stream.Groups[1].Value));
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                content = Encoding.Latin1.GetString(output.ToArray());
            }
            catch
            {
                content = stream.Groups[1].Value;
            }
            foreach (Match text in PdfText().Matches(content))
            {
                var line = DecodePdfString(text.Groups[1].Value).Trim();
                if (line.Length > 0) builder.AppendLine(line);
            }
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd() + "\n";
    }

    private static byte[] CompressedBytes(string hexOrRaw)
    {
        // 内容流在 Latin1 字符串里：逐字符还原字节。
        var bytes = new byte[hexOrRaw.Length];
        for (var i = 0; i < hexOrRaw.Length; i++) bytes[i] = (byte)(hexOrRaw[i] & 0xFF);
        return bytes;
    }

    private static string DecodePdfString(string escaped)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] == '\\' && i + 1 < escaped.Length)
            {
                i++;
                builder.Append(escaped[i] switch
                {
                    'n' => '\n', 'r' => '\r', 't' => '\t',
                    var other => other,
                });
            }
            else
            {
                builder.Append(escaped[i]);
            }
        }
        return builder.ToString();
    }

    private static string HeadingPrefix(string style) => RegexFallback(style);

    private static string RegexFallback(string style) => style.ToLowerInvariant() switch
    {
        "heading1" or "1" or "t1" => "# ",
        "heading2" or "2" or "t2" => "## ",
        "heading3" or "3" or "t3" => "### ",
        "heading4" or "4" or "t4" => "#### ",
        "heading5" or "5" or "t5" => "##### ",
        "heading6" or "6" or "t6" => "###### ",
        "title" => "# ",
        _ => string.Empty,
    };

    [GeneratedRegex("<w:p [^>]*>.*?</w:p>|<w:p>.*?</w:p>", RegexOptions.Singleline)]
    private static partial Regex ParagraphXml();

    [GeneratedRegex("<w:pStyle w:val=\"([^\"]+)\"")]
    private static partial Regex StyleXml();

    [GeneratedRegex("<w:t[^>]*>(.*?)</w:t>", RegexOptions.Singleline)]
    private static partial Regex TextXml();

    [GeneratedRegex("stream\\r?\\n(.*?)endstream", RegexOptions.Singleline)]
    private static partial Regex StreamXml();

    [GeneratedRegex("\\(([^)]*)\\)\\s*Tj|\\[(.*?)\\]\\s*TJ", RegexOptions.Singleline)]
    private static partial Regex PdfText();
}
