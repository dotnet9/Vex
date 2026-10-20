using Irihi.Avalonia.Shared.Contracts;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellPreferencesViewModel(EditorPreferences preferences, ShellAppearanceViewModel appearance,
    ShellEditorDisplayViewModel display, ShellWindowLayoutViewModel layout, McpSettingsViewModel mcp,
    Vex.Modules.Mcp.Services.IMcpOperationAuditService audit) : IDialogContext
{
    public EditorPreferences Preferences { get; } = preferences;
    public ShellAppearanceViewModel Appearance { get; } = appearance;
    public ShellEditorDisplayViewModel Display { get; } = display;
    public ShellWindowLayoutViewModel Layout { get; } = layout;
    public McpSettingsViewModel Mcp { get; } = mcp;
    public Vex.Modules.Mcp.Services.IMcpOperationAuditService Audit { get; } = audit;
    public event EventHandler<object?>? RequestClose;
    public void Close() => RequestClose?.Invoke(this, null);
}
