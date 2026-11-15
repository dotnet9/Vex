using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Templates.Services;

/// <summary>
/// 模板中心对当前文档能力的抽象（解耦主窗体 ViewModel，便于集成测试）。
/// </summary>
public interface ITemplateDocumentTarget
{
    /// <summary>当前文档正文。</summary>
    string Markdown { get; }

    /// <summary>当前文档名（含扩展名）。</summary>
    string CurrentDocumentName { get; }

    /// <summary>是否处于可编辑视图。</summary>
    bool IsEditing { get; }

    /// <summary>以模板内容整体替换当前文档正文（同 ReplaceMarkdownFromMcp 通道）。</summary>
    void ApplyMarkdown(string markdown);

    /// <summary>走新建确认流，以模板内容新建独立文档。</summary>
    Task NewDocumentFromTemplateAsync(string fileName, string markdown);
}

/// <summary>主窗体 ViewModel 的能力适配器。</summary>
public sealed class MainWindowTemplateTarget(MainWindowViewModel main) : ITemplateDocumentTarget
{
    public string Markdown => main.Markdown;
    public string CurrentDocumentName => main.DocumentInfo.CurrentDocumentName;
    public bool IsEditing => main.Workbench.IsEditing;
    public void ApplyMarkdown(string markdown) => main.ApplyTemplateToCurrentDocument(markdown);
    public Task NewDocumentFromTemplateAsync(string fileName, string markdown) =>
        main.NewDocumentFromTemplateAsync(fileName, markdown);
}
