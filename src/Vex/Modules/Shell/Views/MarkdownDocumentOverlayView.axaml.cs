using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.Views;

public partial class MarkdownDocumentOverlayView : UserControl
{
    public MarkdownDocumentOverlayView()
    {
        InitializeComponent();
    }

    public MarkdownDocumentOverlayView(
        string title,
        string markdown,
        string? imageBasePath,
        string? typographyTheme,
        string typographySize)
        : this()
    {
        DocumentTitleText.Text = title;
        DocumentMarkdownViewer.Markdown = RemoveLeadingHeading(markdown);
        DocumentMarkdownViewer.ImageBasePath = imageBasePath;
        DocumentMarkdownViewer.TypographyTheme = typographyTheme;
        DocumentMarkdownViewer.TypographySize = typographySize;
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) => ShellOverlayHost.Dismiss(this);

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
