using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Data;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;
using Vex.Controls;

namespace Vex.Modules.Workspace.Views;

public sealed class MarkdownInstantView : UserControl
{
    public MarkdownLiveEditorView Editor { get; } = new()
    {
        HeaderText = null, ChipText = null, EditorFontSize = 15, CodeFontFamily = Vex.Core.Services.VexFonts.Code, DocumentMaxWidth = 1280, DocumentBlockSpacing = 0, DocumentPadding = new Thickness(76, 36, 76, 80),
        EditorBackgroundKey = "VexPreviewBackgroundBrush", EditorForegroundKey = "VexEditorForegroundBrush"
    };
    private ShellWorkbenchViewModel? _workbench;
    private ShellWindowLayoutViewModel? _layout;
    private int? _elementMenuSourceLine;
    private static readonly IAppLocalizer Localizer = new AppLocalizer();

    public MarkdownInstantView()
    {
        var grid = new Grid();
        grid.Bind(BackgroundProperty, this.GetResourceObservable("VexPreviewBackgroundBrush"));
        grid.Children.Add(Editor);
        var watermark = new WatermarkOverlay();
        watermark.Bind(IsVisibleProperty, new Binding("Workbench.Preferences.WatermarkPreview"));
        watermark.Bind(WatermarkOverlay.TextProperty, new Binding("Workbench.Preferences.WatermarkText"));
        watermark.Bind(WatermarkOverlay.StrengthProperty, new Binding("Workbench.Preferences.WatermarkOpacity"));
        watermark.Bind(WatermarkOverlay.FontSizeProperty, new Binding("Workbench.Preferences.WatermarkFontSize"));
        watermark.Bind(WatermarkOverlay.HexInkProperty, new Binding("Workbench.Preferences.WatermarkColor"));
        watermark.Bind(WatermarkOverlay.WeightValueProperty, new Binding("Workbench.Preferences.WatermarkWeight"));
        grid.Children.Add(watermark); Content = grid;
        AttachedToVisualTree += (_, _) => EventBus.Default.Subscribe(this);
        DetachedFromVisualTree += (_, _) => EventBus.Default.Unsubscribe(this);
        SizeChanged += (_, _) => UpdatePadding();
        Editor.SelectionChanged += (_, _) => CenterCaret();
        // 右键先记录命中块，元素菜单据此把操作对准被点击的块（与原型 element-menu 同一语义）。
        Editor.PointerPressed += OnEditorPointerPressed;
        Editor.ContextMenu = BuildElementMenu();
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) =>
        {
            _workbench?.DetachLiveEditor(Editor); _workbench = null;
            if (_layout is not null) _layout.PropertyChanged -= LayoutChanged;
            _layout = null;
        };
    }

    private void OnEditorPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Editor).Properties.IsRightButtonPressed) return;
        _elementMenuSourceLine = HitTestSourceLine(e.GetPosition(Editor));
    }

    /// <summary>把右键位置映射到命中的渲染块，取其起始源码行号。</summary>
    private int? HitTestSourceLine(Avalonia.Point position)
    {
        var host = Editor.GetVisualDescendants().OfType<CodeWF.Avalonia.Controls.Controls.WindowedStackPanel>().FirstOrDefault();
        if (host is null) return null;
        foreach (var child in host.Children)
        {
            if (child.TranslatePoint(default, Editor) is not { } origin) continue;
            if (position.Y < origin.Y || position.Y > origin.Y + child.Bounds.Height) continue;
            // MarkdownBlockView 对 Vex 是 internal 类型，但 Model 属性是公共块模型。
            if (child.GetType().GetProperty("Model")?.GetValue(child) is MarkdownBlockModel model)
                return Math.Max(1, model.SourceLine);
            return null;
        }
        return null;
    }

    private ContextMenu BuildElementMenu()
    {
        var menu = new ContextMenu();
        var items = new List<object>
        {
            SubMenu(Localizer.Get(VexL.ElementMenuConvertTo), "replace", ConvertItems()),
            SubMenu(Localizer.Get(VexL.Table), "table", TableItems()),
            Separator(),
            ElementItem(Localizer.Get(VexL.InsertParagraphAbove), new MarkdownElementCommand(MarkdownElementOperation.InsertParagraphAbove, SourceLineOrCaret())),
            ElementItem(Localizer.Get(VexL.InsertParagraphBelow), new MarkdownElementCommand(MarkdownElementOperation.InsertParagraphBelow, SourceLineOrCaret())),
            ElementItem(Localizer.Get(VexL.ToggleTask), new MarkdownElementCommand(MarkdownElementOperation.ToggleTask, SourceLineOrCaret())),
            ElementItem(Localizer.Get(VexL.CopyBlock), new MarkdownElementCommand(MarkdownElementOperation.CopyBlock, SourceLineOrCaret())),
            ElementItem(Localizer.Get(VexL.DeleteBlock), new MarkdownElementCommand(MarkdownElementOperation.DeleteBlock, SourceLineOrCaret())),
        };
        menu.ItemsSource = items;
        foreach (var item in items.OfType<MenuItem>()) MenuIconCatalog.Apply(item);
        return menu;
    }

    private static MenuItem SubMenu(string header, string iconKey, List<object> items)
    {
        var item = new MenuItem { Header = header, ItemsSource = items };
        MenuIconCatalog.SetKey(item, iconKey);
        return item;
    }

    private static Separator Separator() => new();

    private MenuItem ElementItem(string header, MarkdownElementCommand command)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = new DelegateCommand(() => EventBus.Default.Publish(command)),
        };
        // 委托闭包没有公开动作参数，显式传递图标语义以保证不同语言下使用相同图形。
        var key = command.Operation switch
        {
            MarkdownElementOperation.Convert => command.ConvertKind switch
            {
                "paragraph" => "pilcrow", "quote" => "quote", "list" => "list",
                "ordered" => "list-ordered", "task" => "list-check", _ => "heading",
            },
            MarkdownElementOperation.Table => command.TableAction switch
            {
                MarkdownTableAction.RowAbove => "arrow-up-to-line", MarkdownTableAction.RowBelow => "arrow-down-to-line",
                MarkdownTableAction.ColBefore or MarkdownTableAction.ColLeft => "arrow-left",
                MarkdownTableAction.ColAfter or MarkdownTableAction.ColRight => "arrow-right",
                MarkdownTableAction.RowUp => "arrow-up", MarkdownTableAction.RowDown => "arrow-down",
                MarkdownTableAction.AlignLeft => "align-left", MarkdownTableAction.AlignCenter => "align-center",
                MarkdownTableAction.AlignRight => "align-right", MarkdownTableAction.RowDelete => "table-rows-split",
                MarkdownTableAction.ColDelete => "table-columns-split", _ => "table",
            },
            MarkdownElementOperation.FormatTable => "table",
            MarkdownElementOperation.InsertParagraphAbove => "arrow-up-to-line",
            MarkdownElementOperation.InsertParagraphBelow => "arrow-down-to-line",
            MarkdownElementOperation.ToggleTask => "list-check",
            MarkdownElementOperation.CopyBlock => "copy",
            MarkdownElementOperation.DeleteBlock => "trash",
            _ => "help",
        };
        MenuIconCatalog.SetKey(item, key);
        return item;
    }

    private MenuItem EditorActionItem(string header, EditorActionKind action) => new()
    {
        Header = header,
        CommandParameter = action,
        Command = new DelegateCommand(() => EventBus.Default.Publish(new EditorActionCommand(action))),
    };

    private List<object> ConvertItems() => new()
    {
        ElementItem(Localizer.Get(VexL.Paragraph), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "paragraph")),
        ElementItem(Localizer.Get(VexL.Heading1), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "heading1")),
        ElementItem(Localizer.Get(VexL.Heading2), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "heading2")),
        ElementItem(Localizer.Get(VexL.Heading3), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "heading3")),
        ElementItem(Localizer.Get(VexL.Heading4), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "heading4")),
        ElementItem(Localizer.Get(VexL.Heading5), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "heading5")),
        ElementItem(Localizer.Get(VexL.Heading6), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "heading6")),
        ElementItem(Localizer.Get(VexL.Quote), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "quote")),
        ElementItem(Localizer.Get(VexL.UnorderedList), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "list")),
        ElementItem(Localizer.Get(VexL.OrderedList), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "ordered")),
        ElementItem(Localizer.Get(VexL.TaskList), new MarkdownElementCommand(MarkdownElementOperation.Convert, SourceLineOrCaret(), convertKind: "task")),
    };

    private List<object> TableItems()
    {
        var line = SourceLineOrCaret();
        MarkdownElementCommand Table(MarkdownTableAction action) =>
            new(MarkdownElementOperation.Table, line, tableAction: action);
        return new List<object>
        {
            EditorActionItem(Localizer.Get(VexL.InsertTable), EditorActionKind.Table),
            Separator(),
            ElementItem(Localizer.Get(VexL.TableRowAbove), Table(MarkdownTableAction.RowAbove)),
            ElementItem(Localizer.Get(VexL.TableRowBelow), Table(MarkdownTableAction.RowBelow)),
            ElementItem(Localizer.Get(VexL.TableColumnBefore), Table(MarkdownTableAction.ColBefore)),
            ElementItem(Localizer.Get(VexL.TableColumnAfter), Table(MarkdownTableAction.ColAfter)),
            Separator(),
            ElementItem(Localizer.Get(VexL.TableRowUp), Table(MarkdownTableAction.RowUp)),
            ElementItem(Localizer.Get(VexL.TableRowDown), Table(MarkdownTableAction.RowDown)),
            ElementItem(Localizer.Get(VexL.TableColumnLeft), Table(MarkdownTableAction.ColLeft)),
            ElementItem(Localizer.Get(VexL.TableColumnRight), Table(MarkdownTableAction.ColRight)),
            Separator(),
            ElementItem(Localizer.Get(VexL.TableColumnAlignLeft), Table(MarkdownTableAction.AlignLeft)),
            ElementItem(Localizer.Get(VexL.TableColumnAlignCenter), Table(MarkdownTableAction.AlignCenter)),
            ElementItem(Localizer.Get(VexL.TableColumnAlignRight), Table(MarkdownTableAction.AlignRight)),
            Separator(),
            ElementItem(Localizer.Get(VexL.TableRowDelete), Table(MarkdownTableAction.RowDelete)),
            ElementItem(Localizer.Get(VexL.TableColumnDelete), Table(MarkdownTableAction.ColDelete)),
            Separator(),
            ElementItem(Localizer.Get(VexL.TableFormat), new MarkdownElementCommand(MarkdownElementOperation.FormatTable, line)),
            ElementItem(Localizer.Get(VexL.TableDelete), new MarkdownElementCommand(MarkdownElementOperation.DeleteBlock, line)),
        };
    }

    private int SourceLineOrCaret() => _elementMenuSourceLine ?? 1;

    private sealed class DelegateCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_workbench?.IsInstantEditing == true) Editor.NavigateToSourceLine(command.Line);
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
        var padding = combined ? 36 : width <= 850 ? 28 : Math.Clamp(width * .06, 28, 88);
        Editor.DocumentPadding = new Thickness(padding, combined ? 26 : width <= 850 ? 28 : 30,
            padding, _layout?.IsTypewriterMode == true ? Math.Max(80, Editor.Bounds.Height / 2) : 80);
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
            scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y + point.Y - scroll.Viewport.Height / 2));
        }, DispatcherPriority.Background);
    }
}
