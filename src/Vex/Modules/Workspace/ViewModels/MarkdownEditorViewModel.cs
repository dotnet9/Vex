using CodeWF.Markdown.Editor.Controls;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Workspace.ViewModels;

/// <summary>
/// 源码编辑器 ViewModel：只维护显示状态（字号、行号、自动配对、焦点模式）
/// 并把 Shell 消息转发给库编辑器视图，编辑能力本身由 CodeWF.Markdown.Editor 提供。
/// </summary>
public sealed class MarkdownEditorViewModel : ReactiveObject
{
    private readonly IEditorDisplayState _editorDisplayState;
    private readonly IWorkspaceDocumentState _workspaceDocumentState;
    private MarkdownEditorView? _editor;
    private bool _syncingFromDocument;
    private double _editorFontSize;
    private bool _showLineNumbers;
    private bool _enableAutoPair = true;

    public MarkdownEditorViewModel(
        IEditorDisplayState editorDisplayState,
        IWorkspaceDocumentState workspaceDocumentState)
    {
        _editorDisplayState = editorDisplayState;
        _workspaceDocumentState = workspaceDocumentState;
        _editorFontSize = editorDisplayState.EditorFontSize;
        _showLineNumbers = editorDisplayState.ShowLineNumbers;
        _enableAutoPair = editorDisplayState.EnableAutoPair;
        _editorDisplayState.Changed += OnEditorDisplayChanged;
        // 文档载入/保存走本地事件：库控件必须先拿到新文档正文，否则首次焦点同步会把
        // 上一篇的文本回抛给 Shell，被误判为「已修改」并弹出保存确认。
        _workspaceDocumentState.DocumentChanged += OnDocumentChanged;
        EventBus.Default.Subscribe(this);
    }

    /// <summary>自动配对开关（Shell 显示偏好下发）。</summary>
    public bool EnableAutoPair
    {
        get => _enableAutoPair;
        private set
        {
            this.RaiseAndSetIfChanged(ref _enableAutoPair, value);
            if (_editor is { } editor)
            {
                editor.EnableAutoPair = value;
            }
        }
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        private set
        {
            this.RaiseAndSetIfChanged(ref _editorFontSize, value);
            if (_editor is { } editor)
            {
                editor.EditorFontSize = value;
            }
        }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        private set
        {
            this.RaiseAndSetIfChanged(ref _showLineNumbers, value);
            if (_editor is { } editor)
            {
                editor.ShowLineNumbers = value;
            }
        }
    }

    /// <summary>焦点模式（Focus Mode）：高亮当前行。</summary>
    public bool HighlightCurrentLine => true;

    public void AttachEditor(MarkdownEditorView editor)
    {
        _editor = editor;
        editor.EnableAutoPair = EnableAutoPair;
        editor.ShowLineNumbers = ShowLineNumbers;
        editor.EditorFontSize = EditorFontSize;
        editor.HighlightCurrentLine = HighlightCurrentLine;

        // 挂载即对齐当前文档：切换文件时视图是同一实例，只能靠这里补上新正文。
        SyncTextFromDocument();
        PublishTextChanged();
    }

    public void DetachEditor(MarkdownEditorView editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            _editor = null;
        }
    }

    /// <summary>库控件正文变化（用户输入/动作）→ 回抛给 Shell；文档载入期间的同步不回抛。</summary>
    public void PublishTextChanged()
    {
        if (_syncingFromDocument || _editor is not { } editor)
        {
            return;
        }

        var caret = editor.CaretPosition;
        EventBus.Default.Publish(new MarkdownTextChangedCommand(
            editor.Text,
            caret.Line,
            caret.Column,
            caret.LineCount));
    }

    /// <summary>插入点变化（光标移动）也走同一条消息通道，状态栏行列号不依赖文本变化。</summary>
    public void PublishSelectionChanged(int line, int column, int lineCount)
    {
        // SetText 也会移动插入点；载入中不能把中间文本回抛成用户编辑。
        if (_syncingFromDocument || _editor is not { } editor)
        {
            return;
        }

        EventBus.Default.Publish(new MarkdownTextChangedCommand(
            editor.Text,
            line,
            column,
            lineCount));
    }

    public void CopyPlainText()
    {
        if (_editor is { } editor)
        {
            _ = editor.CopyPlainTextAsync();
        }
    }

    public void InsertText(string text) => _editor?.InsertText(text);

    /// <summary>外部改写（预览勾选任务回写）转发给库控件，保持编辑器与文档状态一致。</summary>
    public void ApplyExternalEdit(string markdown, int start, int length) =>
        _editor?.ApplyExternalEdit(markdown, start, length);

    /// <summary>执行库编辑器动作（Shell 菜单、快捷键与 MCP 都汇到这里）。</summary>
    public void Execute(EditorActionKind action)
    {
        if (_editor is { } editor)
        {
            _ = editor.ExecuteAsync(action.ToEditorAction());
        }
    }

    // 右键菜单绑定：菜单点击后统一走消息总线，与工具栏/标题菜单保持同一条动作通道。
    public void Undo() => PublishEditorAction(EditorActionKind.Undo);

    public void Redo() => PublishEditorAction(EditorActionKind.Redo);

    public void Cut() => PublishEditorAction(EditorActionKind.Cut);

    public void Copy() => PublishEditorAction(EditorActionKind.Copy);

    public void Paste() => PublishEditorAction(EditorActionKind.Paste);

    public void SelectAll() => PublishEditorAction(EditorActionKind.SelectAll);

    public void InsertAction(EditorActionKind action) => PublishEditorAction(action);

    private void OnDocumentChanged(object? sender, MarkdownDocumentChangedCommand command) => SyncTextFromDocument();

    /// <summary>把当前文档正文写入编辑控件；期间抑制回抛，避免把「载入」当成用户编辑。</summary>
    private void SyncTextFromDocument()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var markdown = _workspaceDocumentState.Markdown ?? string.Empty;
        if (editor.Text == markdown)
        {
            return;
        }

        _syncingFromDocument = true;
        try
        {
            editor.SetText(markdown);
        }
        finally
        {
            _syncingFromDocument = false;
        }
    }

    private static void PublishEditorAction(EditorActionKind action) =>
        EventBus.Default.Publish(new EditorActionCommand(action));

    [EventHandler]
    public void ApplyEditorAction(EditorActionCommand command) => Execute(command.Action);

    [EventHandler]
    public void ApplyEditorInsertText(MarkdownEditorInsertTextCommand command) => InsertText(command.Text);

    [EventHandler]
    public void ApplySearch(EditorSearchCommand command) => _editor?.Search(command.ToEditorSearchCommand());

    [EventHandler]
    public void GetSelectedText(EditorSelectedTextQuery query) => query.Result = _editor?.SelectedText ?? string.Empty;

    [EventHandler]
    public void GetSelection(EditorSelectionQuery query)
    {
        if (_editor is not { } editor)
        {
            query.Result = new EditorSelectionInfo(string.Empty, 0, 0);
            return;
        }

        var (start, length) = editor.Selection;
        query.Result = new EditorSelectionInfo(editor.SelectedText, start, length);
    }

    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command) => _editor?.NavigateToLine(command.Line);

    private void OnEditorDisplayChanged(object? sender, EventArgs e)
    {
        EditorFontSize = _editorDisplayState.EditorFontSize;
        ShowLineNumbers = _editorDisplayState.ShowLineNumbers;
        EnableAutoPair = _editorDisplayState.EnableAutoPair;
    }
}
