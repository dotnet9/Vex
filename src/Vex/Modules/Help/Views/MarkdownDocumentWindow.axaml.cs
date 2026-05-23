using Ursa.Controls;

namespace Vex.Modules.Help.Views;

public partial class MarkdownDocumentWindow : UrsaWindow
{
    public MarkdownDocumentWindow()
    {
        InitializeComponent();
    }

    public MarkdownDocumentWindow(string title, string markdown, string? typographyTheme, string typographySize)
        : this()
    {
        Title = title;
        DocumentMarkdownViewer.Markdown = markdown;
        DocumentMarkdownViewer.TypographyTheme = typographyTheme;
        DocumentMarkdownViewer.TypographySize = typographySize;
    }
}
