using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

public partial class MarkdownLiveEditorView
{
    private Popup? _selectionBarPopup;
    private Rect _selectionBarAnchor;

    private void ShowSelectionBar()
    {
        if (_selectionBarPopup is null)
        {
            _selectionBarPopup = new Popup
            {
                PlacementTarget = this, Placement = PlacementMode.Top,
                IsLightDismissEnabled = true, OverlayDismissEventPassThrough = true, OverlayInputPassThroughElement = this,
                ShouldUseOverlayLayer = true,
                Child = new MarkdownSelectionToolbar
                {
                    DataContext = new MarkdownSelectionToolbarViewModel(action => _ = ExecuteAsync(action),
                        ApplySpanStyle, GetSpanStyle, ClearSpanStyles, HideSelectionBar)
                }
            };
            LogicalChildren.Add(_selectionBarPopup);
        }
        _selectionBarPopup.PlacementRect = _selectionBarAnchor.Inflate(new Thickness(0, 8));
        _selectionBarPopup.IsOpen = true;
    }

    private void HideSelectionBar()
    {
        if (_selectionBarPopup is { IsOpen: true }) _selectionBarPopup.IsOpen = false;
    }
}
