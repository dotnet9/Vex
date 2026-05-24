using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellStatusBarViewModel : ReactiveObject
{
    public ShellStatusBarViewModel(
        ShellStatusViewModel status,
        ShellDocumentInfoViewModel documentInfo,
        ShellWindowLayoutViewModel layout)
    {
        Status = status;
        DocumentInfo = documentInfo;
        Layout = layout;
    }

    public ShellStatusViewModel Status { get; }

    public ShellDocumentInfoViewModel DocumentInfo { get; }

    public ShellWindowLayoutViewModel Layout { get; }
}
