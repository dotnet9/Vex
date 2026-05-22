namespace Vex.Modules.Workspace.Services;

public interface IMarkdownEditorTemplateService
{
    string BoldPlaceholder { get; }

    string ItalicPlaceholder { get; }

    string InlineCodePlaceholder { get; }

    string LinkPlaceholder { get; }

    string LinkUrlPlaceholder { get; }

    string ImageAltPlaceholder { get; }

    string ImageTargetPlaceholder { get; }

    string ImageInsertion { get; }

    string CodeFencePlaceholder { get; }

    string TableInsertion { get; }

    string MathPlaceholder { get; }
}
