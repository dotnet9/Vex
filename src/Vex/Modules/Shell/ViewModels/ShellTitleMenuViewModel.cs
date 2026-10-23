using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellTitleMenuViewModel : ReactiveObject
{
    private readonly Vex.Modules.Shell.Services.IShellStatusPublisher _status;

    public ShellTitleMenuViewModel(
        ShellAppearanceViewModel appearance,
        ShellDocumentInfoViewModel documentInfo,
        ShellEditorActionsViewModel editorActions,
        ShellEditorDisplayViewModel editorDisplay,
        ShellHelpViewModel help,
        ShellWindowLayoutViewModel layout,
        ShellWorkbenchViewModel workbench,
        ShellRecentDocumentsViewModel recent,
        Vex.Core.Services.EditorPreferences preferences,
        Vex.Modules.Shell.Services.IShellStatusPublisher status)
    {
        Appearance = appearance;
        DocumentInfo = documentInfo;
        EditorActions = editorActions;
        EditorDisplay = editorDisplay;
        Help = help;
        Layout = layout;
        Workbench = workbench;
        Recent = recent;
        Preferences = preferences;
        _status = status;
    }

    public ShellAppearanceViewModel Appearance { get; }

    public ShellDocumentInfoViewModel DocumentInfo { get; }

    public ShellEditorActionsViewModel EditorActions { get; }

    public ShellEditorDisplayViewModel EditorDisplay { get; }

    public ShellHelpViewModel Help { get; }

    public ShellWindowLayoutViewModel Layout { get; }
    public ShellWorkbenchViewModel Workbench { get; }

    public ShellRecentDocumentsViewModel Recent { get; }
    public Vex.Core.Services.EditorPreferences Preferences { get; }
    public void ToggleWordCount() => Preferences.WordCountVisible = !Preferences.WordCountVisible;
    public void ToggleWatermark() => Preferences.WatermarkPreview = !Preferences.WatermarkPreview;

    public void NewDocument() => Publish(ShellActionKind.NewDocument);

    public void NewWindow() => Publish(ShellActionKind.NewWindow);

    public void OpenAsync() => Publish(ShellActionKind.Open);

    public void OpenFolderAsync() => Publish(ShellActionKind.OpenFolder);

    public void QuickOpenAsync() => Publish(ShellActionKind.QuickOpen);

    public void OpenRecentDocument1Async() => Publish(ShellActionKind.OpenRecentDocument, "0");

    public void OpenRecentDocument2Async() => Publish(ShellActionKind.OpenRecentDocument, "1");

    public void OpenRecentDocument3Async() => Publish(ShellActionKind.OpenRecentDocument, "2");

    public void OpenRecentDocument4Async() => Publish(ShellActionKind.OpenRecentDocument, "3");

    public void OpenRecentDocument5Async() => Publish(ShellActionKind.OpenRecentDocument, "4");

    public void ReopenWithEncodingAsync(string? encodingName) => Publish(ShellActionKind.ReopenWithEncoding, encodingName);

    public void SaveAsync() => Publish(ShellActionKind.Save);

    public void SaveAsAsync() => Publish(ShellActionKind.SaveAs);

    public void SaveAllAsync() => Publish(ShellActionKind.SaveAll);

    public void ShowProperties() => Publish(ShellActionKind.ShowProperties);


    public void OpenFileLocationAsync() => Publish(ShellActionKind.OpenFileLocation);

    public void DeleteAsync() => Publish(ShellActionKind.Delete);

    public void Export(string? format) => Publish(ShellActionKind.Export, format);

    /// <summary>导入入口（HTML/PDF/Word）：原型当前为演示位，先给状态反馈。</summary>
    public void ImportFrom(string? format) =>
        _status.PublishResourceFormat(VexL.StatusImportUnavailable, format ?? string.Empty);

    // 段落菜单的表格结构操作：走元素命令通道，SourceLine=0 表示作用于编辑器当前光标。
    public void ElementTable(MarkdownTableAction action) =>
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new MarkdownElementCommand(MarkdownElementOperation.Table, 0, tableAction: action));

    public void ElementFormatTable() =>
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new MarkdownElementCommand(MarkdownElementOperation.FormatTable, 0));

    public void ElementCopyBlock() =>
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new MarkdownElementCommand(MarkdownElementOperation.CopyBlock, 0));

    public void ElementDeleteBlock() =>
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new MarkdownElementCommand(MarkdownElementOperation.DeleteBlock, 0));

    public void CopyHtml(string? target) => Publish(ShellActionKind.CopyHtml, target);

    public void Print() => Publish(ShellActionKind.Print);

    public void CloseDocument() => Publish(ShellActionKind.CloseDocument);

    public void RefreshPreview() => Publish(ShellActionKind.RefreshPreview);

    public void ShowFindPanel() => Publish(ShellActionKind.ShowFindPanel);

    public void ShowReplacePanel() => Publish(ShellActionKind.ShowReplacePanel);

    public void WordCount() => Publish(ShellActionKind.WordCount);

    public void ShowPreferences() => Publish(ShellActionKind.ShowPreferences);

    public void ShowAppearancePreferences() => Publish(ShellActionKind.ShowPreferences, "appearance");

    public void ShowMcpSettings() => Publish(ShellActionKind.ShowMcpSettings);

    public void ShowMcpAudit() => Publish(ShellActionKind.ShowMcpAudit);

    private void Publish(ShellActionKind action, string? parameter = null)
    {
        // 标题栏菜单只表达用户意图，文档保存、未保存确认和文件 I/O 仍由 Shell 协调层统一处理。
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(action, parameter));
    }
}
