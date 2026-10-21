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
    public bool PresentationOutline => IsPresenting && _presentationOutline;
    public bool PresentationMap => IsPresenting && _presentationMap;
    public bool PresentationDocument => IsPresenting && !_presentationOutline && !_presentationMap;
    public Avalonia.Thickness PresentationContentMargin => IsPresenting ? new(0, 70, 0, 20) : default;
    public bool CanPreviousPresentation => _presentationIndex > 0;
    public bool CanNextPresentation => _presentationIndex + 1 < PresentationSections.Count;
    public void TogglePresentationOutline() { _presentationOutline = !_presentationOutline; _presentationMap = false; NotifyPresentation(); }
    public void ShowPresentationDocument() { _presentationOutline = false; _presentationMap = false; NotifyPresentation(); }
    public void ShowPresentationOutline() { _presentationOutline = true; _presentationMap = false; NotifyPresentation(); }
    public void ShowPresentationMap() { _presentationOutline = false; _presentationMap = true; NotifyPresentation(); }
    public void PreviousPresentation() => NavigatePresentation(_presentationIndex - 1);
    public void NextPresentation() => NavigatePresentation(_presentationIndex + 1);
    public void NavigatePresentation(int index)
    {
        if (!IsPresenting || index < 0 || index >= PresentationSections.Count) return;
        if (_presentationIndex != index) _presentationMarkdown = _presentationDocument!.MarkdownAt(index);
        _presentationIndex = index;
        _presentationOutline = false;
        _presentationMap = false;
        NotifyPresentation();
    }
    private void BuildPresentation()
    {
        _presentationDocument = new(_document.Markdown, Path.GetFileNameWithoutExtension(_document.FilePath) ?? "Vex");
        _presentationIndex = 0;
        _presentationMarkdown = _presentationDocument.MarkdownAt(0);
        _presentationOutline = false;
        this.RaisePropertyChanged(nameof(PresentationSections));
    }
    public bool HandlePresentationKey(Key key)
    {
        if (!IsPresenting) return false;
        if (key is Key.Escape or Key.F11 or Key.F5) { StopPresentation(); return true; }
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
