using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

/// <summary>
/// 窗内浮层服务：把原先各自为政的独立窗口统一收敛为宿主窗口内的 Ursa 浮层，
/// 调用方只描述「要显示什么」，不再持有 Window、不再自行拼 Show(owner)。
/// </summary>
public interface IShellOverlayService
{
    /// <summary>删除确认浮层；确认删除返回 true，取消/关闭返回 false。</summary>
    Task<bool> ConfirmDeleteAsync(string confirmationText, string warningText, string deletePath);

    Task ShowPropertiesAsync(object dataContext);

    Task ShowStatisticsAsync(object dataContext);

    Task ShowAboutAsync();

    Task ShowDocumentAsync(
        string title,
        string markdown,
        string? imageBasePath,
        string? typographyTheme,
        string typographySize);

    Task ShowMcpSettingsAsync(McpSettingsViewModel viewModel);

    Task ShowMcpAuditAsync(Vex.Modules.Mcp.Services.IMcpOperationAuditService auditService, string title, string refreshText, string emptyText);

    /// <summary>Ctrl+P 快速打开浮层；返回用户选中的文件路径，取消返回 null。</summary>
    Task<string?> ShowQuickOpenAsync(Vex.Modules.Shell.ViewModels.ShellQuickOpenViewModel viewModel);
}
