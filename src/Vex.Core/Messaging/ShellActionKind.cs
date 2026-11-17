namespace Vex.Core.Messaging;

public enum ShellActionKind
{
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.NewDocument, Gesture = "Ctrl+N")]
    NewDocument,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.NewWindow, Gesture = "Ctrl+Shift+N")]
    NewWindow,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.OpenFile, Gesture = "Ctrl+O")]
    Open,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.OpenFolder)]
    OpenFolder,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.QuickOpen, Gesture = "Ctrl+P")]
    QuickOpen,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.Import)]
    Import,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.OpenRecent)]
    OpenRecentDocument,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.ReopenWithEncoding)]
    ReopenWithEncoding,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.Save, Gesture = "Ctrl+S")]
    Save,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.SaveAs, Gesture = "Ctrl+Shift+S")]
    SaveAs,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.SaveAll)]
    SaveAll,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.OpenFileLocation)]
    OpenFileLocation,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.Delete)]
    Delete,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.Export)]
    Export,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.CopyHtml)]
    CopyHtml,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.Print, Gesture = "Ctrl+Shift+P")]
    Print,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.CloseDocument, Gesture = "Ctrl+W")]
    CloseDocument,
    [VexCommand(VexCommandKeys.GroupView, VexCommandKeys.RefreshPreview)]
    RefreshPreview,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Find, Gesture = "Ctrl+F")]
    ShowFindPanel,
    [VexCommand(VexCommandKeys.GroupEdit, VexCommandKeys.Replace, Gesture = "Ctrl+H")]
    ShowReplacePanel,
    [VexCommand(VexCommandKeys.GroupSettings, VexCommandKeys.NavSettings)]
    ShowPreferences,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.TemplateCenter)]
    ShowTemplateCenter,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.TemplateSave)]
    ShowTemplateSave,
    [VexCommand(VexCommandKeys.GroupSettings, VexCommandKeys.McpSettingsTitle)]
    ShowMcpSettings,
    [VexCommand(VexCommandKeys.GroupSettings, VexCommandKeys.McpAudit)]
    ShowMcpAudit,
    [VexCommand(VexCommandKeys.GroupView, VexCommandKeys.RefreshFiles)]
    RefreshFiles,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.OpenPathLocation)]
    OpenPathLocation,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.AddFolderToWorkspace)]
    AddFolderToWorkspace,
    [VexCommand(VexCommandKeys.GroupFile, VexCommandKeys.RemoveWorkspaceFolder)]
    RemoveWorkspaceFolder
}
