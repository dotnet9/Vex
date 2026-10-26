using System.ComponentModel;
using Avalonia;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.ViewModels;

public sealed class MarkdownPreviewViewModel : ReactiveObject
{
    public EditorPreferences Preferences { get; }
    public double BodyFontSize => Preferences.ResolveBodyFontSize(TypographyTheme);
    private readonly IEditorAppearanceState _appearanceState;
    private readonly Vex.Modules.Shell.ViewModels.ShellWorkbenchViewModel _workbench;
    public bool IsTaskEditingEnabled => _workbench.IsSourceEditing;
    public bool IsPresenting => _workbench.IsPresenting;
    public double PresentationZoom => IsPresenting ? _workbench.PresentationZoom : 1;
    private string? _imageBasePath;
    private string _markdown;
    private string _sourceMarkdown;
    private string? _sourceImageBasePath;
    private int _previewSourceLine = 1;
    private double _previewScrollRatio;
    private int _remoteImageRefreshVersion;
    private string _typographySize;
    private string? _typographyTheme;
    private double _notifiedPresentationZoom = 1;
    private bool _notifiedIsPresenting;

    public MarkdownPreviewViewModel(
        IWorkspaceDocumentState documentState,
        EditorPreferences preferences,
        IEditorAppearanceState appearanceState,
        Vex.Modules.Shell.ViewModels.ShellWorkbenchViewModel workbench)
    {
        Preferences = preferences;
        preferences.PreferencesChanged += (_, _) => this.RaisePropertyChanged(nameof(BodyFontSize));
        _workbench = workbench;
        _notifiedIsPresenting = workbench.IsPresenting;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        _appearanceState = appearanceState;
        _sourceImageBasePath = documentState.FilePath;
        _imageBasePath = workbench.IsPreviewVisible ? _sourceImageBasePath : null;
        _sourceMarkdown = documentState.Markdown;
        _markdown = workbench.IsPreviewVisible ? _sourceMarkdown : string.Empty;
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
        set => this.RaiseAndSetIfChanged(ref _previewScrollRatio, value);
    }

    /// <summary>
    /// 需要预览定位到的源码行。除编辑器光标同步外，大纲跳转也会直接写入这里；
    /// 值未变化时不会重新定位，因此重复点击同一章节由视图按指令序号强制发起。
    /// </summary>
    public int PreviewSourceLine
    {
        get => _previewSourceLine;
        set => this.RaiseAndSetIfChanged(ref _previewSourceLine, value);
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
        private set { this.RaiseAndSetIfChanged(ref _typographyTheme, value); this.RaisePropertyChanged(nameof(BodyFontSize)); }
    }

    /// <summary>
    /// 原型 .reading-zoom 的横向落点：坞离块右缘 12px，右栏收起时折叠钮贴到窗边，让开一枚钮宽加 8px 缝到 31px。
    /// 纵向不写在这里，由 vex:VexBottomAxis 按 --vex-axis-view 把中心拉回底部轴线上。
    /// </summary>
    public Thickness ReadingDockMargin => new(0, 0, _workbench.ShowOutlineSidebar ? 12 : 31, 0);

    /// <summary>演示态的缩放在演示 chrome 自己的坞上，正文坞让位（原型 .presentation-mode 不渲染阅读坞）。</summary>
    public bool ShowReadingDock => !IsPresenting;

    /// <summary>原型还原钮：正文缩放回到 100%。</summary>
    public void ResetReadingZoom() => Preferences.ContentZoom = 100;

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
        _sourceImageBasePath = filePath;
        SynchronizeVisibleDocument();
    }

    private void SynchronizeVisibleDocument(bool restoreHidden = false)
    {
        // Keep the latest source while hidden without parsing or building another document tree.
        if (!restoreHidden && !_workbench.IsPreviewVisible && !IsPresenting) return;
        ImageBasePath = _sourceImageBasePath;
        Markdown = IsPresenting ? _workbench.PresentationMarkdown : _sourceMarkdown;
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 入场先让视图保存全文阅读锚点，再替换成演示章节；退出则先恢复全文。
        var enteringPresentation = e.PropertyName == nameof(_workbench.IsPresenting) && IsPresenting;
        if (enteringPresentation) this.RaisePropertyChanged(nameof(IsPresenting));
        // StopPresentation restores layout before publishing IsPresenting. Wait for that
        // boundary so the view can preserve its reading anchor before restoring the full text.
        if (e.PropertyName is nameof(_workbench.PresentationMarkdown) or nameof(_workbench.IsPresenting)
            || e.PropertyName == nameof(_workbench.IsPreviewVisible) && _notifiedIsPresenting == IsPresenting)
            // 退出演示时正文列可能已经隐藏（例如回到即时视图）：这里必须无条件回落全文，
            // 否则演示章节会留在正文里，后续任何不再触发上面事件的显露都只重绘出那一节。
            SynchronizeVisibleDocument(restoreHidden: !IsPresenting && _notifiedIsPresenting);
        if (e.PropertyName == nameof(_workbench.IsPresenting)) _notifiedIsPresenting = IsPresenting;
        if (e.PropertyName == nameof(_workbench.IsPresenting)) this.RaisePropertyChanged(nameof(ShowReadingDock));
        if (e.PropertyName == nameof(_workbench.ShowOutlineSidebar)) this.RaisePropertyChanged(nameof(ReadingDockMargin));
        if (e.PropertyName == nameof(_workbench.IsPresenting) && !enteringPresentation) this.RaisePropertyChanged(nameof(IsPresenting));
        if (e.PropertyName is nameof(_workbench.IsPresenting) or nameof(_workbench.PresentationZoom)
            && _notifiedPresentationZoom != PresentationZoom)
        { _notifiedPresentationZoom = PresentationZoom; this.RaisePropertyChanged(nameof(PresentationZoom)); }
        if (e.PropertyName == nameof(_workbench.IsSourceEditing))
        {
            this.RaisePropertyChanged(nameof(IsTaskEditingEnabled));
        }
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
