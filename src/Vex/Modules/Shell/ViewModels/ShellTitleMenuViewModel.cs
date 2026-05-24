using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellTitleMenuViewModel : ReactiveObject
{
    public ShellTitleMenuViewModel(
        ShellAppearanceViewModel appearance,
        ShellDocumentInfoViewModel documentInfo,
        ShellEditorActionsViewModel editorActions,
        ShellEditorDisplayViewModel editorDisplay,
        ShellHelpViewModel help,
        ShellWindowLayoutViewModel layout,
        ShellRecentDocumentsViewModel recent)
    {
        Appearance = appearance;
        DocumentInfo = documentInfo;
        EditorActions = editorActions;
        EditorDisplay = editorDisplay;
        Help = help;
        Layout = layout;
        Recent = recent;
    }

    public ShellAppearanceViewModel Appearance { get; }

    public ShellDocumentInfoViewModel DocumentInfo { get; }

    public ShellEditorActionsViewModel EditorActions { get; }

    public ShellEditorDisplayViewModel EditorDisplay { get; }

    public ShellHelpViewModel Help { get; }

    public ShellWindowLayoutViewModel Layout { get; }

    public ShellRecentDocumentsViewModel Recent { get; }

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

    public void CopyHtml(string? target) => Publish(ShellActionKind.CopyHtml, target);

    public void Print() => Publish(ShellActionKind.Print);

    public void CloseDocument() => Publish(ShellActionKind.CloseDocument);

    public void ShowFindPanel() => Publish(ShellActionKind.ShowFindPanel);

    public void ShowReplacePanel() => Publish(ShellActionKind.ShowReplacePanel);

    public void WordCount() => Publish(ShellActionKind.WordCount);

    private void Publish(ShellActionKind action, string? parameter = null)
    {
        // 标题栏菜单只表达用户意图，文档保存、未保存确认和文件 I/O 仍由 Shell 协调层统一处理。
        CodeWF.EventBus.EventBus.Default.Publish(new ShellActionCommand(action, parameter));
    }
}
