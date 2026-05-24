using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.ViewModels;

public sealed class MarkdownPreviewViewModel : ReactiveObject
{
    private readonly IEditorAppearanceState _appearanceState;
    private string? _imageBasePath;
    private string _markdown;
    private int _previewSourceLine = 1;
    private double _previewScrollRatio;
    private string _typographySize;
    private string? _typographyTheme;

    public MarkdownPreviewViewModel(
        IWorkspaceDocumentState documentState,
        IEditorAppearanceState appearanceState)
    {
        _appearanceState = appearanceState;
        _imageBasePath = documentState.FilePath;
        _markdown = documentState.Markdown;
        _typographySize = appearanceState.TypographySize;
        _typographyTheme = appearanceState.TypographyTheme;
        _appearanceState.Changed += OnAppearanceChanged;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    public string Markdown
    {
        get => _markdown;
        private set => this.RaiseAndSetIfChanged(ref _markdown, value);
    }

    public string? ImageBasePath
    {
        get => _imageBasePath;
        private set => this.RaiseAndSetIfChanged(ref _imageBasePath, value);
    }

    public double PreviewScrollRatio
    {
        get => _previewScrollRatio;
        private set => this.RaiseAndSetIfChanged(ref _previewScrollRatio, value);
    }

    public int PreviewSourceLine
    {
        get => _previewSourceLine;
        private set => this.RaiseAndSetIfChanged(ref _previewSourceLine, value);
    }

    public string TypographySize
    {
        get => _typographySize;
        private set => this.RaiseAndSetIfChanged(ref _typographySize, value);
    }

    public string? TypographyTheme
    {
        get => _typographyTheme;
        private set => this.RaiseAndSetIfChanged(ref _typographyTheme, value);
    }

    [EventHandler]
    public void ApplyMarkdownDocumentChanged(MarkdownDocumentChangedCommand command)
    {
        Markdown = command.Markdown;
        ImageBasePath = command.FilePath;
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        PreviewSourceLine = command.CaretLine;
        PreviewScrollRatio = CalculatePreviewScrollRatio(command.CaretLine, command.LineCount);
    }

    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        TypographySize = _appearanceState.TypographySize;
        TypographyTheme = _appearanceState.TypographyTheme;
    }

    private static double CalculatePreviewScrollRatio(int caretLine, int lineCount)
    {
        if (lineCount <= 1)
        {
            return 0d;
        }

        var lineIndex = Math.Clamp(caretLine, 1, lineCount) - 1;
        return lineIndex / (double)(lineCount - 1);
    }
}
