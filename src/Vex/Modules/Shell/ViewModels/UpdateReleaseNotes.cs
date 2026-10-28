using System.Text.RegularExpressions;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>一版更新的类型化日志：原型按《更新日志》的分节与标签行渲染，这里同源解析。
/// <paramref name="ShowHeader"/> 由调用方按分节数决定——单版本时标题里已经报过版本号，表头是重复信息。</summary>
public sealed record UpdateNoteVersion(string Version, string Date, IReadOnlyList<UpdateNoteEntry> Entries)
{
    public bool ShowHeader { get; init; }
}

/// <summary>一条更新：<paramref name="TagKey"/> 是标签的界面资源键（认标签用键名，不认任何语言的文案），
/// 解析不出的行没有标签、原样成句；<paramref name="TagText"/> 是胶囊要显示的那两个字，由调用方给出。</summary>
public sealed record UpdateNoteEntry(string? TagKey, string TagText, string Text)
{
    public bool HasTag => TagKey is not null;

    // 胶囊配色按原型 .update-tag 的四档语义：新增=success、优化=info、修复=warning、删除=danger。
    // 判定用资源键而不是界面文案，四语切换后颜色不跟着漂。
    public bool IsNew => TagKey == VexL.UpdateTagNew;
    public bool IsOptimized => TagKey == VexL.UpdateTagOpt;
    public bool IsFixed => TagKey == VexL.UpdateTagFix;
    public bool IsRemoved => TagKey == VexL.UpdateTagRemove;
}

/// <summary>把 GitHub 发布正文（与《更新日志.md》同一份文本）拆成「版本 + 类型化条目」。
/// 正文标签词的词表与文案都来自 I18n 资源（生成器落成 Vex.VexReleaseTag），这里不写任何语言的字面量。</summary>
public static partial class UpdateReleaseNotesParser
{
    // 分节：## V1.13.0（2026-10-27） 到下一个分节或文本结尾。日期只用于多版本时的表头，单版本不显示。
    // 这条式子与原型 about-update.js 里的逐字符同形，只是 C# 用原始字符串省掉双反斜杠。
    [GeneratedRegex(@"\n## V(\d+(?:\.\d+)+)（([^）\n]+)）\n([\s\S]*?)(?=\n## V\d+(?:\.\d+)+（|$)")]
    private static partial Regex SectionPattern();

    // 标签行：`- ✨[新增]-正文`。表情只是视觉前缀，判定依据是方括号里的标签词，
    // 所以前缀按「非 [ 的任意字符」匹配——《更新日志》换表情不必回来改这里，旧记录里的 😄/ 前缀同样能解析。
    // 括号内不枚举词形：词表交给 I18n 资源生成的 VexReleaseTag，认不出来的方括号（自定义发布说明）
    // 退回无标签条目，与原型对同一条正文的结果一致。
    [GeneratedRegex(@"^- (?:[^\[]*)\[([^]]+)\]-(.*)$")]
    private static partial Regex EntryPattern();

    /// <param name="resolve">资源键 → 当前界面语言的文案；正文词形（UpdateTagSource*）与胶囊文案（UpdateTag*）都由它现取。</param>
    public static IReadOnlyList<UpdateNoteVersion> Parse(string? notes, Func<string, string> resolve)
    {
        if (string.IsNullOrWhiteSpace(notes)) return [];
        // 发布正文的行尾不保证是 LF（从 Windows 粘贴或多行提交都可能带 CRLF），式子里的 `\n` 会被 `\r` 顶掉，
        // 整段就退化成「没有分节」的散行——先归一化，分节和条目才不会一起解析不出来。
        notes = notes.Replace("\r\n", "\n");
        var sections = SectionPattern().Matches("\n" + notes).Select(section => new UpdateNoteVersion(
            section.Groups[1].Value, section.Groups[2].Value.Trim(), Entries(section.Groups[3].Value, resolve))).ToList();
        // 正文里没有分节标题（自定义发布说明、旧版发布、测试夹具）时整块按无版本条目渲染，
        // 不能因为少了 `## V` 就把日志丢空。
        return sections.Count > 0 ? sections : [new UpdateNoteVersion("", "", Entries(notes, resolve))];
    }

    private static IReadOnlyList<UpdateNoteEntry> Entries(string body, Func<string, string> resolve) => body
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .Select(line =>
        {
            var match = EntryPattern().Match(line);
            var key = match.Success ? VexReleaseTag.KeyOf(match.Groups[1].Value, resolve) : null;
            return key is not null
                ? new UpdateNoteEntry(key, resolve(key), match.Groups[2].Value.Trim())
                : new UpdateNoteEntry(null, string.Empty, line.StartsWith("- ") ? line[2..].Trim() : line);
        })
        .ToList();
}
