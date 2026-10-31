using Avalonia.Input;
using ReactiveUI;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellWorkbenchViewModel
{
    private MarkdownPresentationDocument? _presentationDocument;
    private int _presentationIndex;
    private bool _presentationOutline;
    private bool _presentationMap;
    private string _presentationMarkdown = string.Empty;
    public IReadOnlyList<MarkdownPresentationDocument.Section> PresentationSections => _presentationDocument?.Sections ?? [];
    public int PresentationIndex => _presentationIndex;
    public string PresentationMarkdown => IsPresenting ? _presentationMarkdown : _document.Markdown;
    public string PresentationProgress => $"{_presentationIndex + 1} / {PresentationSections.Count}";
    /// <summary>原型 progress.value=(页码+1)/总页数：2px 底缘进度条的唯一读数。</summary>
    public double PresentationProgressValue => PresentationSections.Count == 0 ? 1 : (_presentationIndex + 1.0) / PresentationSections.Count;
    public bool PresentationOutline => IsPresenting && _presentationOutline;
    public bool PresentationMap => IsPresenting && _presentationMap;
    /// <summary>原型 .deck-scroll 只被导图档隐藏：大纲是右侧同屏列，不与正文互斥。</summary>
    public bool PresentationDocument => IsPresenting && !_presentationMap;
    /// <summary>原型 .workspace{gap:0;padding:0}（workspace-chrome.css:464）：普通态三列平铺贴满；
    /// 演示态整幅出血（.deck-stage），大纲展开时正文按列宽让位。</summary>
    public Avalonia.Thickness WorkspaceContentMargin => IsPresenting
        ? new(0, 0, DeckNavWidth, 0)
        : default;

    /// <summary>原型 .vex-deck{--deck-nav-w:min(280px,25vw)}：大纲列宽，正文与悬浮件都按它让位。</summary>
    public double DeckNavWidth => PresentationOutline ? Math.Min(280, Math.Max(0, _layout.WorkspaceWidth) * .25) : 0;

    /// <summary>悬浮件层：右侧让出整条大纲列，胶囊才能像原型那样在剩余空间里居中。</summary>
    public Avalonia.Thickness DeckChromeMargin => new(0, 0, DeckNavWidth, 0);

    /// <summary>原型 footer：大纲收起时距窗右 40，展开时贴大纲列再留 12。</summary>
    public Avalonia.Thickness DeckDockMargin => new(0, 0, PresentationOutline ? 12 : 40, 0);

    /// <summary>原型 .deck-outline-toggle：24×24 圆钮，底距 18（圆心落回 30 轴线）；展开时嵌进大纲列左缘 32px。</summary>
    public Avalonia.Thickness DeckOutlineToggleMargin => new(0, 0, PresentationOutline ? Math.Max(0, DeckNavWidth - 32) : 8, 18);

    /// <summary>原型 [data-deck="scale"] 一条拖动条两用：正文 1–2.5 步长 0.1，导图 0.15–3 步长 0.05。</summary>
    public double DeckZoomMin => PresentationMap ? .15 : 1;
    public double DeckZoomMax => PresentationMap ? 3 : 2.5;
    public double DeckZoomStep => PresentationMap ? .05 : .1;

    /// <summary>拖动条读数按当前档位落到对应状态：正文是持久化的字号倍率，导图是本次演示的视口倍率。</summary>
    public double DeckScale
    {
        get => PresentationMap ? PresentationMapScale : PresentationZoom;
        set
        {
            if (PresentationMap) PresentationMapScale = value;
            else PresentationZoomValue = Math.Round(value, 1);
        }
    }

    public string DeckZoomLabel => L(PresentationMap ? VexL.ZoomMap : VexL.ZoomPresentation);
    public string DeckResetLabel => L(PresentationMap ? VexL.WBFit : VexL.ZoomRestoreFont);
    public bool DeckShowsProgress => IsPresenting;
    public void ResetPresentationZoom() => PresentationZoomValue = _presentationInitialZoom;

    public bool CanPreviousPresentation => _presentationIndex > 0;
    public bool CanNextPresentation => _presentationIndex + 1 < PresentationSections.Count;
    // 原型三组状态各自独立：大纲开合不切视图，阅读/导图切换不收大纲，翻页两者都保持不变。
    public void TogglePresentationOutline() { _presentationOutline = !_presentationOutline; NotifyPresentation(); }
    public void ShowPresentationDocument() { _presentationMap = false; NotifyPresentation(); }
    public void ShowPresentationOutline() { _presentationOutline = true; NotifyPresentation(); }
    public void ShowPresentationMap() { _presentationMap = true; NotifyPresentation(); }
    public void PreviousPresentation() => NavigatePresentation(_presentationIndex - 1);
    public void NextPresentation() => NavigatePresentation(_presentationIndex + 1);
    public void NavigatePresentation(int index)
    {
        if (!IsPresenting || index < 0 || index >= PresentationSections.Count) return;
        if (_presentationIndex != index) _presentationMarkdown = _presentationDocument!.MarkdownAt(index);
        _presentationIndex = index;
        NotifyPresentation();
    }
    private void BuildPresentation()
    {
        _presentationDocument = new(_document.Markdown, Path.GetFileNameWithoutExtension(_document.FilePath) ?? "Vex");
        _presentationIndex = 0;
        _presentationMarkdown = _presentationDocument.MarkdownAt(0);
        this.RaisePropertyChanged(nameof(PresentationSections));
    }
    public bool HandlePresentationKey(Key key)
    {
        if (!IsPresenting) return false;
        if (key is Key.Escape or Key.F11 or Key.F6) { StopPresentation(); return true; }
        if (key is Key.Add or Key.OemPlus) { IncreasePresentationText(); return true; }
        if (key is Key.Subtract or Key.OemMinus) { DecreasePresentationText(); return true; }
        if (key == Key.O) { TogglePresentationOutline(); return true; }
        if (key == Key.M) { if (PresentationMap) ShowPresentationDocument(); else ShowPresentationMap(); return true; }
        if (key == Key.D) { ShowPresentationDocument(); return true; }
        if (PresentationMap) return false;
        if (key is Key.Right or Key.PageDown || key == Key.Space && !PresentationOutline) { NextPresentation(); return true; }
        if (key is Key.Left or Key.PageUp) { PreviousPresentation(); return true; }
        if (key == Key.Home) { NavigatePresentation(0); return true; }
        if (key == Key.End) { NavigatePresentation(PresentationSections.Count - 1); return true; }
        return false;
    }
}
