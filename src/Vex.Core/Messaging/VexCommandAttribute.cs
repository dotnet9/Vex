namespace Vex.Core.Messaging;

/// <summary>
/// 命令元数据：标注在 ShellActionKind / EditorActionKind 枚举成员上，
/// 由 Vex.Generators 增量生成器编译期收集为命令面板数据源（对齐 WalletWasabi NavigationMetaData 模式）。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class VexCommandAttribute : Attribute
{
    /// <summary>命令分组（命令面板的分组徽标，如 文件/编辑/插入）。</summary>
    public string Group { get; }

    /// <summary>命令标签（中文原文，本地化走既有 label 回退链）。</summary>
    public string Label { get; }

    /// <summary>快捷键提示（可选）。</summary>
    public string Gesture { get; set; } = string.Empty;

    public VexCommandAttribute(string group, string label)
    {
        Group = group;
        Label = label;
    }
}
