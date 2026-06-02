using Ursa.Controls;

namespace Vex.Modules.Help.Views;

public partial class MarkdownDocumentWindow : UrsaWindow
{
    public MarkdownDocumentWindow()
    {
        InitializeComponent();
    }

    public MarkdownDocumentWindow(string title, string markdown, string? imageBasePath, string? typographyTheme, string typographySize)
        : this()
    {
        Title = title;
        TitleText.Text = title;
        DocumentTitleText.Text = title;
        DocumentMarkdownViewer.Markdown = RemoveLeadingHeading(markdown);
        DocumentMarkdownViewer.ImageBasePath = imageBasePath;
        DocumentMarkdownViewer.TypographyTheme = typographyTheme;
        DocumentMarkdownViewer.TypographySize = typographySize;
    }

    private static string RemoveLeadingHeading(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return markdown;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var headingIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (headingIndex < 0 || !lines[headingIndex].TrimStart().StartsWith("# ", StringComparison.Ordinal))
        {
            return markdown;
        }

        var contentIndex = headingIndex + 1;
        while (contentIndex < lines.Length && string.IsNullOrWhiteSpace(lines[contentIndex]))
        {
            contentIndex++;
        }

        return string.Join(Environment.NewLine, lines.Take(headingIndex).Concat(lines.Skip(contentIndex)));
    }
}
