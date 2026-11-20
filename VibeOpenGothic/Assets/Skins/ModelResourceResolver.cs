using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Assets.Skins;

/// <summary>
/// Files that belong to one MDS model. Gothic model scripts reference the
/// hierarchy and the mesh by their source (ASC) names, while the compiled data
/// uses MDH/MDM/MDL. The original engine tries the skeleton name first and
/// falls back to the script name, so both are resolved here.
/// </summary>
public sealed record ModelResources(
	string? HierarchyPath,
	string? BundlePath,
	IReadOnlyList<string> MeshPaths,
	IReadOnlyList<string> MissingMeshNames)
{
	public bool HasHierarchy => HierarchyPath is not null || BundlePath is not null;
}

public static class ModelResourceResolver
{
	public static ModelResources Resolve(
		IGameVfs vfs,
		string mdsPath,
		string? skeletonName,
		IReadOnlyList<string> meshes)
	{
		ArgumentNullException.ThrowIfNull(vfs);
		ArgumentException.ThrowIfNullOrWhiteSpace(mdsPath);
		ArgumentNullException.ThrowIfNull(meshes);

		var mdsStem = Path.GetFileNameWithoutExtension(mdsPath);
		var skeletonStem = ToStem(skeletonName);

		var hierarchyPath =
			FindFirst(vfs, Candidate(skeletonStem, ".MDH"), Candidate(mdsStem, ".MDH"));

		var bundlePath = hierarchyPath is null
			? FindFirst(vfs, Candidate(skeletonStem, ".MDL"), Candidate(mdsStem, ".MDL"))
			: null;

		var meshPaths = new List<string>();
		var missing = new List<string>();
		var seenStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var bundleStem = bundlePath is null ? null : Path.GetFileNameWithoutExtension(bundlePath);

		foreach (var sourceName in EnumerateMeshNames(skeletonName, meshes, mdsStem))
		{
			var stem = ToStem(sourceName);
			if (stem is null || !seenStems.Add(stem))
			{
				continue;
			}

			// A bundle already contains its own mesh.
			if (bundleStem is not null && stem.Equals(bundleStem, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var path = vfs.Find(stem + ".MDM")?.Path;
			if (path is null)
			{
				missing.Add(sourceName);
			}
			else
			{
				meshPaths.Add(path);
			}
		}

		return new ModelResources(hierarchyPath, bundlePath, meshPaths, missing);
	}

	private static IEnumerable<string> EnumerateMeshNames(
		string? skeletonName,
		IReadOnlyList<string> meshes,
		string mdsStem)
	{
		var any = false;

		if (!string.IsNullOrWhiteSpace(skeletonName))
		{
			any = true;
			yield return skeletonName;
		}

		foreach (var mesh in meshes)
		{
			if (string.IsNullOrWhiteSpace(mesh))
			{
				continue;
			}

			any = true;
			yield return mesh;
		}

		if (!any)
		{
			yield return mdsStem;
		}
	}

	private static string? Candidate(string? stem, string extension)
	{
		return string.IsNullOrWhiteSpace(stem) ? null : stem + extension;
	}

	private static string? FindFirst(IGameVfs vfs, params string?[] candidates)
	{
		foreach (var candidate in candidates)
		{
			if (candidate is null)
			{
				continue;
			}

			var entry = vfs.Find(candidate);
			if (entry is not null)
			{
				return entry.Path;
			}
		}

		return null;
	}

	private static string? ToStem(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		var trimmed = name.Trim();
		var stem = Path.GetFileNameWithoutExtension(trimmed);
		return string.IsNullOrWhiteSpace(stem) ? trimmed : stem;
	}
}
