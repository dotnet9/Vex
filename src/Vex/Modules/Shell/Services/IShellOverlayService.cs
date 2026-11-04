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



    Task ShowAboutAsync();

    /// <summary>打开（或置前）非模态更新窗：后台下载/就绪状态恢复现场，不重新检查。</summary>
    Task ShowUpdatesAsync();

    /// <summary>更新会话的单一数据源：标题栏药丸与日志气泡绑定它，更新窗与「关于」入口复用同一实例。</summary>
    ShellUpdateViewModel UpdateSession { get; }

    /// <summary>当前更新窗（尚未打开或已关闭为 null）：下载中关窗被拦截为隐藏，窗体实例仍由服务持有。</summary>
    Vex.Modules.Shell.Views.ShellUpdateWindow? UpdateWindow { get; }

    /// <summary>应用启动后调用一次：后台静默检查新版，发现新版只亮药丸并 toast，失败不打扰主界面。</summary>
    void StartUpdateWatch();

    Task ShowPreferencesAsync(ShellPreferencesViewModel viewModel, string category = "general");

    Task ShowMcpSettingsAsync(McpSettingsViewModel viewModel);

    Task ShowMcpAuditAsync(Vex.Modules.Mcp.Services.IMcpOperationAuditService auditService, string title, string refreshText, string emptyText);

    /// <summary>Ctrl+P 快速打开浮层；返回用户选中的文件路径，取消返回 null。</summary>
    Task<string?> ShowQuickOpenAsync(Vex.Modules.Shell.ViewModels.ShellQuickOpenViewModel viewModel);
}
