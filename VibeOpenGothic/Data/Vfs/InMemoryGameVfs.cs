namespace VibeOpenGothic.Data.Vfs;

/// <summary>
/// Test VFS backed by a dictionary. Paths are normalized to a leading slash;
/// lookups are case-insensitive.
/// </summary>
public sealed class InMemoryGameVfs : IGameVfs
{
	private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

	public void Add(string path, byte[] bytes)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(bytes);
		_files[Normalize(path)] = bytes;
	}

	public bool FileExists(string nameOrPath)
	{
		return Find(nameOrPath) is not null;
	}

	public bool DirectoryExists(string path)
	{
		var normalized = Normalize(path);
		if (normalized == "/")
		{
			return true;
		}

		var prefix = normalized.TrimEnd('/') + "/";
		return _files.Keys.Any(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
	}

	public Stream OpenRead(string nameOrPath)
	{
		var entry = Find(nameOrPath) ?? throw new FileNotFoundException(
			$"Virtual file not found: '{nameOrPath}'.",
			nameOrPath);

		return new MemoryStream(_files[entry.Path], writable: false);
	}

	public VfsFileEntry? Find(string nameOrPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(nameOrPath);

		if (nameOrPath.Contains('/') || nameOrPath.Contains('\\'))
		{
			var path = Normalize(nameOrPath);
			return _files.ContainsKey(path)
				? new VfsFileEntry(path, Path.GetFileName(path))
				: null;
		}

		foreach (var path in _files.Keys)
		{
			if (string.Equals(Path.GetFileName(path), nameOrPath, StringComparison.OrdinalIgnoreCase))
			{
				return new VfsFileEntry(path, Path.GetFileName(path));
			}
		}

		return null;
	}

	public IEnumerable<VfsFileEntry> EnumerateFiles()
	{
		foreach (var path in _files.Keys)
		{
			yield return new VfsFileEntry(path, Path.GetFileName(path));
		}
	}

	public void Dispose()
	{
	}

	private static string Normalize(string path)
	{
		var normalized = path.Replace('\\', '/').Trim();
		if (!normalized.StartsWith('/'))
		{
			normalized = "/" + normalized;
		}

		while (normalized.Contains("//", StringComparison.Ordinal))
		{
			normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
		}

		return normalized;
	}
}
