using Avalonia;
using Avalonia.Input.Platform;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Workspace.ViewModels;

/// <summary>
/// 源码编辑器 ViewModel：只维护显示状态（字号、行号、自动配对、焦点模式）
/// 并把 Shell 消息转发给库编辑器视图，编辑能力本身由 CodeWF.Avalonia.Markdown.Editor 提供。
/// </summary>
public sealed class MarkdownEditorViewModel : ReactiveObject
{
    private readonly IEditorDisplayState _editorDisplayState;
    private readonly EditorPreferences _preferences;
    private readonly IWorkspaceDocumentState _workspaceDocumentState;
    private readonly Vex.Modules.Shell.ViewModels.ShellWorkbenchViewModel _workbench;
    private MarkdownEditorView? _editor;
    private bool _syncingFromDocument;
    private double _editorFontSize;
    private bool _showLineNumbers;
    private bool _enableAutoPair = true;
    private readonly Vex.Modules.Shell.Services.IShellOverlayService _overlays;
    private readonly IAppLocalizer _localizer;
    private bool _tableDialogOpen;
    private bool _imageDialogOpen;
    private readonly Vex.Modules.Shell.Services.IShellDocumentUtilityActions _documentUtilities;
    private readonly Vex.Modules.Shell.Services.IShellStatusPublisher _status;

