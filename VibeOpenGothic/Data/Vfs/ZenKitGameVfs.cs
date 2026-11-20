using ZenKit;
using ZkVfs = ZenKit.Vfs;
using VibeOpenGothic.Data.Installs;

namespace VibeOpenGothic.Data.Vfs;

/// <summary>
/// ZenKit-backed VFS. Mounting follows OpenGothic: archives are mounted in
/// <see cref="MountPlanner"/> order with <c>Older</c> overwrite semantics, then
/// the loose <c>_work</c> directory is mounted without overriding archives.
/// ZenKit's Vfs is not thread-safe; use this type from one thread at a time.
/// </summary>
public sealed class ZenKitGameVfs : IGameVfs
{
	private readonly ZkVfs _vfs;
	private Dictionary<string, string>? _pathByName;
	private bool _disposed;

	private ZenKitGameVfs(ZkVfs vfs)
	{
		_vfs = vfs;
	}

	public static ZenKitGameVfs Mount(GothicInstall install, out IReadOnlyList<string> warnings)
	{
		ArgumentNullException.ThrowIfNull(install);

		var vfs = new ZkVfs();
		var warningList = new List<string>();

		foreach (var archive in install.Archives)
		{
			try
			{
				vfs.MountDisk(archive.Path, VfsOverwriteBehavior.Older);
			}
			catch (Exception exception)
			{
				warningList.Add($"Unable to mount archive '{archive.Path}': {exception.Message}");
			}
		}

		if (Directory.Exists(install.WorkDirectory))
		{
			try
			{
				vfs.Mkdir("/_work");
				vfs.Mount(install.WorkDirectory, "/_work", VfsOverwriteBehavior.None);
			}
			catch (Exception exception)
			{
				warningList.Add($"Unable to mount loose files from '{install.WorkDirectory}': {exception.Message}");
			}
		}

		warnings = warningList;
		return new ZenKitGameVfs(vfs);
	}

	public bool FileExists(string nameOrPath)
	{
		return Find(nameOrPath) is not null;
	}

	public bool DirectoryExists(string path)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		var node = _vfs.Resolve(NormalizePath(path));
		return node is not null && node.IsDir();
	}

	public Stream OpenRead(string nameOrPath)
	{
		ThrowIfDisposed();

		var entry = Find(nameOrPath) ?? throw new FileNotFoundException(
			$"Virtual file not found: '{nameOrPath}'.",
			nameOrPath);

		var node = _vfs.Resolve(entry.Path) ?? throw new FileNotFoundException(
			$"Virtual file not found: '{entry.Path}'.",
			entry.Path);

		if (!node.IsFile())
		{
			throw new FileNotFoundException($"Virtual path is not a file: '{entry.Path}'.", entry.Path);
		}

		return new MemoryStream(node.Buffer.Bytes, writable: false);
	}

	public VfsFileEntry? Find(string nameOrPath)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(nameOrPath);

		var normalized = nameOrPath.Replace('\\', '/').Trim();

		if (normalized.Contains('/'))
		{
			var path = NormalizePath(normalized);
			var node = _vfs.Resolve(path);
			return node is not null && node.IsFile()
				? new VfsFileEntry(path, node.Name)
				: null;
		}

		var resolvedPath = FindPathByName(normalized);
		return resolvedPath is null
			? null
			: new VfsFileEntry(resolvedPath, Path.GetFileName(resolvedPath));
	}

	public IEnumerable<VfsFileEntry> EnumerateFiles()
	{
		ThrowIfDisposed();
		return Walk(_vfs.Root, string.Empty);
	}

	public void Dispose()
	{
		// ZenKit.Vfs has a finalizer but no deterministic Dispose in 1.3.0-cs.1.
		_disposed = true;
		_pathByName = null;
	}

	private static IEnumerable<VfsFileEntry> Walk(VfsNode node, string path)
	{
		foreach (var child in node.Children)
		{
			var childPath = path + "/" + child.Name;

			if (child.IsFile())
			{
				yield return new VfsFileEntry(childPath, child.Name);
			}
			else if (child.IsDir())
			{
				foreach (var entry in Walk(child, childPath))
				{
					yield return entry;
				}
			}
		}
	}

	private string? FindPathByName(string name)
	{
		if (_pathByName is null)
		{
			_pathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var entry in EnumerateFiles())
			{
				_pathByName.TryAdd(entry.Name, entry.Path);
			}
		}

		return _pathByName.TryGetValue(name, out var path) ? path : null;
	}

	private static string NormalizePath(string path)
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

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}
}
