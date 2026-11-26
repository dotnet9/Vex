using System.Runtime.CompilerServices;

// 源引用拆分过渡桥：原单程序集时代的 internal 类型（MarkdownTemplates/WorkspaceFileEnumeration 等）
// 在 Vex.Core 中跨程序集不可见。加此桥后 Vex 主工程可继续使用；
// 长期应将这些类型评估为 public 或合并回调用方，届时移除本桥。
[assembly: InternalsVisibleTo("Vex")]
[assembly: InternalsVisibleTo("Vex.Core.Tests")]
