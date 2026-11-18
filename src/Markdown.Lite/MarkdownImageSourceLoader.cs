using System.Text;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Loads Markdown image sources from data URIs, local files, file URIs, and HTTP(S) URLs.
/// </summary>
public static class MarkdownImageSourceLoader
{
	public const long DefaultMaxRemoteImageBytes = 32L * 1024 * 1024;

	private static readonly HttpClient HttpClient = CreateHttpClient();

	public static MarkdownImageSource Load(
		string? source,
		string? imageBasePath = null,
		long maxRemoteImageBytes = DefaultMaxRemoteImageBytes,
		long maxLocalImageBytes = DefaultMaxRemoteImageBytes,
		Func<string, string?>? resolveLocalSource = null)
	{
		ValidateMaxImageBytes(maxRemoteImageBytes, nameof(maxRemoteImageBytes));
		ValidateMaxImageBytes(maxLocalImageBytes, nameof(maxLocalImageBytes));
		var normalizedSource = NormalizeSource(source);
		if (TryReadDataUri(normalizedSource, out var dataUriBytes, out var dataUriIsSvg, out var dataUriIsGif))
		{
			ValidateImageLength(dataUriBytes.Length, maxLocalImageBytes);
			return new MarkdownImageSource(
				dataUriBytes,
				ResolveFileName(normalizedSource),
				dataUriIsSvg || IsSvgBytes(dataUriBytes),
				dataUriIsGif || IsGifBytes(dataUriBytes),
				null);
		}

		if (Uri.TryCreate(normalizedSource, UriKind.Absolute, out var uri))
		{
			if (uri.Scheme is "http" or "https")
			{
				// 宿主解析回调先行（如 CDN URL 反查本地文件离线显示）：返回本地路径
				// 时仍走白名单与大小限制的本地加载；返回 null/空白时按原样远程获取。
				var resolvedLocal = resolveLocalSource?.Invoke(normalizedSource);
				if (!string.IsNullOrWhiteSpace(resolvedLocal))
				{
					return LoadLocalFile(resolvedLocal, resolvedLocal, maxLocalImageBytes);
				}

				var bytes = ReadRemoteBytesWithDiskCache(uri, maxRemoteImageBytes);
				return new MarkdownImageSource(
					bytes,
					ResolveFileName(normalizedSource),
					IsSvgPath(uri.LocalPath) || IsSvgBytes(bytes),
					IsGifPath(uri.LocalPath) || IsGifBytes(bytes),
					null);
			}

			if (uri.IsFile)
			{
				return LoadLocalFile(uri.LocalPath, uri.LocalPath, maxLocalImageBytes);
			}
		}

		var localPath = ResolveLocalPath(normalizedSource, imageBasePath);
		return LoadLocalFile(localPath.Path, localPath.DisplayPath, maxLocalImageBytes);
	}

	public static async Task<MarkdownImageSource> LoadAsync(
		string? source,
		string? imageBasePath = null,
		CancellationToken cancellationToken = default,
		long maxRemoteImageBytes = DefaultMaxRemoteImageBytes,
		long maxLocalImageBytes = DefaultMaxRemoteImageBytes,
		Func<string, string?>? resolveLocalSource = null)
	{
		ValidateMaxImageBytes(maxRemoteImageBytes, nameof(maxRemoteImageBytes));
		ValidateMaxImageBytes(maxLocalImageBytes, nameof(maxLocalImageBytes));
		var normalizedSource = NormalizeSource(source);
		if (TryReadDataUri(normalizedSource, out var dataUriBytes, out var dataUriIsSvg, out var dataUriIsGif))
		{
			ValidateImageLength(dataUriBytes.Length, maxLocalImageBytes);
			return new MarkdownImageSource(
				dataUriBytes,
				ResolveFileName(normalizedSource),
				dataUriIsSvg || IsSvgBytes(dataUriBytes),
				dataUriIsGif || IsGifBytes(dataUriBytes),
				null);
		}

		if (Uri.TryCreate(normalizedSource, UriKind.Absolute, out var uri))
		{
			if (uri.Scheme is "http" or "https")
			{
				var resolvedLocal = resolveLocalSource?.Invoke(normalizedSource);
				if (!string.IsNullOrWhiteSpace(resolvedLocal))
				{
					return await LoadLocalFileAsync(resolvedLocal, resolvedLocal, maxLocalImageBytes, cancellationToken);
				}

				var bytes = await ReadRemoteBytesWithDiskCacheAsync(uri, maxRemoteImageBytes, cancellationToken);
				return new MarkdownImageSource(
					bytes,
					ResolveFileName(normalizedSource),
					IsSvgPath(uri.LocalPath) || IsSvgBytes(bytes),
					IsGifPath(uri.LocalPath) || IsGifBytes(bytes),
					null);
			}

			if (uri.IsFile)
			{
				return await LoadLocalFileAsync(uri.LocalPath, uri.LocalPath, maxLocalImageBytes, cancellationToken);
			}
		}

		var localPath = ResolveLocalPath(normalizedSource, imageBasePath);
		return await LoadLocalFileAsync(localPath.Path, localPath.DisplayPath, maxLocalImageBytes, cancellationToken);
	}

