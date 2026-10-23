using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// HTML / PDF / Word 导入：把源文件转换为 Markdown 文本。
/// HTML 走 ReverseMarkdown；PDF 走 PdfPig 按页抽文本；Word 解 docx 包并映射标题样式。
/// </summary>
public static class DocumentImportService
{
    public static string FromHtml(string html)
    {
        var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
        });
        return Normalize(converter.Convert(html));
    }

    public static string FromWord(string docxPath)
    {
        using var archive = ZipFile.OpenRead(docxPath);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("The document package does not contain word/document.xml.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        var lines = new List<string>();
        foreach (Match paragraph in Regex.Matches(xml, "<w:p[ >].*?</w:p>", RegexOptions.Singleline))
        {
            var body = paragraph.Value;
            var style = Regex.Match(body, "<w:pStyle w:val=\"([^\"]+)\"").Groups[1].Value;
            var text = string.Concat(Regex.Matches(body, "<w:t[^>]*>(.*?)</w:t>", RegexOptions.Singleline)
                .Select(match => match.Groups[1].Value));
            if (string.IsNullOrWhiteSpace(text))
            {
                if (lines.Count > 0 && lines[^1].Length > 0) lines.Add(string.Empty);
                continue;
            }
            var heading = Regex.Match(style, @"^Heading(\d)$", RegexOptions.IgnoreCase) is { Success: true } level
                ? new string('#', int.Parse(level.Groups[1].Value)) + " "
                : string.Empty;
            lines.Add(heading + text.Trim());
        }
        return Normalize(string.Join("\n", lines));
    }

    public static string FromPdf(string pdfPath)
    {
        var lines = new List<string>();
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text)) continue;
            lines.Add(text.Replace("\r\n", "\n").Trim());
            lines.Add(string.Empty);
        }
        return Normalize(lines.Count > 0 ? string.Join("\n", lines) : string.Empty);
    }

    private static string Normalize(string markdown) => markdown.Replace("\r\n", "\n").Trim() + "\n";
}
