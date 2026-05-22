using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.ViewModels;

public sealed class MarkdownPreviewViewModel : ReactiveObject
{
    private readonly IEditorAppearanceState _appearanceState;
    private string _markdown;
    private string _typographySize;
    private string? _typographyTheme;

    public MarkdownPreviewViewModel(
        IEventBus eventBus,
        IWorkspaceDocumentState documentState,
        IEditorAppearanceState appearanceState)
    {
        _appearanceState = appearanceState;
        _markdown = documentState.Markdown;
        _typographySize = appearanceState.TypographySize;
        _typographyTheme = appearanceState.TypographyTheme;
        _appearanceState.Changed += OnAppearanceChanged;
        eventBus.Subscribe(this);
    }

    public string Markdown
    {
        get => _markdown;
        private set => this.RaiseAndSetIfChanged(ref _markdown, value);
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
    }

    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        TypographySize = _appearanceState.TypographySize;
        TypographyTheme = _appearanceState.TypographyTheme;
    }
}
