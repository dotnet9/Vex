namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// <see cref="Controls.MarkdownViewer" /> 输出的统一样式类名与资源 Key。
/// 主题包只依赖这些常量，不需要感知渲染器内部控件结构。
/// </summary>
public static class MarkdownStyleKeys
{
    public const string Document = "MdDocument";
    public const string DocumentBottomSpacer = "MdDocumentBottomSpacer";
    public const string SelectedBlock = "MdSelectedBlock";
    public const string Paragraph = "MdParagraph";
    public const string Heading = "MdHeading";
    public const string HeadingBorder = "MdHeadingBorder";
    public const string Heading1 = "MdH1";
    public const string Heading2 = "MdH2";
    public const string Heading3 = "MdH3";
    public const string Heading4 = "MdH4";
    public const string Heading5 = "MdH5";
    public const string Heading6 = "MdH6";
    public const string Heading1Border = "MdH1Border";
    public const string Heading2Border = "MdH2Border";
    public const string Heading3Border = "MdH3Border";
    public const string Heading4Border = "MdH4Border";
    public const string Heading5Border = "MdH5Border";
    public const string Heading6Border = "MdH6Border";
    public const string CodeBlock = "MdCodeBlock";
    public const string CodeBlockContent = "MdCodeBlockContent";
    public const string CodeBlockHeader = "MdCodeBlockHeader";
    public const string CodeBlockScrollViewer = "MdCodeBlockScrollViewer";
    public const string CodeBlockText = "MdCodeBlockText";
    public const string CodeLanguage = "MdCodeLanguage";
    public const string CopyButton = "MdCopyButton";
    public const string CodeBlockToolbar = "MdCodeBlockToolbar";
    public const string CodeBlockToolButton = "MdCodeBlockToolButton";
    public const string CodeBlockFooter = "MdCodeBlockFooter";
    public const string CodeExpandButton = "MdCodeExpandButton";
    public const string List = "MdList";
    public const string ListItem = "MdListItem";
    public const string ListItemContent = "MdListItemContent";
    public const string ListMarker = "MdListMarker";
    public const string TaskMarkerBox = "MdTaskMarkerBox";
    public const string Quote = "MdQuote";
    public const string QuoteContent = "MdQuoteContent";
    public const string ThematicBreak = "MdThematicBreak";
    public const string TableContainer = "MdTableContainer";
    public const string TableBlock = "MdTableBlock";
    public const string Table = "MdTable";
    public const string TableHeaderCell = "MdTableHeaderCell";
    public const string TableCell = "MdTableCell";
    public const string TableCellContent = "MdTableCellContent";
    public const string HtmlBlock = "MdHtmlBlock";
    public const string UnknownBlock = "MdUnknownBlock";
    public const string InlineCode = "MdInlineCode";
    public const string InlineCodeText = "MdInlineCodeText";
    public const string Image = "MdImage";
    public const string ImageContent = "MdImageContent";
    public const string ImageFallback = "MdImageFallback";
    public const string ImageFallbackText = "MdImageFallbackText";
    public const string ImagePreviewWindow = "MdImagePreviewWindow";
    public const string ImagePreviewToolbar = "MdImagePreviewToolbar";
    public const string ImagePreviewButton = "MdImagePreviewButton";
    public const string ImagePreviewZoomText = "MdImagePreviewZoomText";
    public const string ImagePreviewContent = "MdImagePreviewContent";
    public const string MediaZoomDialog = "MdMediaZoomDialog";
    public const string MediaZoomDialogBackdrop = "MdMediaZoomDialogBackdrop";
    public const string MediaZoomDialogContent = "MdMediaZoomDialogContent";
    public const string Link = "MdLink";

    public const string TextBrushResource = "TextBlockDefaultForeground";
    public const string MutedTextBrushResource = "TextBlockDisabledForeground";
    public const string BorderBrushResource = "BorderCardBorderBrush";
    public const string AccentBrushResource = "ButtonSolidPrimaryBackground";
    public const string AccentForegroundBrushResource = "ButtonSolidForeground";
    public const string QuoteBackgroundBrushResource = "BorderCardBackground";
    public const string InlineCodeBackgroundBrushResource = "TextBoxDefaultBackground";
    public const string TableHeaderBackgroundBrushResource = "ButtonDefaultBackground";
    public const string CodeBackgroundBrushResource = "TextBlockCodeBackground";
    public const string SelectedBlockBrushResource = "TextBlockSelectionBackground";
    public const string CodeBlockFontSizeResource = "CodeWFMarkdownCodeBlockFontSize";
    public const string ParagraphFontSizeResource = "CodeWFMarkdownParagraphFontSize";
    public const string ParagraphLineHeightResource = "CodeWFMarkdownParagraphLineHeight";
    public const string Heading1FontSizeResource = "CodeWFMarkdownHeading1FontSize";
    public const string Heading2FontSizeResource = "CodeWFMarkdownHeading2FontSize";
    public const string Heading3FontSizeResource = "CodeWFMarkdownHeading3FontSize";
    public const string Heading4FontSizeResource = "CodeWFMarkdownHeading4FontSize";
    public const string Heading5FontSizeResource = "CodeWFMarkdownHeading5FontSize";
    public const string Heading6FontSizeResource = "CodeWFMarkdownHeading6FontSize";
    public const string TypographyThemeResource = "CodeWFMarkdownTypographyTheme";
    public const string TypographySizeResource = "CodeWFMarkdownTypographySize";
    public const string TypographyBaseResourcesResource = "CodeWFMarkdownTypographyBaseResources";

    public static string GetHeadingClass(int level)
    {
        return level switch
        {
            1 => Heading1,
            2 => Heading2,
            3 => Heading3,
            4 => Heading4,
            5 => Heading5,
            _ => Heading6
        };
    }

    public static string GetHeadingBorderClass(int level)
    {
        return level switch
        {
            1 => Heading1Border,
            2 => Heading2Border,
            3 => Heading3Border,
            4 => Heading4Border,
            5 => Heading5Border,
            _ => Heading6Border
        };
    }

}