    public MarkdownEditorViewModel(
        IEditorDisplayState editorDisplayState,
        EditorPreferences preferences,
        IWorkspaceDocumentState workspaceDocumentState,
        Vex.Modules.Shell.ViewModels.ShellWorkbenchViewModel workbench,
        Vex.Modules.Shell.Services.IShellOverlayService overlays, IAppLocalizer localizer,
        Vex.Modules.Shell.Services.IShellDocumentUtilityActions documentUtilities,
        Vex.Modules.Shell.Services.IShellStatusPublisher status)
    {
        _editorDisplayState = editorDisplayState;
        _preferences = preferences; preferences.PreferencesChanged += (_, _) => ApplyPreferences();
        _workspaceDocumentState = workspaceDocumentState;
        _workbench = workbench;
        _overlays = overlays;
        _localizer = localizer;
        _documentUtilities = documentUtilities;
        _status = status;
        workbench.PropertyChanged += (_, _) => { if (_editor is { } source) source.Editor.IsReadOnly = !workbench.IsSourceEditing; };
        workbench.SourceNavigationRequested += (_, node) =>
        {
            if (_editor is null) return;
            _editor.NavigateToLine(node.SourceLine);
            _editor.Editor.Select(node.SourceStart, Math.Max(0, node.SourceEnd - node.SourceStart));
        };
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

    /// <summary>原型源码视图是纯 textarea，无当前行底色带；对齐原型保持关闭。</summary>
    public bool HighlightCurrentLine => false;

    public void AttachEditor(MarkdownEditorView editor)
    {
        _editor = editor;
        editor.Editor.IsReadOnly = !_workbench.IsSourceEditing;
        editor.EnableAutoPair = EnableAutoPair;
        editor.ShowLineNumbers = ShowLineNumbers;
        editor.EditorFontSize = EditorFontSize;
        editor.HighlightCurrentLine = HighlightCurrentLine;
        ApplyPreferences();

        // 挂载即对齐当前文档：切换文件时视图是同一实例，只能靠这里补上新正文。
        SyncTextFromDocument();
        PublishTextChanged();
    }

    private void ApplyPreferences()
    {
        if (_editor is null) return;
        EditorFontSize = _editorDisplayState.EditorFontSize;
        _editor.Editor.WordWrap = _preferences.CodeWrap;
        _editor.Editor.Options.IndentationSize = _preferences.IndentSize;
        _editor.Editor.Options.ConvertTabsToSpaces = true;
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
        if (_syncingFromDocument || !_workbench.IsSourceEditing || _editor is not { } editor)
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
        if (_syncingFromDocument || !_workbench.IsSourceEditing || _editor is not { } editor)
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
        if (_workbench.IsInstantEditing && _workbench.LiveEditor is { } live) { _ = live.CopyPlainTextAsync(); return; }
        if (_editor is { } editor)
        {
            _ = editor.CopyPlainTextAsync();
        }
    }

    public void InsertText(string text)
    {
        if (_workbench.IsInstantEditing) _workbench.LiveEditor?.InsertText(text);
        else if (_workbench.IsSourceEditing) _editor?.InsertText(text);
    }

    /// <summary>外部改写（预览勾选任务回写）转发给库控件，保持编辑器与文档状态一致。</summary>
    public void ApplyExternalEdit(string markdown, int start, int length)
    {
        if (_workbench.IsSourceEditing) _editor?.ApplyExternalEdit(markdown, start, length);
    }

    public AvaloniaEdit.Document.TextDocument? BeginTemporaryDocument()
    {
        if (_editor is null) return null;
        _syncingFromDocument = true;
        try { var saved = _editor.Editor.Document; _editor.Editor.Document = new(); return saved; }
        finally { _syncingFromDocument = false; }
    }
    public void RestoreDocument(AvaloniaEdit.Document.TextDocument? document)
    {
        if (_editor is null || document is null) return;
        _syncingFromDocument = true;
        try { _editor.Editor.Document = document; }
        finally { _syncingFromDocument = false; }
    }

    /// <summary>执行库编辑器动作（Shell 菜单、快捷键与 MCP 都汇到这里）。</summary>
    public void Execute(EditorActionKind action)
    {
        if (action == EditorActionKind.Table) { _ = InsertTableAsync(); return; }
        if (action == EditorActionKind.Image) { _ = InsertImageAsync(); return; }
        if (action == EditorActionKind.Undo) { _workbench.UndoDocument(); return; }
        if (action == EditorActionKind.Redo) { _workbench.RedoDocument(); return; }
        if (_workbench.IsInstantEditing)
        {
            if (action == EditorActionKind.FocusEditor && _workbench.LiveEditor is { } focused)
            {
                if (focused.Text.Length == 0 || focused.ActiveBlockIndex >= 0) focused.FocusEditor();
                else focused.Focus();
            }
            else if (_workbench.LiveEditor is { } live) _ = ExecuteAndRefocusAsync(live, action.ToEditorAction());
            return;
        }
        if (_workbench.IsSourceEditing && _editor is { } editor)
        {
            _ = ExecuteAndRefocusAsync(editor, action.ToEditorAction());
        }
    }

    [EventHandler]
    public void InsertSnippet(EditorSnippetCommand command)
    {
        if (MarkdownTemplates.Get(command.Key) is { } snippet) InsertMarkdown(snippet);
    }

    private void InsertMarkdown(string insertion)
    {
        if (!_workbench.IsEditing) return;
        var selection = _workbench.IsInstantEditing ? _workbench.LiveEditor?.Selection : _editor?.Selection;
        if (selection is not { } range) return;
        var text = _workspaceDocumentState.Markdown;
        if (range.Start < 0 || range.Start + range.Length > text.Length) return;
        _workbench.History.BreakGroup();
        var changed = text.Remove(range.Start, range.Length).Insert(range.Start, insertion);
        EventBus.Default.Publish(new MarkdownTextChangedCommand(changed, 1, 1, changed.Count(c => c == '\n') + 1));
        _workbench.History.BreakGroup();
    }

    private async Task InsertImageAsync()
    {
        if (_imageDialogOpen || !_workbench.IsEditing) return;
        var live = _workbench.IsInstantEditing ? _workbench.LiveEditor : null;
        var source = _workbench.IsSourceEditing ? _editor : null;
        var control = (Avalonia.Controls.Control?)live ?? source;
        var owner = control is null ? null : Avalonia.Controls.TopLevel.GetTopLevel(control);
        if (owner is null) return;
        var text = _workspaceDocumentState.Markdown;
        var path = _workspaceDocumentState.FilePath;
        var selection = live?.Selection ?? source!.Selection;
        var snapshot = live?.CaptureState();
        _imageDialogOpen = true;
        try
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = _localizer.Get(VexL.Image), AllowMultiple = false,
                FileTypeFilter = [Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll]
            });
            if (_workspaceDocumentState.FilePath != path || _workspaceDocumentState.Markdown != text || !_workbench.IsEditing) return;
            if (files.FirstOrDefault() is { } file && Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(file) is { } image)
            {
                var resource = _documentUtilities.CopyImageToAssets(path, image);
                if (resource is null) return;
                var insertion = $"![]({resource})";
                _workbench.History.BreakGroup();
                var changed = text.Remove(selection.Start, selection.Length).Insert(selection.Start, insertion);
                EventBus.Default.Publish(new MarkdownTextChangedCommand(changed, 1, 1, changed.Count(c => c == '\n') + 1));
                _workbench.History.BreakGroup();
            }
            else if (live is not null && snapshot is not null) live.RestoreState(snapshot);
            else source?.Editor.Select(selection.Start, selection.Length);
            if (live is not null) live.FocusEditor(); else source?.FocusEditor();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _status.PublishResource(VexL.ErrorMessageImageDropFailed);
        }
        finally { _imageDialogOpen = false; }
    }

    private async Task InsertTableAsync()
    {
        if (_tableDialogOpen || !_workbench.IsEditing) return;
        var live = _workbench.IsInstantEditing ? _workbench.LiveEditor : null;
        var source = _workbench.IsSourceEditing ? _editor : null;
        if (live is null && source is null) return;
        var text = _workspaceDocumentState.Markdown;
        var path = _workspaceDocumentState.FilePath;
        var selection = live?.Selection ?? source!.Selection;
        var snapshot = live?.CaptureState();
        _tableDialogOpen = true;
        try
        {
            var size = await _overlays.ShowInsertTableAsync();
            // 弹窗期间若文档被外部更新，不能把旧插入点写入新文档。
            if (_workspaceDocumentState.FilePath != path || _workspaceDocumentState.Markdown != text) return;
            if (size is null)
            {
                if (live is not null && snapshot is not null) live.RestoreState(snapshot);
                else if (source is not null) { source.Editor.Select(selection.Start, selection.Length); source.FocusEditor(); }
                return;
            }
            var table = MarkdownTableInsertion.Create(size, column => _localizer.Format(VexL.TableColumnTitle, column));
            var insertion = MarkdownTableInsertion.AtSelection(text, selection.Start, selection.Length, table);
            _workbench.History.BreakGroup();
            var changed = text.Remove(selection.Start, selection.Length).Insert(selection.Start, insertion);
            EventBus.Default.Publish(new MarkdownTextChangedCommand(changed, 1, 1, changed.Count(c => c == '\n') + 1));
            _workbench.History.BreakGroup();
            var headerOffset = selection.Start + insertion.IndexOf('|') + 2;
            if (source is not null)
            {
                source.Editor.Select(headerOffset, _localizer.Format(VexL.TableColumnTitle, 1).Length);
                source.FocusEditor();
            }
            else live?.NavigateToSourceLine(1 + changed.AsSpan(0, headerOffset).Count('\n'));
        }
        finally { _tableDialogOpen = false; }
    }

    /// <summary>格式按钮执行后把焦点还给编辑器，保留当前块或源码选区。</summary>
    private static async Task ExecuteAndRefocusAsync(MarkdownLiveEditorView editor, MarkdownEditorAction action)
    {
        await editor.ExecuteAsync(action);
        editor.FocusEditor();
    }
    private static async Task ExecuteAndRefocusAsync(MarkdownEditorView editor, MarkdownEditorAction action)
    {
        await editor.ExecuteAsync(action);
        editor.FocusEditor();
    }

    // 右键菜单绑定：菜单点击后统一走消息总线，与工具栏/标题菜单保持同一条动作通道。
    public void Undo() => PublishEditorAction(EditorActionKind.Undo);

    public void Redo() => PublishEditorAction(EditorActionKind.Redo);

    public void Cut() => PublishEditorAction(EditorActionKind.Cut);

    public void Copy() => PublishEditorAction(EditorActionKind.Copy);

    public void Paste() => PublishEditorAction(EditorActionKind.Paste);

    public void SelectAll() => PublishEditorAction(EditorActionKind.SelectAll);

    public void InsertAction(EditorActionKind action) => PublishEditorAction(action);

    // —— 元素菜单：表格结构、段落转换与块操作（文档文本级，源码与即时共用一条通道）。 ——

    /// <summary>当前编辑文本与光标偏移；不在编辑态或拿不到选区时返回 null。</summary>
    private (string Text, int Caret)? EditingCaret(int? caret = null)
    {
        if (!_workbench.IsEditing) return null;
        var resolved = caret ?? (_workbench.IsInstantEditing ? _workbench.LiveEditor?.Selection : _editor?.Selection)?.Start;
        if (resolved is not { } position || position < 0) return null;
        var text = _workspaceDocumentState.Markdown;
        return position <= text.Length ? (text, position) : null;
    }

    private Avalonia.Controls.Control? EditingControl() => _workbench.IsInstantEditing
        ? (Avalonia.Controls.Control?)_workbench.LiveEditor : _editor;

    private void ReplaceSpan(string text, int start, int length, string insertion)
    {
        _workbench.History.BreakGroup();
        var changed = text.Remove(start, length).Insert(start, insertion);
        EventBus.Default.Publish(new MarkdownTextChangedCommand(changed, 1, 1, changed.Count(c => c == '\n') + 1));
        _workbench.History.BreakGroup();
    }

    /// <summary>光标（或命中块）处于表格内时才可用的表格结构操作。</summary>
    public bool HasTableAt(int? caret = null) => EditingCaret(caret) is { } editing
        && MarkdownElementOperations.FindTableAt(editing.Text, editing.Caret) is not null;

    public void TableAction(MarkdownTableAction action) => TableAction(action, null);

    public void TableAction(MarkdownTableAction action, int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        if (MarkdownElementOperations.FindTableAt(editing.Text, editing.Caret) is not { } hit) return;
        if (MarkdownElementOperations.ParseTable(hit.Raw) is not { } parsed) return;
        if (MarkdownElementOperations.ChangeTable(parsed, action, hit.Row, hit.Column) is not { } changed) return;
        ReplaceSpan(editing.Text, hit.Start, hit.Length, changed.Raw);
        // 源码模式把光标带回操作后的单元格，支持连续的行/列操作。
        if (_editor is { } editor)
        {
            editor.Editor.Select(hit.Start + MarkdownElementOperations.CellOffset(changed.Raw, changed.Row, changed.Column), 0);
        }
    }

    /// <summary>格式化光标所在表格：重排分隔行对齐标记并按内容宽度补齐各列。</summary>
    public void FormatTable() => FormatTable(null);

    public void FormatTable(int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        if (MarkdownElementOperations.FindTableAt(editing.Text, editing.Caret) is not { } hit) return;
        if (MarkdownElementOperations.ParseTable(hit.Raw) is not { } parsed) return;
        ReplaceSpan(editing.Text, hit.Start, hit.Length, MarkdownElementOperations.SerializeTable(parsed));
    }

    public void ConvertBlock(string kind, int? caret = null)
    {
        if (EditingCaret(caret) is not { } editing) return;
        var (start, length) = MarkdownElementOperations.FindBlockAt(editing.Text, editing.Caret);
        var converted = MarkdownElementOperations.ConvertBlock(editing.Text.Substring(start, length), kind);
        ReplaceSpan(editing.Text, start, length, converted);
        _editor?.Editor.Select(start + converted.Length, 0);
    }

    public void InsertParagraphAbove() => InsertParagraphAbove(null);

    public void InsertParagraphAbove(int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        var (start, _) = MarkdownElementOperations.FindBlockAt(editing.Text, editing.Caret);
        var newline = editing.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        ReplaceSpan(editing.Text, start, 0, newline + newline);
    }

    public void InsertParagraphBelow() => InsertParagraphBelow(null);

    public void InsertParagraphBelow(int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        var (start, length) = MarkdownElementOperations.FindBlockAt(editing.Text, editing.Caret);
        var newline = editing.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        ReplaceSpan(editing.Text, start + length, 0, newline + newline);
    }

    public void ToggleTask() => ToggleTask(null);

    /// <summary>切换光标所在列表项的任务状态；非任务列表项先升级为任务。</summary>
    public void ToggleTask(int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        var lineStart = editing.Text.LastIndexOf('\n', Math.Max(0, editing.Caret - 1)) + 1;
        var lineEnd = editing.Text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = editing.Text.Length;
        var line = editing.Text[lineStart..lineEnd];
        var changed = System.Text.RegularExpressions.Regex.Replace(line,
            @"^(\s*(?:[-+*]|\d+[.)])\s+)\[[ xX]\]\s*", match => match.Groups[1].Value + (match.Value.Contains("[ ]") ? "[x] " : "[ ] "));
        if (changed == line)
            changed = System.Text.RegularExpressions.Regex.Replace(line, @"^(\s*(?:[-+*]|\d+[.)])\s+)", "$1[ ] ");
        if (changed == line) return;
        ReplaceSpan(editing.Text, lineStart, line.Length, changed);
        _editor?.Editor.Select(lineStart + changed.Length, 0);
    }

    public void DeleteBlock() => DeleteBlock(null);

    /// <summary>删除光标所在块，并吞掉块后（或块前）的一个换行避免留下连续空行。</summary>
    public void DeleteBlock(int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        var (start, length) = MarkdownElementOperations.FindBlockAt(editing.Text, editing.Caret);
        var newline = editing.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (start + length < editing.Text.Length && editing.Text.AsSpan(start + length).StartsWith(newline))
            length += newline.Length;
        else if (start > 0 && editing.Text.AsSpan(start - newline.Length, newline.Length).SequenceEqual(newline))
        { start -= newline.Length; length += newline.Length; }
        ReplaceSpan(editing.Text, start, length, string.Empty);
    }

    public void CopyBlock() => CopyBlock(null);

    public async void CopyBlock(int? caret)
    {
        if (EditingCaret(caret) is not { } editing) return;
        var (start, length) = MarkdownElementOperations.FindBlockAt(editing.Text, editing.Caret);
        var control = EditingControl();
        var owner = control is null ? null : Avalonia.Controls.TopLevel.GetTopLevel(control);
        if (owner?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(editing.Text.Substring(start, length));
        _status.PublishResource(VexL.StatusBlockCopied);
    }

    /// <summary>粘贴剪贴板纯文本，丢弃来源富文本格式。</summary>
    public async void PasteAsPlainTextAsync()
    {
        if (EditingCaret() is not { } editing) return;
        var control = EditingControl();
        var owner = control is null ? null : Avalonia.Controls.TopLevel.GetTopLevel(control);
        if (owner?.Clipboard is not { } clipboard) return;
        var plain = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(plain)) return;
        var selection = _workbench.IsInstantEditing ? _workbench.LiveEditor?.Selection : _editor?.Selection;
        if (selection is not { } range) return;
        ReplaceSpan(editing.Text, range.Start, range.Length, plain);
    }

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
    public void ApplyTaskWriteBack(MarkdownTaskWriteBackCommand command) => ApplyExternalEdit(command.Markdown, command.Start, command.Length);

    /// <summary>即时渲染块的元素菜单：块行号换算成文档偏移后走统一的文本操作；SourceLine 非正时用编辑器当前光标。</summary>
    [EventHandler]
    public void ApplyElementCommand(MarkdownElementCommand command)
    {
        var text = _workspaceDocumentState.Markdown;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        int? caret = null;
        if (command.SourceLine > 0)
        {
            var offset = 0;
            for (var line = 1; line < command.SourceLine && offset <= text.Length; line++)
            {
                var next = text.IndexOf('\n', offset);
                if (next < 0) { offset = text.Length; break; }
                offset = next + 1;
            }
            if (offset > text.Length) return;
            caret = Math.Min(offset, text.Length);
        }
        switch (command.Operation)
        {
            case MarkdownElementOperation.Table when command.TableAction is { } action:
                TableAction(action, caret);
                break;
            case MarkdownElementOperation.FormatTable:
                FormatTable(caret);
                break;
            case MarkdownElementOperation.Convert when command.ConvertKind is { } kind:
                ConvertBlock(kind, caret);
                break;
            case MarkdownElementOperation.InsertParagraphAbove:
                InsertParagraphAbove(caret);
                break;
            case MarkdownElementOperation.InsertParagraphBelow:
                InsertParagraphBelow(caret);
                break;
            case MarkdownElementOperation.ToggleTask:
                ToggleTask(caret);
                break;
            case MarkdownElementOperation.CopyBlock:
                CopyBlock(caret);
                break;
            case MarkdownElementOperation.DeleteBlock:
                DeleteBlock(caret);
                break;
        }
    }

    [EventHandler]
    public void ApplySearch(EditorSearchCommand command)
    {
        // 查找始终露出命中位置，替换只在明确的编辑视图执行。
        if (command.Action != EditorSearchAction.Count) _workbench.ShowSource();
        _editor?.Search(command.ToEditorSearchCommand());
    }

    [EventHandler]
    public void GetSelectedText(EditorSelectedTextQuery query) => query.Result = _workbench.IsInstantEditing
        ? _workbench.LiveEditor?.SelectedText ?? string.Empty : _editor?.SelectedText ?? string.Empty;

    [EventHandler]
    public void GetSelection(EditorSelectionQuery query)
    {
        if (_workbench.IsInstantEditing && _workbench.LiveEditor is { } live)
        {
            var selection = live.Selection;
            query.Result = new EditorSelectionInfo(live.SelectedText, selection.Start, selection.Length);
            return;
        }
        if (_editor is not { } editor)
        {
            query.Result = new EditorSelectionInfo(string.Empty, 0, 0);
            return;
        }

        var (start, length) = editor.Selection;
        query.Result = new EditorSelectionInfo(editor.SelectedText, start, length);
    }

    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command) { if (_workbench.IsSourceVisible) _editor?.NavigateToLine(command.Line); }

    private void OnEditorDisplayChanged(object? sender, EventArgs e)
    {
        EditorFontSize = _editorDisplayState.EditorFontSize;
        ShowLineNumbers = _editorDisplayState.ShowLineNumbers;
        EnableAutoPair = _editorDisplayState.EnableAutoPair;
    }
}
