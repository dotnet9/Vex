using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class WorkspaceDocumentState : IWorkspaceDocumentState
{
    private readonly IEventBus _eventBus;
    private string _markdown = string.Empty;

    public WorkspaceDocumentState(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public string Markdown => _markdown;

    public void UpdateMarkdown(string markdown)
    {
        var normalized = markdown ?? string.Empty;
        if (_markdown == normalized)
        {
            return;
        }

        _markdown = normalized;
        // 文档正文变化统一广播，预览、大纲和后续可视化编辑都可以复用这一条轻量状态通道。
        _eventBus.Publish(new MarkdownDocumentChangedCommand(_markdown));
    }
}
