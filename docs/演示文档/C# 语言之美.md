# C# 语言之美：让数据与异步工作流更清楚

C# 是语言，.NET 是运行平台与开发工具生态。这里围绕一个 Avalonia 文档工作区，观察类型、模式匹配、集合与异步如何配合。版本关系见 [.NET 历史与版本关系](.NET%20历史与版本关系.md)。

## 用 record 表达不可变数据

```csharp
public sealed record DocumentInfo(string Path, string Title, bool IsDirty);

var document = new DocumentInfo("notes.md", "架构笔记", false);
var edited = document with { IsDirty = true };
Console.WriteLine($"{edited.Title}: {edited.IsDirty}");
```

`with` 表达式创建新值，原对象保持原样。适合快照、配置和状态比较；它不自动解决深层集合的可变性。

## 模式匹配让分支贴近含义

```csharp
static string Describe(DocumentInfo document) => document switch
{
    { IsDirty: true } => $"{document.Title} · 未保存",
    { Title.Length: 0 } => "未命名文档",
    _ => document.Title
};
```

把状态判断放在可测试的函数或 ViewModel 中，AXAML 只消费结果，不负责推断业务状态。

## 集合表达式与 LINQ

```csharp
DocumentInfo[] files =
[
    new("guide.md", "Avalonia 入门", false),
    new("draft.md", ".NET 方案", true),
    new("notes.md", "C# 记录", true)
];

var titles = files.Where(file => file.IsDirty)
                 .OrderBy(file => file.Title, StringComparer.Ordinal)
                 .Select(file => file.Title)
                 .ToArray();
```

集合表达式要求 C# 12 或更新版本。LINQ 的延迟执行有助于组合查询；需要稳定快照时再调用 `ToArray()`。

## 异步加载与取消

```csharp
public static async Task<string> LoadPreviewAsync(
    string path, CancellationToken cancellationToken)
{
    var markdown = await File.ReadAllTextAsync(path, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();
    return markdown;
}
```

文件切换时应取消过期请求，并检查返回结果是否仍属于当前文档。`async` 不等于启动后台线程；IO 等待与 CPU 密集计算需要分别安排。

## 泛型约束记录真正需要的能力

```csharp
static T FirstOrDefault<T>(IReadOnlyList<T> items, T fallback) =>
    items.Count == 0 ? fallback : items[0];
```

没有需要的能力就不额外添加约束。Nullable 标注、参数验证与清楚的返回约定，比把所有异常都吞掉更有用。

## 小结与练习

- [x] 用值对象保存文档元数据
- [x] 用模式匹配表达互斥状态
- [ ] 为取消、空列表和重复标题补测试

> 当一个抽象让调用者更难理解输入与结果时，先考虑删掉它。

官方参考：[C# 文档](https://learn.microsoft.com/dotnet/csharp/)、[C# 版本历史](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-version-history)。
