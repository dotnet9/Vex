using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Workspace.Views;

public sealed partial class MarkdownInstantView : UserControl
{
    public MarkdownLiveEditorView Editor => InstantEditor;
    public Vex.Modules.Workspace.ViewModels.MarkdownElementMenuViewModel MenuActions { get; } = new();
    private ShellWorkbenchViewModel? _workbench;
    private ShellWindowLayoutViewModel? _layout;

    public MarkdownInstantView()
    {
        InitializeComponent();
        // Typora 行为：点击文档空白（未命中任何块编辑器）→ 光标落到最后一个段落块末尾。
        // Tunnel 从窗口根向下必经此处；点在块内时交还给块自己的 OnPointerPressed。
        // 「落到末段」只覆盖点到正文末尾之下这一种 Typora 语义：正文列两侧留白、窗格上方
        // 余白的点击必须就地忽略——否则拖动阅读后点到任何编辑器子树外的空白都会把光标
        // 与视口拽到文档末段（表现即「点一下，视图上移」）。
        this.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Avalonia.Visual source
                && source.GetVisualAncestors().OfType<MarkdownLiveEditorView>().Any()) return;
            if (e.GetPosition(Editor).Y <= Editor.Bounds.Height) return;
            Editor.FocusLastParagraph();
        }, RoutingStrategies.Tunnel);
        // 巨文档后台解析指示：解析开始显示加载层（挡住旧内容闪变），完成/作废消失。
        Editor.AsyncParseStarted += (_, e) =>
        {
            var text = this.FindControl<TextBlock>("InstantLoadText");
            if (text is not null)
            {
                text.Text = string.Format(
                    Vex.Core.Models.WorkbenchText.Get("LoadingLargeDocument"),
                    e.Length / 1024);
            }
            var indicator = this.FindControl<Border>("InstantLoadIndicator");
            if (indicator is not null) indicator.IsVisible = true;
        };
        Editor.AsyncParseFinished += (_, _) =>
        {
            var indicator = this.FindControl<Border>("InstantLoadIndicator");
            if (indicator is not null) indicator.IsVisible = false;
        };
        AttachedToVisualTree += (_, _) => EventBus.Default.Subscribe(this);
        DetachedFromVisualTree += (_, _) => EventBus.Default.Unsubscribe(this);
        SizeChanged += (_, _) => UpdatePadding();
        Editor.SelectionChanged += (_, _) => CenterCaret();
        // 切换视图后正文块已重排，残留的右键菜单会指向错误段落（对应原型 element-menu 的切换即收起）。
        // 变为可见时空白文档补一次首块聚焦：切布局走 SynchronizeLive 同文本短路不调 SetText，
        // 库侧挂起聚焦不会武装；推到布局稳定（Dispatcher.Background）后执行。
        PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty) return;
            if (!IsVisible) { CloseElementMenu(); return; }
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible && this.IsAttachedToVisualTree()) Editor.FocusBlankIfBlank();
            }, DispatcherPriority.Background);
        };
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) =>
        {
            _workbench?.DetachLiveEditor(Editor); _workbench = null;
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = null;
        };
    }

    private void CloseElementMenu()
    {
        if (Editor.ContextMenu is { IsOpen: true } menu) menu.Close();
    }

    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_workbench?.IsInstantEditing == true) Editor.NavigateToSourceLine(command.Line);
    }

    [EventHandler]
    public void OnDocumentSwitched(MarkdownDocumentSwitchedCommand command)
    {
        CloseElementMenu();
        Dispatcher.UIThread.Post(() =>
        {
            var scroll = Editor?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scroll is not null) scroll.Offset = new Avalonia.Vector(scroll.Offset.X, 0);
        }, DispatcherPriority.Loaded);
    }
    private void Attach()
    {
        var layout = (DataContext as MainWindowViewModel)?.Layout;
        if (!ReferenceEquals(layout, _layout))
        {
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = layout;
            if (layout is not null) layout.PropertyChanged += LayoutChanged;
            UpdatePadding();
        }
        var workbench = (DataContext as MainWindowViewModel)?.Workbench;
        if (ReferenceEquals(workbench, _workbench)) return;
        _workbench?.DetachLiveEditor(Editor);
        _workbench = workbench;
        workbench?.AttachLiveEditor(Editor);
    }

    private void LayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellWindowLayoutViewModel.IsTypewriterMode)) return;
        UpdatePadding(); CenterCaret();
    }

    private void UpdatePadding()
    {
        var width = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? 1280;
        var combined = _workbench?.IsCombined == true;
        // 原型捕获的 app-window 比浏览器桌面视口少 48px（左右各 24px），clamp 以外层桌面宽度计算。
        var padding = combined ? Math.Clamp(Editor.Bounds.Width * .07, 14, 30) : width <= 850 ? 28 : Math.Clamp((width + 48) * .06, 28, 88);
        Editor.DocumentPadding = new Thickness(padding, combined ? 22 : width <= 850 ? 28 : 36,
            padding, _layout?.IsTypewriterMode == true ? Math.Max(80, Editor.Bounds.Height / 2) : combined ? 56 : width <= 850 ? 28 : 80);
    }

    private void CenterCaret()
    {
        if (_layout?.IsTypewriterMode != true || !IsEffectivelyVisible) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_layout?.IsTypewriterMode != true || !IsEffectivelyVisible) return;
            var box = Editor.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.IsEffectivelyVisible);
            var presenter = box?.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
            var scroll = Editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (presenter is null || scroll is null) return;
            var caret = presenter.TextLayout.HitTestTextPosition(box!.CaretIndex);
            if (presenter.TranslatePoint(caret.Center, scroll) is not { } point) return;
            scroll.Offset = new Vector(
                scroll.Offset.X,
                TypewriterScroll.GetTargetOffset(scroll.Offset.Y + point.Y, scroll.Viewport.Height));
        }, DispatcherPriority.Background);
    }
}
