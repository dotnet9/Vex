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
        DocumentMarkdownViewer.Markdown = markdown;
        DocumentMarkdownViewer.ImageBasePath = imageBasePath;
        DocumentMarkdownViewer.TypographyTheme = typographyTheme;
        DocumentMarkdownViewer.TypographySize = typographySize;
    }
}