	public static string CreateCacheKey(string source, string? imageBasePath)
	{
		var normalizedSource = NormalizeSource(source);
		if (IsDataUri(normalizedSource)
			|| Uri.TryCreate(normalizedSource, UriKind.Absolute, out _)
			|| Path.IsPathRooted(normalizedSource))
		{
			return normalizedSource;
		}

		return $"{ResolveImageBaseDirectory(imageBasePath) ?? AppContext.BaseDirectory}|{normalizedSource}";
	}

	private static HttpClient CreateHttpClient()
	{
		var client = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30)
		};
		client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeWF.Avalonia.Markdown/12");
		return client;
	}

	private static string NormalizeSource(string? source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			throw new ArgumentException("Markdown image source cannot be empty.", nameof(source));
		}

		return source.Trim();
	}

	private static MarkdownLocalImagePath ResolveLocalPath(string source, string? imageBasePath)
	{
		foreach (var candidate in EnumerateLocalPathCandidates(source))
		{
			if (Path.IsPathRooted(candidate))
			{
				return new MarkdownLocalImagePath(Path.GetFullPath(candidate), candidate);
			}
		}

		var baseDirectory = ResolveImageBaseDirectory(imageBasePath) ?? AppContext.BaseDirectory;
		foreach (var candidate in EnumerateLocalPathCandidates(source))
		{
			var path = Path.GetFullPath(Path.Combine(baseDirectory, candidate));
			if (File.Exists(path))
			{
				return new MarkdownLocalImagePath(path, path);
			}
		}

		var fallback = Path.GetFullPath(Path.Combine(baseDirectory, source.Replace('/', Path.DirectorySeparatorChar)));
		return new MarkdownLocalImagePath(fallback, fallback);
	}

	private static IEnumerable<string> EnumerateLocalPathCandidates(string source)
	{
		var normalized = source.Replace('/', Path.DirectorySeparatorChar);
		yield return normalized;

		var decoded = DecodeLocalImageUrl(normalized);
		if (!string.Equals(decoded, normalized, StringComparison.Ordinal))
		{
			yield return decoded;
		}
	}

	private static string DecodeLocalImageUrl(string source)
	{
		try
		{
			return Uri.UnescapeDataString(source);
		}
		catch (UriFormatException)
		{
			return source;
		}
	}

	private static string? ResolveImageBaseDirectory(string? imageBasePath)
	{
		var basePath = imageBasePath?.Trim();
		if (string.IsNullOrWhiteSpace(basePath))
		{
			return null;
		}

		try
		{
			if (Uri.TryCreate(basePath, UriKind.Absolute, out var uri) && uri.IsFile)
			{
				return ResolveExistingDirectory(uri.LocalPath);
			}

			return ResolveExistingDirectory(basePath);
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return null;
		}
	}

	private static string? ResolveExistingDirectory(string path)
	{
		var fullPath = Path.GetFullPath(path);
		if (Directory.Exists(fullPath))
		{
			return fullPath;
		}

		var directory = Path.GetDirectoryName(fullPath);
		return string.IsNullOrWhiteSpace(directory) ? null : directory;
	}

	private static MarkdownImageSource LoadLocalFile(string path, string displayPath, long maxImageBytes)
	{
		EnsureLocalPathAllowed(path);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Markdown image file was not found.", displayPath);
		}

		var fileInfo = new FileInfo(path);
		ValidateImageLength(fileInfo.Length, maxImageBytes);
		var bytes = File.ReadAllBytes(path);
		ValidateImageLength(bytes.Length, maxImageBytes);
		return new MarkdownImageSource(
			bytes,
			ResolveFileName(path),
			IsSvgPath(path) || IsSvgBytes(bytes),
			IsGifPath(path) || IsGifBytes(bytes),
			path);
	}

	private static async Task<MarkdownImageSource> LoadLocalFileAsync(
		string path,
		string displayPath,
		long maxImageBytes,
		CancellationToken cancellationToken)
	{
		EnsureLocalPathAllowed(path);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Markdown image file was not found.", displayPath);
		}

		var fileInfo = new FileInfo(path);
		ValidateImageLength(fileInfo.Length, maxImageBytes);
		var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
		ValidateImageLength(bytes.Length, maxImageBytes);
		return new MarkdownImageSource(
			bytes,
			ResolveFileName(path),
			IsSvgPath(path) || IsSvgBytes(bytes),
			IsGifPath(path) || IsGifBytes(bytes),
			path);
	}

	private static byte[] ReadRemoteBytes(Uri uri, long maxRemoteImageBytes)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		using var response = HttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		using var source = response.Content.ReadAsStream();
		return ReadRemoteResponseBytes(source, response.Content.Headers.ContentLength, maxRemoteImageBytes);
	}

	/// <summary>远程读取附带磁盘缓存：命中免网络，未命中取回后回存。</summary>
	private static byte[] ReadRemoteBytesWithDiskCache(Uri uri, long maxRemoteImageBytes)
	{
		var cacheKey = "remote|" + uri.ToString();
		var cached = MarkdownImageDiskCache.TryRead(cacheKey);
		if (cached is not null)
		{
			return cached;
		}

		var bytes = ReadRemoteBytes(uri, maxRemoteImageBytes);
		MarkdownImageDiskCache.Store(cacheKey, bytes);
		return bytes;
	}

	private static async Task<byte[]> ReadRemoteBytesWithDiskCacheAsync(
		Uri uri, long maxRemoteImageBytes, CancellationToken cancellationToken)
	{
		var cacheKey = "remote|" + uri.ToString();
		var cached = MarkdownImageDiskCache.TryRead(cacheKey);
		if (cached is not null)
		{
			return cached;
		}

		var bytes = await ReadRemoteBytesAsync(uri, maxRemoteImageBytes, cancellationToken);
		MarkdownImageDiskCache.Store(cacheKey, bytes);
		return bytes;
	}

	// 本地图片白名单：配置后只允许读取根目录之内的本地图片（file:// 与
	// 相对路径解析共用此约束）。TKCode 式做法：不可信文档的本地图片
	// 只能来自已添加的项目目录。大小写不敏感，根按目录边界匹配。
	private static volatile string[] _allowedLocalImageRoots = [];

	/// <summary>当前允许的本地图片根目录（绝对路径；空数组 = 不限制）。</summary>
	public static IReadOnlyList<string> AllowedLocalImageRoots => _allowedLocalImageRoots;

	/// <summary>设置本地图片白名单根目录（传空集合表示不限制）。</summary>
	public static void SetAllowedLocalImageRoots(IEnumerable<string> roots)
	{
		ArgumentNullException.ThrowIfNull(roots);
		_allowedLocalImageRoots = roots
			.Where(root => !string.IsNullOrWhiteSpace(root))
			.Select(root => Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderByDescending(root => root.Length)
			.ToArray();
	}

	/// <summary>清空白名单（恢复不限制）。</summary>
	public static void ClearAllowedLocalImageRoots() => _allowedLocalImageRoots = [];

	private static void EnsureLocalPathAllowed(string path)
	{
		var roots = _allowedLocalImageRoots;
		if (roots.Length == 0)
		{
			return;
		}

		var fullPath = Path.GetFullPath(path);
		foreach (var root in roots)
		{
			if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
				|| fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
				|| fullPath.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}

		throw new UnauthorizedAccessException(string.Format(MarkdownLocalization.GetResource(MarkdownL.LocalImageOutsideRoots), fullPath));
	}

	private static async Task<byte[]> ReadRemoteBytesAsync(Uri uri, long maxRemoteImageBytes, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
		return await ReadRemoteResponseBytesAsync(source, response.Content.Headers.ContentLength, maxRemoteImageBytes, cancellationToken);
	}

	private static byte[] ReadRemoteResponseBytes(Stream source, long? contentLength, long maxRemoteImageBytes)
	{
		ValidateRemoteLength(contentLength, maxRemoteImageBytes);
		using var memory = new MemoryStream(GetInitialRemoteImageCapacity(contentLength, maxRemoteImageBytes));
		var buffer = new byte[81920];
		int read;
		while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
		{
			ValidateRemoteLength(memory.Length + read, maxRemoteImageBytes);
			memory.Write(buffer, 0, read);
		}

		return memory.ToArray();
	}

	private static async Task<byte[]> ReadRemoteResponseBytesAsync(
		Stream source,
		long? contentLength,
		long maxRemoteImageBytes,
		CancellationToken cancellationToken)
	{
		ValidateRemoteLength(contentLength, maxRemoteImageBytes);
		using var memory = new MemoryStream(GetInitialRemoteImageCapacity(contentLength, maxRemoteImageBytes));
		var buffer = new byte[81920];
		int read;
		while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
		{
			ValidateRemoteLength(memory.Length + read, maxRemoteImageBytes);
			await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		return memory.ToArray();
	}

	private static void ValidateRemoteLength(long? contentLength, long maxRemoteImageBytes)
	{
		ValidateMaxImageBytes(maxRemoteImageBytes, nameof(maxRemoteImageBytes));
		if (contentLength > maxRemoteImageBytes)
		{
			throw new InvalidDataException("Markdown image response is too large.");
		}
	}

	private static void ValidateImageLength(long length, long maxImageBytes)
	{
		if (length > maxImageBytes)
		{
			throw new InvalidDataException("Markdown image data is too large.");
		}
	}

	private static void ValidateMaxImageBytes(long maxImageBytes, string parameterName)
	{
		if (maxImageBytes is <= 0 or > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(parameterName);
		}
	}

	private static int GetInitialRemoteImageCapacity(long? contentLength, long maxRemoteImageBytes)
	{
		return contentLength is > 0 and <= int.MaxValue
			? (int)Math.Min(contentLength.Value, maxRemoteImageBytes)
			: 0;
	}

	private static bool TryReadDataUri(string source, out byte[] bytes, out bool isSvg, out bool isGif)
	{
		bytes = [];
		isSvg = false;
		isGif = false;

		if (!IsDataUri(source))
		{
			return false;
		}

		var commaIndex = source.IndexOf(',');
		if (commaIndex < 0)
		{
			return false;
		}

		var metadata = source[..commaIndex];
		var payload = source[(commaIndex + 1)..];
		isSvg = IsSvgMediaType(metadata);
		isGif = IsGifMediaType(metadata);
		bytes = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
			? Convert.FromBase64String(payload)
			: Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
		return true;
	}

	private static bool IsDataUri(string source)
	{
		return source.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSvgMediaType(string? mediaType)
	{
		return mediaType?.Contains("image/svg+xml", StringComparison.OrdinalIgnoreCase) == true;
	}

	private static bool IsSvgPath(string path)
	{
		return string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSvgBytes(byte[] bytes)
	{
		var length = Math.Min(bytes.Length, 512);
		if (length == 0)
		{
			return false;
		}

		var prefix = Encoding.UTF8.GetString(bytes, 0, length).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
		return prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
			   || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
			   && prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsGifMediaType(string? mediaType)
	{
		return mediaType?.Contains("image/gif", StringComparison.OrdinalIgnoreCase) == true;
	}

	private static bool IsGifPath(string path)
	{
		return string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsGifBytes(byte[] bytes)
	{
		return bytes.Length >= 6
			   && bytes[0] == 'G'
			   && bytes[1] == 'I'
			   && bytes[2] == 'F'
			   && bytes[3] == '8'
			   && (bytes[4] == '7' || bytes[4] == '9')
			   && bytes[5] == 'a';
	}

	private static string ResolveFileName(string source)
	{
		var fileName = "markdown-image";
		if (IsDataUri(source))
		{
			fileName += ResolveDataUriExtension(source);
		}
		else if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
		{
			fileName = Path.GetFileName(uri.IsFile ? uri.LocalPath : uri.LocalPath);
		}
		else
		{
			fileName = Path.GetFileName(source);
		}

		if (string.IsNullOrWhiteSpace(fileName))
		{
			fileName = "markdown-image.png";
		}
		else if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
		{
			fileName += ".png";
		}

		foreach (var invalidChar in Path.GetInvalidFileNameChars())
		{
			fileName = fileName.Replace(invalidChar, '_');
		}

		return fileName;
	}

	private static string ResolveDataUriExtension(string source)
	{
		var semicolonIndex = source.IndexOf(';');
		var commaIndex = source.IndexOf(',');
		var separatorIndex = semicolonIndex >= 0
			? semicolonIndex
			: commaIndex;
		if (separatorIndex <= "data:image/".Length)
		{
			return ".png";
		}

		return source["data:image/".Length..separatorIndex].ToLowerInvariant() switch
		{
			"svg+xml" => ".svg",
			"jpeg" => ".jpg",
			"jpg" => ".jpg",
			"webp" => ".webp",
			"bmp" => ".bmp",
			"gif" => ".gif",
			_ => ".png"
		};
	}

	private sealed record MarkdownLocalImagePath(string Path, string DisplayPath);
}
