using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.ViewModels;

public sealed class MarkdownPreviewViewModel : ReactiveObject
{
    private readonly IEditorAppearanceState _appearanceState;
    private string? _imageBasePath;
    private string _markdown;
    private string _sourceMarkdown;
    private int _previewSourceLine = 1;
    private double _previewScrollRatio;
    private int _remoteImageRefreshVersion;
    private string _typographySize;
    private string? _typographyTheme;

    public MarkdownPreviewViewModel(
        IWorkspaceDocumentState documentState,
        IEditorAppearanceState appearanceState)
    {
        _appearanceState = appearanceState;
        _imageBasePath = documentState.FilePath;
        _sourceMarkdown = documentState.Markdown;
        _markdown = _sourceMarkdown;
        _typographySize = appearanceState.TypographySize;
        _typographyTheme = appearanceState.TypographyTheme;
        _appearanceState.Changed += OnAppearanceChanged;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
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

    /// <summary>
    /// 远程图片刷新信号：数值递增即要求预览视图调用
    /// <c>MarkdownViewer.RefreshRemoteImages()</c> 失效图片缓存，
    /// 不再通过给 URL 追查询参数强制刷新。
    /// </summary>
    public int RemoteImageRefreshVersion
    {
        get => _remoteImageRefreshVersion;
        private set => this.RaiseAndSetIfChanged(ref _remoteImageRefreshVersion, value);
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
        SetDocument(command.Markdown, command.FilePath);
    }

    [EventHandler]
    public void ApplyMarkdownPreviewRefresh(MarkdownPreviewRefreshCommand command)
    {
        SetDocument(command.Markdown, command.FilePath);

        // 文档内容与图片地址都没变时（例如同一文件的外部改动），仍要强制重取远程图片。
        RemoteImageRefreshVersion++;
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

    private void SetDocument(string? markdown, string? filePath)
    {
        _sourceMarkdown = markdown ?? string.Empty;
        ImageBasePath = filePath;
        Markdown = _sourceMarkdown;
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
