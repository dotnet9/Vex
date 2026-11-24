namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

internal static class MarkdownTableCellText
{
    public static string Normalize(string text) => text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Replace("\n", "<br>", StringComparison.Ordinal);
}
