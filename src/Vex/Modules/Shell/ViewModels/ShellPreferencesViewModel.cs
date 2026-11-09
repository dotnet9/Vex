using Irihi.Avalonia.Shared.Contracts;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellPreferencesViewModel(EditorPreferences preferences, ShellAppearanceViewModel appearance,
    ShellEditorDisplayViewModel display, ShellWindowLayoutViewModel layout, McpSettingsViewModel mcp,
    Vex.Modules.Mcp.Services.IMcpOperationAuditService audit, ShellOutlineViewModel outline) : IDialogContext
{
    public EditorPreferences Preferences { get; } = preferences;
    public ShellAppearanceViewModel Appearance { get; } = appearance;
    public ShellEditorDisplayViewModel Display { get; } = display;
    public ShellWindowLayoutViewModel Layout { get; } = layout;
    public McpSettingsViewModel Mcp { get; } = mcp;
    public Vex.Modules.Mcp.Services.IMcpOperationAuditService Audit { get; } = audit;

    /// <summary>大纲面板模型：偏好里「大纲显示章节序号」开关直接落在它上面，与侧栏共用同一实例即时生效。</summary>
    public ShellOutlineViewModel Outline { get; } = outline;
    public event EventHandler<object?>? RequestClose;
    public void Close() => RequestClose?.Invoke(this, null);
}
