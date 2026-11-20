using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Assets.Textures;

/// <summary>
/// Resolves material texture names to VFS entries. Gothic materials use
/// <c>.TGA</c> (or no extension) while the archives store <c>.TEX</c>.
/// </summary>
public static class TextureNameResolver
{
	public static IReadOnlyList<string> GetCandidates(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return Array.Empty<string>();
		}

		var normalized = name.Trim().Replace('\\', '/');
		var candidates = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		Add(normalized);

		var extension = Path.GetExtension(normalized);
		var stem = extension.Length == 0
			? normalized
			: normalized[..^extension.Length];

		if (!extension.Equals(".TEX", StringComparison.OrdinalIgnoreCase))
		{
			// Gothic stores most TGA textures as <name>-C.TEX.
			Add(stem + "-C.TEX");
			Add(stem + ".TEX");
		}

		return candidates;

		void Add(string candidate)
		{
			if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
			{
				candidates.Add(candidate);
			}
		}
	}

	public static VfsFileEntry? Resolve(IGameVfs vfs, string? name)
	{
		ArgumentNullException.ThrowIfNull(vfs);

		foreach (var candidate in GetCandidates(name))
		{
			var entry = vfs.Find(candidate);
			if (entry is not null)
			{
				return entry;
			}
		}

		return null;
	}
}
