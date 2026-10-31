using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellDocumentToolsViewModel(
    ShellDocumentInfoViewModel documentInfo,
    ShellWindowLayoutViewModel layout,
    ShellWorkbenchViewModel workbench)
{
    public ShellDocumentInfoViewModel DocumentInfo { get; } = documentInfo;
    public ShellWindowLayoutViewModel Layout { get; } = layout;
    public ShellWorkbenchViewModel Workbench { get; } = workbench;
    public EditorPreferences Preferences => Workbench.Preferences;
}
