using System.Text;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// Loads image bytes for lightweight renderers with bounded local and remote reads.
/// </summary>
internal static class MarkdownImageByteLoader
{
	public const long DefaultMaxImageBytes = 32L * 1024 * 1024;

	private static readonly HttpClient HttpClient = CreateHttpClient();

	public static async Task<byte[]> LoadAsync(
		string source,
		string? imageBasePath,
		CancellationToken cancellationToken,
		long maxImageBytes = DefaultMaxImageBytes)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			throw new ArgumentException("Markdown image source cannot be empty.", nameof(source));
		}

		ValidateMaxImageBytes(maxImageBytes);
		var normalizedSource = source.Trim();
		if (TryReadDataUri(normalizedSource, maxImageBytes, out var dataUriBytes))
		{
			return dataUriBytes;
		}

		if (Uri.TryCreate(normalizedSource, UriKind.Absolute, out var uri))
		{
			if (uri.Scheme is "http" or "https")
			{
				return await ReadRemoteBytesAsync(uri, maxImageBytes, cancellationToken);
			}

			if (uri.IsFile)
			{
				return await ReadFileBytesAsync(uri.LocalPath, uri.LocalPath, maxImageBytes, cancellationToken);
			}

			throw new NotSupportedException($"Unsupported Markdown image URI scheme: {uri.Scheme}.");
		}

		var localPath = ResolveLocalPath(normalizedSource, imageBasePath);
		return await ReadFileBytesAsync(localPath.Path, localPath.DisplayPath, maxImageBytes, cancellationToken);
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

	private static async Task<byte[]> ReadRemoteBytesAsync(Uri uri, long maxImageBytes, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
		return await ReadBoundedBytesAsync(source, response.Content.Headers.ContentLength, maxImageBytes, cancellationToken);
	}

	private static async Task<byte[]> ReadFileBytesAsync(
		string path,
		string displayPath,
		long maxImageBytes,
		CancellationToken cancellationToken)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Markdown image file was not found.", displayPath);
		}

		await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		return await ReadBoundedBytesAsync(stream, stream.Length, maxImageBytes, cancellationToken);
	}

	private static async Task<byte[]> ReadBoundedBytesAsync(
		Stream source,
		long? contentLength,
		long maxImageBytes,
		CancellationToken cancellationToken)
	{
		ValidateLength(contentLength, maxImageBytes);
		using var memory = new MemoryStream(GetInitialCapacity(contentLength, maxImageBytes));
		var buffer = new byte[81920];
		int read;
		while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
		{
			ValidateLength(memory.Length + read, maxImageBytes);
			await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		return memory.ToArray();
	}

	private static bool TryReadDataUri(string source, long maxImageBytes, out byte[] bytes)
	{
		bytes = [];
		if (!source.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var commaIndex = source.IndexOf(',');
		if (commaIndex < 0)
		{
			throw new InvalidDataException("Markdown image data URI is invalid.");
		}

		var metadata = source[..commaIndex];
		var payload = source[(commaIndex + 1)..];
		if (metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
		{
			var maximumEncodedLength = ((maxImageBytes + 2) / 3) * 4 + 4;
			if (payload.Length > maximumEncodedLength)
			{
				throw new InvalidDataException("Markdown image data is too large.");
			}

			bytes = Convert.FromBase64String(payload);
		}
		else
		{
			bytes = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
		}

		ValidateLength(bytes.Length, maxImageBytes);
		return true;
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

		string decoded;
		try
		{
			decoded = Uri.UnescapeDataString(normalized);
		}
		catch (UriFormatException)
		{
			decoded = normalized;
		}

		if (!string.Equals(decoded, normalized, StringComparison.Ordinal))
		{
			yield return decoded;
		}
	}

	private static string? ResolveImageBaseDirectory(string? imageBasePath)
	{
		if (string.IsNullOrWhiteSpace(imageBasePath))
		{
			return null;
		}

		try
		{
			if (Uri.TryCreate(imageBasePath.Trim(), UriKind.Absolute, out var uri) && uri.IsFile)
			{
				return ResolveExistingDirectory(uri.LocalPath);
			}

			return ResolveExistingDirectory(imageBasePath.Trim());
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

	private static void ValidateMaxImageBytes(long maxImageBytes)
	{
		if (maxImageBytes is <= 0 or > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(nameof(maxImageBytes));
		}
	}

	private static void ValidateLength(long? length, long maxImageBytes)
	{
		if (length > maxImageBytes)
		{
			throw new InvalidDataException("Markdown image data is too large.");
		}
	}

	private static int GetInitialCapacity(long? length, long maxImageBytes)
	{
		return length is > 0 and <= int.MaxValue
			? (int)Math.Min(length.Value, maxImageBytes)
			: 0;
	}

	private sealed record MarkdownLocalImagePath(string Path, string DisplayPath);
}
