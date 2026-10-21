using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

/// <summary>
/// 设置、确认和检查更新的窗内浮层服务。
/// </summary>
public interface IShellOverlayService
{
    Task<Vex.Modules.Shell.Views.DocumentReviewChoice> ShowDocumentReviewAsync(Vex.Modules.Shell.Views.ShellDocumentReviewModel model);
    Task<Vex.Modules.Workspace.Services.TableDimensions?> ShowInsertTableAsync();

    /// <summary>删除确认浮层；确认删除返回 true，取消/关闭返回 false。</summary>
    Task<bool> ConfirmDeleteAsync(string confirmationText, string warningText, string deletePath);

    Task ShowPropertiesAsync(object dataContext);

    Task ShowStatisticsAsync(object dataContext);

    Task ShowAboutAsync();

    Task ShowUpdatesAsync();

    Task ShowPreferencesAsync(ShellPreferencesViewModel viewModel, string category = "general");

    Task ShowMcpSettingsAsync(McpSettingsViewModel viewModel);

    Task ShowMcpAuditAsync(Vex.Modules.Mcp.Services.IMcpOperationAuditService auditService, string title, string refreshText, string emptyText);

    /// <summary>Ctrl+P 快速打开浮层；返回用户选中的文件路径，取消返回 null。</summary>
    Task<string?> ShowQuickOpenAsync(Vex.Modules.Shell.ViewModels.ShellQuickOpenViewModel viewModel);
}
