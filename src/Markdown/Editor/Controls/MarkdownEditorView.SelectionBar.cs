using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

public partial class MarkdownEditorView
{
    private Popup? _selectionBarPopup;

    private void InitializeSelectionToolbar()
    {
        _editor.AddHandler(PointerPressedEvent, (_, _) => HideSelectionToolbar(), RoutingStrategies.Tunnel, true);
        _editor.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left) Dispatcher.UIThread.Post(ShowSelectionToolbar);
        }, RoutingStrategies.Bubble, true);
        _editor.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) HideSelectionToolbar();
            else Dispatcher.UIThread.Post(ShowSelectionToolbar);
        }, RoutingStrategies.Bubble, true);
        _editor.TextChanged += (_, _) => HideSelectionToolbar();
        _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => HideSelectionToolbar();
        DetachedFromVisualTree += (_, _) => HideSelectionToolbar();
    }

    /// <summary>
    /// 选区工具条扩展槽（2026-11-13，随 12.1.6.5）：宿主可在拖选工具条尾部追加自定义按钮
    /// （如 Vox 的「添加到 AI 会话」）。默认空集合时工具条与既有行为完全一致，既有消费者不受影响。
    /// extras 在工具条视觉树内，可直接复用 .format 样式类与 CodeWFMarkdownSelection* 资源。
    /// </summary>
    public IList<Control> SelectionBarExtras { get; } = new List<Control>();

    private void ShowSelectionToolbar()
    {
        if (_editor.SelectionLength == 0 || !_editor.TextArea.IsKeyboardFocusWithin) { HideSelectionToolbar(); return; }
        if (_selectionBarPopup is null)
        {
            var toolbar = new MarkdownSelectionToolbar
            {
                DataContext = new MarkdownSelectionToolbarViewModel(action => _ = ExecuteAsync(action),
                    ApplySpanStyle, GetSpanStyle, () => _ = ExecuteAsync(MarkdownEditorAction.ClearSpanStyles), HideSelectionToolbar)
            };
            toolbar.AppendExtras(SelectionBarExtras);
            _selectionBarPopup = new Popup
            {
                PlacementTarget = this, Placement = PlacementMode.Top,
                IsLightDismissEnabled = true, OverlayDismissEventPassThrough = true, OverlayInputPassThroughElement = this, ShouldUseOverlayLayer = true,
                Child = toolbar
            };
            LogicalChildren.Add(_selectionBarPopup);
        }
        var position = _editor.TextArea.Caret.CalculateCaretRectangle();
        var point = _editor.TextArea.TextView.TranslatePoint(position.TopLeft, this) ?? default;
        _selectionBarPopup.PlacementRect = new Rect(point, new Size(1, Math.Max(1, position.Height))).Inflate(new Thickness(0, 8));
        _selectionBarPopup.IsOpen = true;
    }

    private void HideSelectionToolbar()
    {
        if (_selectionBarPopup is { IsOpen: true }) _selectionBarPopup.IsOpen = false;
    }
}
