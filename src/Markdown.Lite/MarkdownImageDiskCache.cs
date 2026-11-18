using System.IO;
using System.Security.Cryptography;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// 远程图片磁盘缓存（进程外持久化）。默认禁用；宿主启动时设置
/// <see cref="CacheDirectory"/> 即启用（TKCode 式做法：磁盘缓存 +
/// 进程内存 LRU 配合，二次打开同一文档图片秒出、断网可看）。
/// 所有 IO 尽力而为：损坏/被清理只表现为缓存未命中。
/// </summary>
public static class MarkdownImageDiskCache
{
	private static readonly object Gate = new();

	/// <summary>缓存目录；null 或空表示禁用。</summary>
	public static string? CacheDirectory { get; set; }

	public static byte[]? TryRead(string key)
	{
		var path = GetCacheFilePath(key);
		if (path is null)
		{
			return null;
		}

		try
		{
			return File.ReadAllBytes(path);
		}
		catch (DirectoryNotFoundException)
		{
			return null;
		}
		catch (FileNotFoundException)
		{
			return null;
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
	}

	public static void Store(string key, byte[] bytes)
	{
		if (bytes.Length == 0)
		{
			return;
		}

		var path = GetCacheFilePath(key);
		if (path is null)
		{
			return;
		}

		try
		{
			lock (Gate)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				// 原子写入：临时文件后移动，避免并发读到半截文件。
				var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
				File.WriteAllBytes(temporaryPath, bytes);
				File.Move(temporaryPath, path, overwrite: true);
			}
		}
		catch (IOException)
		{
			// 磁盘满/被占用等：缓存写入失败不影响图片加载。
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	private static string? GetCacheFilePath(string key)
	{
		var directory = CacheDirectory;
		if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrEmpty(key))
		{
			return null;
		}

		var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
		return Path.Combine(directory, Convert.ToHexString(hash).ToLowerInvariant());
	}
}
