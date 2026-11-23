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

    /// <summary>模板中心浮层；initialMode=SaveForm 直接进入另存当前文档为模板。</summary>
    Task ShowTemplateCenterAsync(Vex.Modules.Templates.ViewModels.TemplateViewMode initialMode = Vex.Modules.Templates.ViewModels.TemplateViewMode.Picker, string? initialSourceFile = null);

    /// <summary>另存为模板（独立菜单入口）；sourceFilePath 非空时以该文件为正文（文件列表右键入口）。</summary>
    Task ShowTemplateSaveAsync(string? sourceFilePath = null);

    Task ShowMcpAuditAsync(Vex.Modules.Mcp.Services.IMcpOperationAuditService auditService, string title, string refreshText, string emptyText);

    /// <summary>Ctrl+P 快速打开浮层；返回用户选中的文件路径，取消返回 null。</summary>
    Task<string?> ShowQuickOpenAsync(Vex.Modules.Shell.ViewModels.ShellQuickOpenViewModel viewModel);

    /// <summary>Git 远程地址弹层（原型 layer-git-remote）；currentUrl 为已存地址（编辑态预填），取消返回 null。</summary>
    Task<string?> ShowGitRemoteAsync(string? currentUrl);

    /// <summary>HTTPS 远程访问令牌（PAT）输入弹层；返回打包答案（令牌+是否记住），取消返回 null。</summary>
    Task<string?> ShowGitPatAsync();

    /// <summary>新建/重命名分支弹层（GitHub Desktop create-branch-dialog 口径）：返回 名称+基准，取消返回 null。</summary>
    Task<Vex.Modules.Git.ViewModels.GitBranchDialogResult?> ShowGitBranchDialogAsync(
        Vex.Modules.Git.ViewModels.GitBranchDialogRequest request);

    /// <summary>合并分支弹层（GitHub Desktop merge-choose-branch-dialog 口径）：选择源分支，取消返回 null。</summary>
    Task<string?> ShowGitMergeDialogAsync(string currentBranch, IReadOnlyList<string> sourceBranches);

    /// <summary>删除分支确认弹层（GitHub Desktop delete-branch-dialog 口径）：确认返回 true。</summary>
    Task<bool> ShowGitBranchDeleteConfirmAsync(string branchName);
}
