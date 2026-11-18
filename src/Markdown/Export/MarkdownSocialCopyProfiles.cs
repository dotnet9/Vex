namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Built-in social editor copy profiles.
/// </summary>
public static class MarkdownSocialCopyProfiles
{
	public static readonly MarkdownSocialCopyProfile Wechat = new("wechat", MarkdownSocialCopyFormat.Wechat);

	public static readonly MarkdownSocialCopyProfile Zhihu = new("zhihu", MarkdownSocialCopyFormat.Mountain);

	public static readonly MarkdownSocialCopyProfile Juejin = Zhihu with
	{
		TargetName = "juejin",
		AppendSuffix = true
	};

	public static MarkdownSocialCopyProfile Resolve(CopyKind target)
	{
		return target switch
		{
			CopyKind.Wechat => Wechat,
			CopyKind.Zhihu => Zhihu,
			CopyKind.Juejin => Juejin,
			_ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported social copy target.")
		};
	}

	public static bool TryResolve(string? targetName, out MarkdownSocialCopyProfile profile)
	{
		profile = Wechat;
		if (string.IsNullOrWhiteSpace(targetName))
		{
			return false;
		}

		switch (targetName.Trim().ToLowerInvariant())
		{
			case "wechat":
			case "weixin":
			case "mp-weixin":
			case "wechat-official-account":
				profile = Wechat;
				return true;
			case "zhihu":
				profile = Zhihu;
				return true;
			case "juejin":
				profile = Juejin;
				return true;
			default:
				return false;
		}
	}
}
