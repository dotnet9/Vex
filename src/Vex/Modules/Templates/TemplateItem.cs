namespace Vex.Modules.Templates;

/// <summary>
/// 模板条目：模板 = 纯 Markdown 文件（无 YAML 元数据），分组对应模板目录的子文件夹。
/// 唯一标识 = 分组 + 名称：同名覆盖仅限用户模板，系统自带模板（软件研发/运维）不可修改与覆盖。
/// </summary>
public sealed record TemplateItem(
    string Group,
    string Name,
    string Standard,
    string Description,
    IReadOnlyList<string> Tags,
    string Body,
    bool IsBuiltin)
{
    /// <summary>唯一标识：分组 + 名称（跨内置与用户模板判定同名冲突）。</summary>
    public string Identity => Group + "/" + Name;

    public bool HasStandard => !string.IsNullOrEmpty(Standard);
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public bool HasTags => Tags.Count > 0;
}
