using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

/// <summary>
/// Git 变更操作完成（面板提交/推送/拉取/放弃/自动提交、MCP git 工具）：
/// 文件树 Git 徽章据此触发重算——提交不改工作区文件内容，文件监控收不到，必须显式通知。
/// </summary>
public sealed class WorkspaceGitMutatedCommand : Command
{
    public WorkspaceGitMutatedCommand(string? workspaceRootPath) => WorkspaceRootPath = workspaceRootPath;

    public string? WorkspaceRootPath { get; }
}
