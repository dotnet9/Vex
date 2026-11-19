using AvaloniaEdit;

namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>查找/替换结果回调：宿主用它更新查找栏文案与计数。</summary>
public delegate void MarkdownEditorSearchResultHandler(string message, int currentIndex, int totalCount);

/// <summary>
/// 查找/替换服务：正则、大小写与全词匹配，支持计数、上一个/下一个、替换当前与全部替换。
/// 只有真正改写正文的动作走 <paramref name="runTextMutation"/>；定位命中只移动选区，不回报文本变化。
/// </summary>
public interface IMarkdownEditorSearchService
{
    void Search(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
        Action<Action> runTextMutation,
        MarkdownEditorSearchResultHandler report);
}
