using System.ComponentModel;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 大纲折叠状态的持久化编解码：配置值使用转义后的独立行，避免路径和标题中的
/// 分隔符互相干扰；配合 <see cref="OutlineExpansionKey"/> 生成的稳定键读写宿主配置。
/// </summary>
public static class OutlineExpansionState
{
	public static string PathKey(string path)
	{
		var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
	}

	public static HashSet<string> Read(string? value) => new(
		(value ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

	public static string Encode(string key) => Uri.EscapeDataString(key);

	public static string Set(string? value, string key, bool collapsed)
	{
		var entries = Read(value);
		if (collapsed) entries.Add(key); else entries.Remove(key);
		return string.Join('\n', entries.Order(StringComparer.Ordinal));
	}
}

/// <summary>
/// 折叠键生成：文档路径 + 标题祖先链 + 层级 + 同名出现序号。
/// 同名标题通过出现序号区分，重建大纲后键保持稳定，宿主据此持久化折叠状态。
/// </summary>
public static class OutlineExpansionKey
{
	public static IReadOnlyDictionary<OutlineRow, string> Build(
		IReadOnlyList<OutlineRow> rows, string documentKey)
	{
		var keys = new Dictionary<OutlineRow, string>();
		var ancestors = new Stack<OutlineRow>();
		var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			while (ancestors.Count > 0 && ancestors.Peek().Level >= row.Level) ancestors.Pop();
			var parentKey = ancestors.Count > 0 ? keys[ancestors.Peek()] : documentKey;
			var key = parentKey + "/" + row.Level + ":" + OutlineExpansionState.Encode(row.Title);
			occurrences.TryGetValue(key, out var occurrence);
			occurrences[key] = occurrence + 1;
			keys[row] = key + ":" + occurrence;
			ancestors.Push(row);
		}

		return keys;
	}

	/// <summary>便捷重载：直接按条目集合（Level/Title）生成与面板行同序的键序列。</summary>
	public static IReadOnlyList<string> BuildSequence(IEnumerable<(int Level, string Title)> items, string documentKey)
	{
		var keys = new List<string>();
		var ancestors = new Stack<int>(); // 栈存层级
		var ancestorsKeys = new Stack<string>();
		var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var (level, title) in items)
		{
			while (ancestors.Count > 0 && ancestors.Peek() >= level)
			{
				ancestors.Pop();
				ancestorsKeys.Pop();
			}

			var parentKey = ancestorsKeys.Count > 0 ? ancestorsKeys.Peek() : documentKey;
			var key = parentKey + "/" + level + ":" + OutlineExpansionState.Encode(title);
			occurrences.TryGetValue(key, out var occurrence);
			occurrences[key] = occurrence + 1;
			var final = key + ":" + occurrence;
			keys.Add(final);
			ancestors.Push(level);
			ancestorsKeys.Push(final);
		}

		return keys;
	}
}
